using AVABurgerT.Models;

namespace AVABurgerT.Engine;

public sealed partial class GameEngine
{
    public const double ChefSpeed = 46;
    public const double ClimbSpeed = 34;
    public const double FallSpeed = 140;
    public const int StartingPepper = 5;
    public const double StunSeconds = 2.5;
    public const double AwaySeconds = 4;
    public const double TreatSeconds = 8;
    public const double ReadySeconds = 1.4;
    public const double FirstReadySeconds = 2.4;
    public const double DyingSeconds = 2.5;
    public const double LevelClearSeconds = 3;
    /// <summary>A treat comes out after this many layers dropped.</summary>
    public const int TreatEvery = 6;
    /// <summary>Enemies climb slower than they walk.</summary>
    public const double LadderPace = 0.75;

    public Kitchen Kitchen { get; private set; }
    public Chef Chef { get; } = new();
    public List<Enemy> Enemies { get; } = [];
    public List<Pepper> Peppers { get; } = [];
    public int PepperLeft { get; private set; } = StartingPepper;
    public double TreatLeft { get; private set; }
    public Treat TreatKind { get; private set; }
    public static readonly Vec TreatSpot = new(112, Field.Floors[2]);
    public int Dropped { get; private set; }
    public string? LastDeath { get; private set; }
    internal List<string> LossLog { get; } = [];

    private double _readyFor = ReadySeconds;
    private bool _pepperPrev;
    private double _stepSound;
    private bool _calm;

    public double EnemySpeed => Math.Min(46, 26 + 2.5 * (Level - 1));

    /// <summary>Where enemies walk on from: the ends of the top and bottom floors.</summary>
    private static readonly (double X, int Floor)[] Doors = [(-8, 0), (Field.Width + 8, 0), (-8, 5), (Field.Width + 8, 5)];

    // ----- levels -------------------------------------------------------------------------------

    private void StartLevel(int level)
    {
        Level = level;
        Kitchen = Kitchen.Build(level);
        SetUpLevel();
        SetPhase(Phase.Ready);
        _readyFor = level == 1 ? FirstReadySeconds : ReadySeconds;
    }

    private void SetUpLevel()
    {
        Enemies.Clear();
        EnemyKind[] kinds = [EnemyKind.Sausage, EnemyKind.Sausage, EnemyKind.Egg, EnemyKind.Pickle, EnemyKind.Sausage, EnemyKind.Egg];
        int count = Math.Min(6, 3 + (Level - 1) / 2);
        for (int i = 0; i < count; i++) Enemies.Add(new Enemy { Kind = kinds[i] });
        Peppers.Clear();
        TreatLeft = 0;
        Dropped = 0;
        PlaceEveryone();
    }

    /// <summary>The chef back at the bottom, the enemies back at the doors to come on one by one.</summary>
    private void PlaceEveryone()
    {
        Chef.X = Kitchen.ChefStart.X;
        Chef.Y = Kitchen.ChefStart.Y;
        Chef.Ladder = null;
        Chef.Facing = Dir.Right;
        Chef.Moving = false;
        for (int i = 0; i < Enemies.Count; i++)
        {
            var e = Enemies[i];
            var door = Doors[i % Doors.Length];
            e.X = door.X;
            e.Y = Field.Floors[door.Floor];
            e.Ladder = null;
            e.Dir = door.X < 0 ? Dir.Right : Dir.Left;
            e.Stunned = 0;
            e.Riding = null;
            e.Away = 0;
            e.Delay = 0.8 + i * 1.6;
        }
        Peppers.Clear();
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        switch (Phase)
        {
            case Phase.Ready:
                if (PhaseTime >= _readyFor) { SetPhase(Phase.Playing); Music(true); }
                return;
            case Phase.Dying:
                Music(false);
                if (PhaseTime < DyingSeconds) return;
                Lives--;
                if (Lives <= 0) { GameOver(); return; }
                PlaceEveryone();
                SetPhase(Phase.Ready);
                _readyFor = ReadySeconds;
                return;
            case Phase.LevelClear:
                Music(false);
                if (PhaseTime >= LevelClearSeconds) StartLevel(Level + 1);
                return;
        }

        MoveChef(dt, c);
        ShakePepper(dt, c);
        MovePieces(dt);
        if (Phase != Phase.Playing) return;
        MoveEnemies(dt);
        Treats(dt);
        Collide();
    }

    private void Die(string why)
    {
        LastDeath = why;
        LossLog.Add($"L{Level}: {why} at {Chef.Pos}");
        SetPhase(Phase.Dying);
        Play(Sfx.Death);
        Music(false);
    }

    // ----- the chef -----------------------------------------------------------------------------

    private void MoveChef(double dt, Controls c)
    {
        Chef.Moving = false;
        double moved = Advance(Chef, Kitchen, c.Move, dt);
        if (moved <= 0) return;
        Moved(moved);
        if (Chef.Ladder is null && c.Move != Dir.None && Chef.Floor >= 0) StepOn(Chef.Floor);
    }

    /// <summary>
    /// Moves a chef one step: along a floor, up or down a ladder, or (asked to climb near a ladder)
    /// over to it. Returns the distance moved. Has no other effect, so the computer player can use it to
    /// look ahead.
    /// </summary>
    public static double Advance(Chef chef, Kitchen kitchen, Dir want, double dt)
    {
        if (want == Dir.None) return 0;
        if (chef.Ladder is { } l)
        {
            if (want.Horizontal()) return 0;
            chef.Facing = want;
            double top = Field.Floors[l.Upper], bottom = Field.Floors[l.Lower];
            double before = chef.Y;
            chef.Y = Math.Clamp(chef.Y + want.Dy() * ClimbSpeed * dt, top, bottom);
            if (chef.Y <= top || chef.Y >= bottom) chef.Ladder = null;
            return Math.Abs(chef.Y - before);
        }
        int floor = chef.Floor;
        if (floor < 0) return 0;
        if (want.Horizontal())
        {
            chef.Facing = want;
            double before = chef.X;
            chef.X = Math.Clamp(chef.X + want.Dx() * ChefSpeed * dt, Field.Left + 4, Field.Right - 4);
            return Math.Abs(chef.X - before);
        }
        // Up or down: onto a ladder here, or slide over to one close by.
        var ladder = kitchen.LaddersAt(floor)
            .Where(l2 => want == Dir.Up ? l2.Lower == floor : l2.Upper == floor)
            .OrderBy(l2 => Math.Abs(l2.X - chef.X)).FirstOrDefault();
        if (ladder is null || Math.Abs(ladder.X - chef.X) > 10)
        {
            chef.Facing = want; // turns to face that way (for the pepper), but stays put
            return 0;
        }
        if (Math.Abs(ladder.X - chef.X) > 1)
        {
            double before = chef.X;
            chef.X += Math.Clamp(ladder.X - chef.X, -ChefSpeed * dt, ChefSpeed * dt);
            return Math.Abs(chef.X - before);
        }
        chef.X = ladder.X;
        chef.Ladder = ladder;
        chef.Facing = want;
        return 0.001;
    }

    private void Moved(double distance)
    {
        Chef.Moving = true;
        Chef.Stride += distance;
        _stepSound += distance;
        if (_stepSound >= 8) { _stepSound = 0; Play(Sfx.Step); }
    }

    /// <summary>Walking over a layer: each quarter sags under the chef; all four and down it goes.</summary>
    private void StepOn(int floor)
    {
        foreach (var p in Kitchen.Pieces)
        {
            if (p.Falling || p.OnPlate || p.Floor != floor) continue;
            int q = (int)Math.Floor((Chef.X - p.X) / (Field.PieceWidth / 4));
            if (q is < 0 or > 3 || p.Stepped[q]) continue;
            p.Stepped[q] = true;
            Play(Sfx.Squeak);
            if (p.Stepped.All(s => s)) Drop(p);
        }
    }

    // ----- the layers ---------------------------------------------------------------------------

    /// <summary>The layer starts down to the next floor, taking any enemies standing on it (a floor further for each).</summary>
    private void Drop(Piece p)
    {
        int from = p.Floor;
        p.Falling = true;
        p.Floor = -1;
        Array.Clear(p.Stepped);
        var riders = Enemies.Where(e => e.Active && e.Riding is null && e.Ladder is null && e.Floor == from && e.X >= p.X - 2 && e.X <= p.X + Field.PieceWidth + 2).ToList();
        foreach (var e in riders) { e.Riding = p; e.Stunned = 0; }
        p.ExtraFloors = riders.Count;
        p.LandY = from + 1 < Field.Floors.Length ? Field.Floors[from + 1] : PlateTop(p.Column);
        AddScore(50);
        Play(Sfx.Drop);
        Dropped++;
        if (Dropped % TreatEvery == 0 && TreatLeft <= 0)
        {
            TreatLeft = TreatSeconds;
            TreatKind = (Treat)((Dropped / TreatEvery - 1) % 3);
            Play(Sfx.TreatShows);
        }
    }

    /// <summary>The top of the stack building on a column's plate.</summary>
    public double PlateTop(int column) => Field.PlateY - 2 - 5 * Kitchen.Pieces.Count(p => p.Column == column && p.OnPlate);

    private void MovePieces(double dt)
    {
        foreach (var p in Kitchen.Pieces.Where(p => p.Falling).ToList())
        {
            p.Y += FallSpeed * dt;
            // Anything standing in the way below is flattened.
            foreach (var e in Enemies)
            {
                if (!e.Active || e.Riding is not null || e.X < p.X - 2 || e.X > p.X + Field.PieceWidth + 2) continue;
                if (e.Y > p.Y - 2 && e.Y - p.Y < 12)
                {
                    e.Away = AwaySeconds;
                    e.Ladder = null;
                    AddScore(500);
                    Popup(e.Pos with { Y = e.Y - 12 }, 500);
                    Play(Sfx.Squash);
                }
            }
            foreach (var e in Enemies.Where(e => e.Riding == p)) { e.X = Math.Clamp(e.X, p.X + 4, p.X + Field.PieceWidth - 4); e.Y = p.Y; }
            if (p.Y < p.LandY) continue;
            Land(p);
        }
    }

    private void Land(Piece p)
    {
        p.Y = p.LandY;
        int floor = Field.FloorAt(p.Y);
        // Reaching a floor with another layer on it: that one's knocked down in turn.
        if (floor >= 0)
        {
            var below = Kitchen.Pieces.FirstOrDefault(q => q != p && !q.Falling && !q.OnPlate && q.Column == p.Column && q.Floor == floor);
            if (below is not null) Drop(below);
            if (p.ExtraFloors > 0)
            {
                p.ExtraFloors--;
                p.LandY = floor + 1 < Field.Floors.Length ? Field.Floors[floor + 1] : PlateTop(p.Column);
                return;
            }
            p.Falling = false;
            p.Floor = floor;
            Play(Sfx.Thud);
        }
        else
        {
            p.Y = PlateTop(p.Column);
            p.Falling = false;
            p.OnPlate = true;
            p.Floor = -1;
            Play(Sfx.Plate);
            if (Kitchen.Pieces.Count(q => q.Column == p.Column && q.OnPlate) == 4)
            {
                AddScore(500);
                Popup(new Vec(p.X + 16, p.Y - 20), 500);
                Play(Sfx.BurgerDone);
            }
        }
        // Riders come to grief at the bottom of the ride.
        var riders = Enemies.Where(e => e.Riding == p).ToList();
        if (riders.Count > 0)
        {
            int points = 500 << (riders.Count - 1);
            AddScore(points);
            Popup(new Vec(p.X + 16, p.Y - 14), points);
            Play(Sfx.Squash);
            foreach (var e in riders) { e.Riding = null; e.Away = AwaySeconds; }
        }
        if (Kitchen.Pieces.All(q => q.OnPlate))
        {
            SetPhase(Phase.LevelClear);
            Play(Sfx.LevelClear);
        }
    }

    // ----- pepper and treats --------------------------------------------------------------------

    private void ShakePepper(double dt, Controls c)
    {
        bool pressed = c.Pepper && !_pepperPrev;
        _pepperPrev = c.Pepper;
        foreach (var p in Peppers) p.Age += dt;
        Peppers.RemoveAll(p => p.Age >= Pepper.Life);
        if (pressed && Peppers.Count == 0)
        {
            if (PepperLeft <= 0) { Play(Sfx.NoPepper); return; }
            PepperLeft--;
            var f = Chef.Facing;
            Peppers.Add(new Pepper { Pos = new Vec(Chef.X + f.Dx() * 14, Chef.Y - 8 + f.Dy() * 14) });
            Play(Sfx.Pepper);
        }
        foreach (var p in Peppers)
            foreach (var e in Enemies)
                if (e.Active && e.Riding is null && e.Stunned <= 0 && Math.Abs(e.X - p.Pos.X) < 12 && Math.Abs(e.Y - 8 - p.Pos.Y) < 12)
                {
                    e.Stunned = StunSeconds;
                    Play(Sfx.Stunned);
                }
    }

    public static int TreatPoints(int level) => 500 * Math.Min(3, level);

    private void Treats(double dt)
    {
        if (TreatLeft <= 0) return;
        TreatLeft -= dt;
        if (Chef.Ladder is not null || Math.Abs(Chef.Y - TreatSpot.Y) > 1 || Math.Abs(Chef.X - TreatSpot.X) > 8) return;
        TreatLeft = 0;
        int points = TreatPoints(Level);
        AddScore(points);
        PepperLeft++;
        Popup(TreatSpot with { Y = TreatSpot.Y - 18 }, points);
        Play(Sfx.Treat);
    }

    // ----- the enemies --------------------------------------------------------------------------

    private void MoveEnemies(double dt)
    {
        foreach (var e in Enemies)
        {
            if (e.Away > 0)
            {
                e.Away -= dt;
                if (e.Away <= 0) Return(e);
                continue;
            }
            if (e.Delay > 0) { e.Delay -= dt; continue; }
            if (e.Riding is not null) continue;
            if (e.Stunned > 0) { e.Stunned -= dt; continue; }
            if (_calm) continue;
            double step = EnemySpeed * dt;
            if (e.Ladder is { } l)
            {
                double top = Field.Floors[l.Upper], bottom = Field.Floors[l.Lower];
                e.Y = Math.Clamp(e.Y + e.Dir.Dy() * step * LadderPace, top, bottom);
                if (e.Y <= top || e.Y >= bottom)
                {
                    e.Ladder = null;
                    e.Dir = Chef.X < e.X ? Dir.Left : Dir.Right;
                }
                continue;
            }
            // Walking on from a door.
            if (e.X < Field.Left || e.X > Field.Right)
            {
                e.X += (e.X < Field.Left ? 1 : -1) * step;
                continue;
            }
            Think(e);
            if (e.Ladder is not null) continue;
            e.X += e.Dir.Dx() * step;
            if (e.X <= Field.Left + 4 || e.X >= Field.Right - 4)
            {
                e.X = Math.Clamp(e.X, Field.Left + 4, Field.Right - 4);
                e.Dir = e.Dir == Dir.Left ? Dir.Right : Dir.Left;
            }
        }
    }

    /// <summary>On a floor: head for the chef along it, or for a ladder towards the chef's floor.</summary>
    private void Think(Enemy e)
    {
        int floor = e.Floor;
        if (floor < 0) return;
        int target = Chef.Ladder is { } cl ? (Chef.Y - Field.Floors[cl.Upper] < Field.Floors[cl.Lower] - Chef.Y ? cl.Upper : cl.Lower) : Chef.Floor;
        if (target < 0 || target == floor)
        {
            if (Math.Abs(Chef.X - e.X) > 2) e.Dir = Chef.X < e.X ? Dir.Left : Dir.Right;
            return;
        }
        bool up = target < floor;
        var ladders = Kitchen.LaddersAt(floor).Where(l => up ? l.Lower == floor : l.Upper == floor).ToList();
        if (ladders.Count == 0) return;
        var at = ladders.FirstOrDefault(l => Math.Abs(l.X - e.X) < 1.2);
        if (at is not null)
        {
            e.Think -= Field.Step;
            if (e.Think <= 0)
            {
                e.Think = 0.6;
                if (_rng.NextDouble() < 0.8)
                {
                    e.X = at.X;
                    e.Ladder = at;
                    e.Dir = up ? Dir.Up : Dir.Down;
                    return;
                }
            }
        }
        var aim = ladders.OrderBy(l => Math.Abs(l.X - e.X) + Math.Abs(l.X - Chef.X) * 0.3).First();
        if (Math.Abs(aim.X - e.X) > 1) e.Dir = aim.X < e.X ? Dir.Left : Dir.Right;
    }

    /// <summary>Back on from a door after being squashed.</summary>
    private void Return(Enemy e)
    {
        var door = Doors[_rng.Next(Doors.Length)];
        e.X = door.X;
        e.Y = Field.Floors[door.Floor];
        e.Dir = door.X < 0 ? Dir.Right : Dir.Left;
        e.Ladder = null;
        e.Stunned = 0;
        e.Riding = null;
    }

    private void Collide()
    {
        foreach (var e in Enemies)
        {
            if (!e.Active || e.Stunned > 0 || e.Riding is not null) continue;
            if (Math.Abs(e.X - Chef.X) < 10 && Math.Abs(e.Y - Chef.Y) < 10)
            {
                Die($"was caught by the {e.Kind.ToString().ToLowerInvariant()}");
                return;
            }
        }
    }

    // ----- for the tests and screenshots --------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Step(default);
    }

    internal void Go()
    {
        if (Phase != Phase.Ready) return;
        SetPhase(Phase.Playing);
        Music(true);
    }

    internal void JumpTo(int level)
    {
        StartLevel(level);
        Go();
    }

    /// <summary>Keeps the enemies where they are (off at the doors, unless put somewhere).</summary>
    internal void Calm() => _calm = true;
    internal void Wake() => _calm = false;

    internal void PutChef(double x, int floor)
    {
        Chef.X = x;
        Chef.Y = Field.Floors[floor];
        Chef.Ladder = null;
    }

    internal Enemy PutEnemy(EnemyKind kind, double x, int floor)
    {
        var e = Enemies.FirstOrDefault(e => e.Kind == kind && e.Delay > 0) ?? Enemies[0];
        e.X = x;
        e.Y = Field.Floors[floor];
        e.Delay = 0;
        e.Away = 0;
        e.Ladder = null;
        return e;
    }

    internal void SetPepper(int n) => PepperLeft = n;
}
