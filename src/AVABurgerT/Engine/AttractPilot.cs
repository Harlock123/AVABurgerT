using AVABurgerT.Models;

namespace AVABurgerT.Engine;

/// <summary>
/// The computer player for attract mode and the tests. It picks the nearest layer still to drop, takes
/// the ladders to its floor and walks right across it; but before each move it looks a moment ahead,
/// and if that move would bring an enemy too close it takes the safest one instead. An enemy right in
/// front gets a shake of pepper, while there's any left. It goes for a treat when one shows nearby.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private Piece? _target;
    private int _crossing;

    private const double Ahead = 0.55;

    public Controls Decide(GameEngine g)
    {
        if (g.Phase != Phase.Playing) return default;
        var c = g.Chef;

        // A shake of pepper for an enemy right in front.
        if (g.PepperLeft > 0 && g.Peppers.Count == 0)
            foreach (var e in Threats(g))
            {
                double dx = e.X - c.X, dy = e.Y - c.Y;
                bool level = e.Ladder is null && Math.Abs(dy) < 1 && Math.Abs(dx) < 20;
                bool above = Math.Abs(dx) < 6 && Math.Abs(dy) is > 0.5 and < 22;
                var face = level ? (dx < 0 ? Dir.Left : Dir.Right) : above ? (dy < 0 ? Dir.Up : Dir.Down) : Dir.None;
                if (face == Dir.None) continue;
                if (c.Facing == face) return new Controls(Dir.None, true);
                if (c.Ladder is null || !face.Horizontal()) return new Controls(face, false);
            }

        var plan = Plan(g);
        // Look a moment ahead: follow the plan if it stays clear of the enemies, otherwise take the safest move.
        Dir best = plan;
        double bestValue = double.MinValue;
        foreach (var move in new[] { plan, Dir.None, Dir.Left, Dir.Right, Dir.Up, Dir.Down }.Distinct())
        {
            var ghost = new Chef { X = c.X, Y = c.Y, Ladder = c.Ladder, Facing = c.Facing };
            for (double t = 0; t < Ahead; t += 1.0 / 30) GameEngine.Advance(ghost, g.Kitchen, move, 1.0 / 30);
            double gap = Threats(g).Select(e => Distance(ghost.Pos, Predict(g, e, ghost.Pos), ghost.Ladder is not null)).DefaultIfEmpty(200).Min();
            double value = Math.Min(gap, 48) * 2 + (move == plan ? 30 : 0) + (gap < 18 ? -200 : 0);
            if (value > bestValue) { bestValue = value; best = move; }
        }
        return new Controls(best, false);
    }

    private static IEnumerable<Enemy> Threats(GameEngine g) =>
        g.Enemies.Where(e => e.Active && e.Stunned <= 0.3 && e.Riding is null && e.X > Field.Left - 30 && e.X < Field.Right + 30);

    /// <summary>Where an enemy will be shortly: coming along its floor towards the point, or on along its ladder.</summary>
    private static Vec Predict(GameEngine g, Enemy e, Vec towards)
    {
        double step = g.EnemySpeed * Ahead;
        if (e.Ladder is not null) return new Vec(e.X, e.Y + e.Dir.Dy() * step * GameEngine.LadderPace);
        if (Math.Abs(e.Y - towards.Y) < 1) return new Vec(e.X + Math.Clamp(towards.X - e.X, -step, step), e.Y);
        return new Vec(e.X + e.Dir.Dx() * step, e.Y);
    }

    /// <summary>How far apart two places are for an enemy: along a floor, up a ladder, or (between floors) the long way.</summary>
    private static double Distance(Vec a, Vec b, bool onLadder)
    {
        double dx = Math.Abs(a.X - b.X), dy = Math.Abs(a.Y - b.Y);
        if (dy < 1) return dx;
        if (dx < 8) return dy + dx;
        return dx + dy * (onLadder ? 1.2 : 2);
    }

    /// <summary>The way the plan says: to the next layer to drop, or across the one we're on.</summary>
    private Dir Plan(GameEngine g)
    {
        var c = g.Chef;
        if (c.Ladder is { } ladder) return GoalFloor(g) <= ladder.Upper ? Dir.Up : Dir.Down;
        int floor = c.Floor;
        if (g.TreatLeft > 1 && floor == 2) return Walk(c, GameEngine.TreatSpot.X).Move;
        if (_target is null || _target.Falling || _target.OnPlate || _target.Floor < 0) { _target = Pick(g); _crossing = 0; }
        if (_target is null) return Dir.None;
        if (_target.Floor != floor) return Route(g, floor, _target.Floor).Move;
        if (_crossing == 0) _crossing = c.X < _target.X + Field.PieceWidth / 2 ? 1 : -1;
        double end = _crossing > 0 ? _target.X + Field.PieceWidth + 2 : _target.X - 2;
        if (Math.Abs(c.X - end) < 1.5) _crossing = -_crossing;
        return Walk(c, end).Move;
    }

    private static Controls Walk(Chef c, double x) =>
        Math.Abs(x - c.X) < 1 ? default : new Controls(x < c.X ? Dir.Left : Dir.Right, false);

    private int GoalFloor(GameEngine g) => _target?.Floor is >= 0 and var f ? f : Field.Floors.Length - 1;

    /// <summary>The nearest layer still to go down: fewer floors away first, then closer along the floor.</summary>
    private Piece? Pick(GameEngine g)
    {
        var c = g.Chef;
        int floor = c.Floor < 0 ? 0 : c.Floor;
        return g.Kitchen.Pieces.Where(p => !p.Falling && !p.OnPlate && p.Floor >= 0)
            .OrderBy(p => Math.Abs(p.Floor - floor) * 60 + Math.Abs(p.X + 16 - c.X) + _rng.NextDouble() * 4).FirstOrDefault();
    }

    /// <summary>To the ladder that leads towards the target floor, and onto it.</summary>
    private static Controls Route(GameEngine g, int floor, int target)
    {
        var c = g.Chef;
        bool up = target < floor;
        var ladder = g.Kitchen.LaddersAt(floor).Where(l => up ? l.Lower == floor : l.Upper == floor)
            .OrderBy(l => Math.Abs(l.X - c.X)).FirstOrDefault();
        if (ladder is null) return default;
        if (Math.Abs(ladder.X - c.X) > 1) return Walk(c, ladder.X);
        return new Controls(up ? Dir.Up : Dir.Down, false);
    }
}
