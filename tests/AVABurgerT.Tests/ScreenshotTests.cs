using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVABurgerT.Engine;
using AVABurgerT.Models;
using AVABurgerT.Views;
using Controls = AVABurgerT.Engine.Controls;

namespace AVABurgerT.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set BURGER_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "burger-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("BURGER_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVABurgerT-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_title_and_a_kitchen_at_work()
    {
        var (w, c, g) = Open();
        g.Idle(60 * 2);
        Shot(w, c, "title");
        g.StartGame();
        g.Go();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 && g.State == GameState.Playing; i++)
        {
            g.Step(pilot.Decide(g));
            if (i > 60 * 20 && g.Phase == Phase.Playing && g.Kitchen.Pieces.Any(p => p.Falling) && g.Kitchen.Pieces.Count(p => p.OnPlate) >= 2) break;
        }
        Shot(w, c, "kitchen");
        w.Close();
    }

    [AvaloniaFact]
    public void Pepper_and_a_ride_down()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame();
        g.JumpTo(2);
        g.Calm();
        var piece = g.Kitchen.Pieces.First(p => p.Floor is >= 1 and <= 3);
        int floor = piece.Floor;
        g.PutChef(piece.X - 6, floor);
        // The trick: pepper an enemy standing on a layer, then drop the layer with it aboard.
        var rider = g.PutEnemy(EnemyKind.Sausage, piece.X + 24, floor);
        rider.Dir = Dir.Left;
        rider.Stunned = 5;
        var stunned = g.PutEnemy(EnemyKind.Egg, Math.Min(200, piece.X + 70), floor);
        stunned.Stunned = 2;
        // Walk right over it, past the dizzy sausage: the last quarter drops it with the sausage aboard.
        for (int i = 0; i < 60 && piece.Floor == floor; i++) g.Step(new Controls(Dir.Right, false));
        for (int i = 0; i < 6; i++) g.Step(default);
        Assert.True(piece.Falling);
        Shot(w, c, "ride");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        for (int i = 0; i < 60 * 60 * 5 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        Shot(w, c, "game-over");
        g.Idle(60 * 4);
        if (g.State == GameState.EnterInitials) { g.Initials!.Finish(); g.Idle(5); }
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
