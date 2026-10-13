using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVABurgerT.Engine;
using AVABurgerT.Models;

namespace AVABurgerT.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVABurgerT/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Cyan = Brush(0xFF4FD7FF);
    private static readonly IBrush Dim = Brush(0xFF8A8AA6);
    private const uint FloorBlue = 0xFF2A5AE8, FloorLight = 0xFF8AB8FF, LadderColour = 0xFF8FE4FF;
    private const uint Bun = 0xFFE0A050, BunDark = 0xFFB8783A;

    /// <summary>Game pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, FieldTop + y * Scale);
    private static Point P(Vec v) => P(v.X, v.Y);

    private static void Fill(DrawingContext dc, uint argb, double x, double y, double w, double h) =>
        dc.FillRectangle(Brush(argb), new Rect(FieldLeft + x * Scale, FieldTop + y * Scale, w * Scale, h * Scale));

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        using (dc.PushClip(new Rect(P(0, 0), P(Field.Width, Field.Height))))
        {
            DrawKitchen(dc);
            switch (g.State)
            {
                case GameState.Title:
                    DrawTitle(dc);
                    break;
                case GameState.HighScores:
                    DrawHighScores(dc);
                    break;
                default:
                    DrawPlay(dc);
                    break;
            }
        }
        DrawPanels(dc);
    }

    // ----- the kitchen --------------------------------------------------------------------------

    private void DrawKitchen(DrawingContext dc)
    {
        var g = Engine;
        foreach (var l in g.Kitchen.Ladders)
        {
            double top = Field.Floors[l.Upper], bottom = Field.Floors[l.Lower];
            Fill(dc, LadderColour, l.X - 5, top, 1, bottom - top);
            Fill(dc, LadderColour, l.X + 4, top, 1, bottom - top);
            for (double y = top + 3; y < bottom; y += 4) Fill(dc, LadderColour, l.X - 5, y, 10, 1);
        }
        foreach (double y in Field.Floors)
        {
            Fill(dc, FloorBlue, Field.Left, y, Field.Right - Field.Left, 3);
            Fill(dc, FloorLight, Field.Left, y, Field.Right - Field.Left, 1);
        }
        // The plates.
        for (int col = 0; col < 4; col++)
        {
            double x = Field.Columns[col];
            Fill(dc, 0xFF9A9AAA, x - 4, Field.PlateY, Field.PieceWidth + 8, 2);
            Fill(dc, 0xFFC8C8D4, x - 2, Field.PlateY + 2, Field.PieceWidth + 4, 2);
        }
        foreach (var p in g.Kitchen.Pieces) DrawPiece(dc, p);
    }

    /// <summary>A burger layer: each quarter drawn a little lower once it's been walked on.</summary>
    private static void DrawPiece(DrawingContext dc, Piece p)
    {
        for (int q = 0; q < 4; q++)
        {
            double x = p.X + q * 8, y = p.Y + (p.Stepped[q] ? 2 : 0);
            bool first = q == 0, last = q == 3;
            switch (p.Layer)
            {
                case Layer.BunTop:
                    Fill(dc, Bun, x + (first ? 2 : 0), y - 7, 8 - (first || last ? 2 : 0), 2);
                    Fill(dc, Bun, x, y - 5, 8, 4);
                    Fill(dc, BunDark, x, y - 2, 8, 1);
                    Fill(dc, 0xFFF4F4E0, x + 2 + q % 2 * 3, y - 6 + q % 2, 1, 1);
                    Fill(dc, 0xFFF4F4E0, x + 5 - q % 2 * 2, y - 4, 1, 1);
                    break;
                case Layer.BunBottom:
                    Fill(dc, BunDark, x, y - 5, 8, 1);
                    Fill(dc, Bun, x, y - 4, 8, 2);
                    Fill(dc, Bun, x + (first ? 1 : 0), y - 2, 8 - (first || last ? 1 : 0), 2);
                    break;
                case Layer.Lettuce:
                    Fill(dc, 0xFF3CD84A, x, y - 4, 8, 2);
                    for (int i = 0; i < 8; i += 3) Fill(dc, 0xFF1E8A2A, x + i, y - 2, 2, 1);
                    Fill(dc, 0xFF8AF07A, x + 1, y - 5, 2, 1);
                    Fill(dc, 0xFF8AF07A, x + 5, y - 5, 2, 1);
                    break;
                case Layer.Cheese:
                    Fill(dc, 0xFFFFD23A, x, y - 4, 8, 2);
                    if (q % 2 == 0) Fill(dc, 0xFFFFD23A, x + 3, y - 2, 2, 1);
                    break;
                case Layer.Patty:
                    Fill(dc, 0xFF8A4A1E, x, y - 5, 8, 4);
                    Fill(dc, 0xFF5A2A10, x + 1 + q % 2 * 3, y - 4, 2, 1);
                    Fill(dc, 0xFF5A2A10, x + 5 - q % 2 * 3, y - 2, 2, 1);
                    break;
                case Layer.Tomato:
                    Fill(dc, 0xFFE83A3A, x, y - 4, 8, 3);
                    Fill(dc, 0xFFFF9A8A, x + 2, y - 3, 1, 1);
                    Fill(dc, 0xFFFF9A8A, x + 5, y - 3, 1, 1);
                    break;
            }
        }
    }

    // ----- the play -----------------------------------------------------------------------------

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        if (g.TreatLeft > 0 && !(g.TreatLeft < 2 && (int)(_clock * 8) % 2 == 0))
            DrawSprite(dc, Sprites.TreatArt(g.TreatKind), GameEngine.TreatSpot.X - 8, GameEngine.TreatSpot.Y - 16);

        foreach (var e in g.Enemies)
        {
            if (!e.Active || e.X < -6 || e.X > Field.Width + 6) continue;
            var frames = e.Kind switch { EnemyKind.Sausage => Sprites.SausageWalk, EnemyKind.Egg => Sprites.EggWalk, _ => Sprites.PickleWalk };
            int frame = e.Stunned > 0 || e.Riding is not null ? 0 : (int)(_clock * 6 + e.X / 20) % 2;
            if (frame < 0) frame = -frame;
            DrawSprite(dc, frames[frame], e.X - 8, e.Y - 16, flip: e.Dir == Dir.Left);
            if (e.Stunned > 0) DrawSprite(dc, Sprites.Dizzy[(int)(_clock * 6) % 2], e.X - 8, e.Y - 21);
        }

        DrawChef(dc);
        foreach (var p in g.Peppers) DrawSprite(dc, Sprites.PepperCloud, p.Pos.X - 8, p.Pos.Y - 8);

        foreach (var fx in g.Effects)
            Text(dc, fx.Text, P(fx.Pos.X, fx.Pos.Y - fx.Age * 8), 28, Yellow, center: true);

        switch (g.State)
        {
            case GameState.Paused:
                Banner(dc, "PAUSED", "ESC TO QUIT, ANY OTHER KEY TO RESUME");
                break;
            case GameState.GameOver:
                Banner(dc, "GAME  OVER", null);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
            default:
                if (g.Phase == Phase.Ready) Banner(dc, $"LEVEL {g.Level}", g.State == GameState.Attract ? "DEMONSTRATION" : "PLAYER 1");
                else if (g.Phase == Phase.LevelClear) Banner(dc, "ORDER UP!", $"LEVEL {g.Level} DONE");
                else if (g.State == GameState.Attract) Text(dc, "DEMONSTRATION", P(Field.Width / 2.0, 20), 30, Red, center: true);
                break;
        }
    }

    private void Banner(DrawingContext dc, string title, string? sub)
    {
        Fill(dc, 0xE0000000, 30, 92, 164, sub is null ? 24 : 36);
        Text(dc, title, P(Field.Width / 2.0, 104), 46, Yellow, center: true);
        if (sub is not null) Text(dc, sub, P(Field.Width / 2.0, 120), 26, White, center: true);
    }

    private void DrawChef(DrawingContext dc)
    {
        var g = Engine;
        var c = g.Chef;
        if (g.Phase == Phase.Dying)
        {
            if (g.PhaseTime < 0.8) DrawSprite(dc, Sprites.ChefWalk[0], c.X - 8, c.Y - 16, angle: g.PhaseTime * 900);
            else DrawSprite(dc, Sprites.ChefDown, c.X - 8, c.Y - 16);
            return;
        }
        if (c.Ladder is not null || c.Facing is Dir.Up)
            DrawSprite(dc, Sprites.ChefClimb[(int)(c.Stride / 5) % 2], c.X - 8, c.Y - 16);
        else
            DrawSprite(dc, Sprites.ChefWalk[c.Moving ? (int)(c.Stride / 4) % 2 : 0], c.X - 8, c.Y - 16, flip: c.Facing == Dir.Left);
    }

    /// <summary>Draws a sprite with its top-left at a game point, optionally mirrored or turned.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y, bool flip = false, double angle = 0, double px = 1)
    {
        var centre = P(x + s.Width * px / 2.0, y + s.Height * px / 2.0);
        using var pushed = angle != 0
            ? (IDisposable?)dc.PushTransform(Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(angle * Math.PI / 180) * Matrix.CreateTranslation(centre.X, centre.Y))
            : null;
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[flip ? s.Width - 1 - c : c];
                if (ch == '.') continue;
                var a = P(x + c * px, y + r * px);
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(a.X, a.Y, Scale * px + 0.3, Scale * px + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
