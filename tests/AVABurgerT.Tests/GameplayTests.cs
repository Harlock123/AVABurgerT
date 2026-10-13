using AVABurgerT.Engine;
using AVABurgerT.Models;

namespace AVABurgerT.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "burger-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>A level under way with the enemies held at the doors.</summary>
    private GameEngine Calm(int level = 1, int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(level);
        g.Calm();
        return g;
    }

    private static void Steps(GameEngine g, int n, Controls c = default)
    {
        for (int i = 0; i < n; i++) g.Step(c);
    }

    private static Piece PieceOn(GameEngine g, Func<Piece, bool> where) => g.Kitchen.Pieces.First(p => !p.OnPlate && where(p));

    [Fact]
    public void Every_kitchen_has_four_burgers_in_order_and_ladders_joining_every_floor()
    {
        for (int level = 1; level <= 10; level++)
        {
            var k = Kitchen.Build(level);
            Assert.Equal(16, k.Pieces.Count);
            for (int col = 0; col < 4; col++)
            {
                var layers = k.Pieces.Where(p => p.Column == col).OrderBy(p => p.Floor).ToList();
                Assert.Equal(4, layers.Count);
                Assert.Equal(Layer.BunTop, layers[0].Layer);
                Assert.Equal(Layer.BunBottom, layers[3].Layer);
                Assert.Equal(4, layers.Select(p => p.Floor).Distinct().Count());
            }
            for (int f = 0; f < Field.Floors.Length - 1; f++) Assert.Contains(k.Ladders, l => l.Upper == f && l.Lower == f + 1);
            // No ladder runs through a burger.
            Assert.All(k.Ladders, l => Assert.DoesNotContain(Field.Columns, x => l.X > x - 4 && l.X < x + Field.PieceWidth + 4));
        }
    }

    [Fact]
    public void The_chef_walks_the_floors_and_climbs_the_ladders()
    {
        var g = Calm();
        var c = g.Chef;
        Assert.Equal(Kitchen.ChefStart, c.Pos);
        Steps(g, 30, new Controls(Dir.Right, false));
        Assert.True(c.X > Kitchen.ChefStart.X + 20);
        // Up the nearest ladder from the bottom floor (sliding over to it first).
        var ladder = g.Kitchen.LaddersAt(5).Where(l => l.Lower == 5).OrderBy(l => Math.Abs(l.X - c.X)).First();
        g.PutChef(ladder.X + 6, 5);
        Steps(g, 60 * 2, new Controls(Dir.Up, false));
        Assert.Equal(Field.Floors[4], c.Y);
        Assert.Null(c.Ladder);
        // No climbing where there's no ladder.
        g.PutChef(40, 5);
        Steps(g, 30, new Controls(Dir.Up, false));
        Assert.Equal(Field.Floors[5], c.Y);
    }

    [Fact]
    public void Walking_all_four_quarters_drops_a_layer_and_it_knocks_the_one_below()
    {
        var g = Calm();
        // A layer with another directly below it on the next floor.
        var top = PieceOn(g, p => g.Kitchen.Pieces.Any(q => q.Column == p.Column && q.Floor == p.Floor + 1));
        var below = g.Kitchen.Pieces.Single(q => q.Column == top.Column && q.Floor == top.Floor + 1);
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        int from = top.Floor;
        g.PutChef(top.X - 4, from);
        Steps(g, 20, new Controls(Dir.Right, false));
        Assert.Equal(2, top.Stepped.Count(s => s));
        Assert.Contains(Sfx.Squeak, heard);
        Assert.False(top.Falling);
        for (int i = 0; i < 40 && !top.Falling; i++) g.Step(new Controls(Dir.Right, false));
        Assert.True(top.Falling);
        Assert.Contains(Sfx.Drop, heard);
        Steps(g, 60);
        Assert.Equal(from + 1, top.Floor);
        Assert.True(below.Floor > from + 1 || below.OnPlate, "knocked down in turn");
        Assert.True(g.Score >= 100);
    }

    [Fact]
    public void Layers_stack_on_the_plate_and_four_make_a_burger()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        // Walk every layer of the first column down, bottom first, until they're all plated.
        for (int round = 0; round < 40 && g.Kitchen.Pieces.Count(p => p.Column == 0 && p.OnPlate) < 4; round++)
        {
            var p = g.Kitchen.Pieces.Where(q => q.Column == 0 && !q.OnPlate && !q.Falling).OrderByDescending(q => q.Floor).First();
            g.PutChef(p.X - 4, p.Floor);
            Steps(g, 60, new Controls(Dir.Right, false));
            Steps(g, 60 * 2);
        }
        var plated = g.Kitchen.Pieces.Where(p => p.Column == 0 && p.OnPlate).OrderByDescending(p => p.Y).ToList();
        Assert.Equal(4, plated.Count);
        Assert.Equal(Layer.BunBottom, plated[0].Layer);
        Assert.Equal(Layer.BunTop, plated[3].Layer);
        Assert.Contains(Sfx.Plate, heard);
        Assert.Contains(Sfx.BurgerDone, heard);
    }

    [Fact]
    public void All_four_burgers_finish_the_level()
    {
        var g = Calm();
        foreach (var p in g.Kitchen.Pieces.Skip(1)) { p.OnPlate = true; p.Floor = -1; }
        var last = g.Kitchen.Pieces[0];
        for (int i = 0; i < 6 && !last.OnPlate; i++)
        {
            if (!last.Falling) { g.PutChef(last.X - 4, last.Floor); Steps(g, 60, new Controls(Dir.Right, false)); }
            Steps(g, 60);
        }
        Assert.Equal(Phase.LevelClear, g.Phase);
        Steps(g, (int)(60 * GameEngine.LevelClearSeconds) + 2);
        Assert.Equal(2, g.Level);
        Assert.Equal(Phase.Ready, g.Phase);
        Assert.All(g.Kitchen.Pieces, p => Assert.False(p.OnPlate));
    }

    [Fact]
    public void A_layer_squashes_an_enemy_below_and_carries_one_riding_it_further()
    {
        // Clear a column below one layer so it has room to fall.
        static Piece Clear(GameEngine g, int floor)
        {
            var p = g.Kitchen.Pieces.First(q => q.Column == 1 && q.Floor == floor || q.Column == 1 && q.Layer == Layer.BunTop);
            p.Floor = floor;
            p.Y = Field.Floors[floor];
            foreach (var q in g.Kitchen.Pieces.Where(q => q.Column == 1 && q != p)) { q.OnPlate = true; q.Floor = -1; }
            return p;
        }

        var g = Calm();
        var top = Clear(g, 1);
        var under = g.PutEnemy(EnemyKind.Egg, top.X + 16, 2);
        g.PutChef(top.X - 4, 1);
        Steps(g, 60, new Controls(Dir.Right, false));
        Steps(g, 30);
        Assert.True(under.Away > 0, "squashed");
        Assert.Equal(50 + 500, g.Score);

        g = Calm();
        var piece = Clear(g, 1);
        var rider = g.PutEnemy(EnemyKind.Sausage, piece.X + 24, 1);
        rider.Stunned = 5;
        g.PutChef(piece.X - 4, 1);
        for (int i = 0; i < 60 && !piece.Falling; i++) g.Step(new Controls(Dir.Right, false));
        Assert.Same(piece, rider.Riding);
        Steps(g, 60);
        Assert.Equal(3, piece.Floor); // a floor further for its passenger
        Assert.True(rider.Away > 0);
        Assert.Equal(50 + 500, g.Score);
    }

    [Fact]
    public void Pepper_stuns_an_enemy_and_runs_out()
    {
        var g = Calm();
        g.Wake();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutChef(60, 5);
        var e = g.PutEnemy(EnemyKind.Pickle, 75, 5);
        g.Chef.Facing = Dir.Right;
        g.Step(new Controls(Dir.None, true));
        Assert.Equal(GameEngine.StartingPepper - 1, g.PepperLeft);
        Steps(g, 3);
        Assert.True(e.Stunned > 0);
        Assert.Contains(Sfx.Stunned, heard);
        // Harmless while dizzy: walk straight through it.
        Steps(g, 30, new Controls(Dir.Right, false));
        Assert.Equal(Phase.Playing, g.Phase);

        g.SetPepper(0);
        g.Step(default);
        Steps(g, 30);
        g.Step(new Controls(Dir.None, true));
        Assert.Contains(Sfx.NoPepper, heard);
    }

    [Fact]
    public void An_enemy_catching_the_chef_costs_a_life()
    {
        var g = Calm();
        g.Wake();
        g.PutChef(60, 5);
        g.PutEnemy(EnemyKind.Sausage, 100, 5);
        for (int i = 0; i < 60 * 5 && g.Phase == Phase.Playing; i++) g.Step(default);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("sausage", g.LastDeath);
        Steps(g, (int)(60 * (GameEngine.DyingSeconds + GameEngine.ReadySeconds)) + 4);
        Assert.Equal(2, g.Lives);
        Assert.Equal(Kitchen.ChefStart, g.Chef.Pos);
    }

    [Fact]
    public void Enemies_climb_ladders_to_reach_the_chef()
    {
        var g = Calm();
        g.Wake();
        g.PutChef(30, 0);
        var e = g.PutEnemy(EnemyKind.Egg, 100, 5);
        bool climbed = false;
        for (int i = 0; i < 60 * 30 && g.Phase == Phase.Playing; i++)
        {
            g.Chef.X = 30; g.Chef.Y = Field.Floors[0];
            g.Step(default);
            if (e.Ladder is not null) climbed = true;
        }
        Assert.True(climbed);
        Assert.Equal(Phase.Dying, g.Phase);
    }

    [Fact]
    public void A_treat_after_six_drops_gives_points_and_pepper()
    {
        var g = Calm();
        for (int i = 0; i < GameEngine.TreatEvery && g.TreatLeft <= 0; i++)
        {
            var p = g.Kitchen.Pieces.Where(q => !q.OnPlate && !q.Falling).OrderByDescending(q => q.Floor).First();
            g.PutChef(p.X - 4, p.Floor);
            Steps(g, 60, new Controls(Dir.Right, false));
            Steps(g, 60);
        }
        Assert.True(g.TreatLeft > 0);
        int pepper = g.PepperLeft, score = g.Score;
        g.PutChef(GameEngine.TreatSpot.X, 2);
        g.Step(default);
        Assert.Equal(0, g.TreatLeft);
        Assert.Equal(pepper + 1, g.PepperLeft);
        Assert.Equal(score + GameEngine.TreatPoints(1), g.Score);
    }

    [Fact]
    public void Bonus_chefs_at_ten_thousand_and_every_twenty_thousand_after()
    {
        var g = Calm();
        g.AddScore(9999);
        Assert.Equal(3, g.Lives);
        g.AddScore(1);
        Assert.Equal(4, g.Lives);
        g.AddScore(20000);
        Assert.Equal(5, g.Lives);
    }

    [Fact]
    public void The_demonstration_pilot_builds_burgers_and_the_game_ends_in_the_table()
    {
        int best = 0;
        var losses = new List<string>();
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = new GameEngine(seed, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(seed);
            for (int i = 0; i < 60 * 60 * 4 && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
            int plated = g.Level > 1 ? 16 : g.Kitchen.Pieces.Count(p => p.OnPlate);
            best = Math.Max(best, plated);
            losses.AddRange(g.LossLog);
            if (seed == 1)
            {
                for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
                Assert.Equal(GameState.GameOver, g.State);
                g.Idle(60 * 4);
                if (g.State == GameState.EnterInitials) { g.Initials!.Finish(); g.Idle(2); Assert.Equal(g.Score, HighScoreTable.Load(_dir).TopScore); }
                Assert.Equal(GameState.HighScores, g.State);
            }
        }
        Assert.True(best >= 16, $"best {best} plated; lost: {string.Join("; ", losses)}");
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = new GameEngine(21, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Step(pilot.Decide(g));
            return $"{g.Level} {g.Score} {g.Lives} {g.Chef.Pos} {g.PepperLeft} {g.Kitchen.Pieces.Count(p => p.OnPlate)}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 10)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.StartTune, heard);
        Assert.Contains(Sfx.Music, heard);
        Assert.Contains(Sfx.Step, heard);
        g.Idle((int)(60 * (GameEngine.AttractSeconds - 8)));
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(Sfx.MusicStop, heard.Last(s => s is Sfx.Music or Sfx.MusicStop));
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Pausing_stops_the_music()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Pause();
        Assert.Equal(Sfx.MusicStop, heard[^1]);
        var pos = g.Chef.Pos;
        Steps(g, 30, new Controls(Dir.Right, false));
        Assert.Equal(pos, g.Chef.Pos);
        g.Resume();
        Assert.Equal(Sfx.Music, heard[^1]);
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var files = Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop).Select(s => s.ToString());
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
    }

    [Fact]
    public void Every_sprite_is_well_formed()
    {
        foreach (var s in new[] { Sprites.ChefWalk[0], Sprites.ChefClimb[0], Sprites.SausageWalk[0], Sprites.EggWalk[0], Sprites.PickleWalk[0], Sprites.IceCream, Sprites.Fries })
        {
            Assert.Equal((16, 16), (s.Width, s.Height));
            Assert.All(s.Rows.SelectMany(r => r).Where(ch => ch != '.'), ch => Assert.True(Sprites.Colours.ContainsKey(ch), $"colour {ch}"));
        }
    }
}
