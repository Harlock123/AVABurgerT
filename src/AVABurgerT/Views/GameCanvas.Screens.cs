using Avalonia;
using Avalonia.Media;
using AVABurgerT.Engine;
using AVABurgerT.Models;

namespace AVABurgerT.Views;

public sealed partial class GameCanvas
{
    private const double RightPanel = FieldLeft + Field.Width * Scale;

    /// <summary>The panels either side: the scores and spare chefs on the left; the level, the pepper and the keys on the right.</summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        double lx = FieldLeft / 2, rx = RightPanel + (ScreenWidth - RightPanel) / 2;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;

        Text(dc, "1UP", new Point(lx, 60), 30, Red, center: true);
        Text(dc, (playing ? g.Score : 0).ToString(), new Point(lx, 92), 34, White, center: true);
        Text(dc, "HI-SCORE", new Point(lx, 160), 30, Red, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString(), new Point(lx, 192), 34, White, center: true);

        if (playing)
        {
            int spare = g.Lives - (g.Phase is Phase.Dying ? 0 : 1);
            for (int i = 0; i < Math.Min(spare, 5); i++) DrawPanelSprite(dc, Sprites.ChefWalk[0], lx - 60 + i * 30, 690);
            Text(dc, "LEVEL", new Point(rx, 60), 30, Red, center: true);
            Text(dc, g.Level.ToString(), new Point(rx, 92), 34, White, center: true);
            Text(dc, "PEPPER", new Point(rx, 160), 30, Red, center: true);
            Text(dc, g.PepperLeft.ToString(), new Point(rx, 192), 34, Yellow, center: true);
            Text(dc, "BURGERS", new Point(rx, 260), 30, Red, center: true);
            Text(dc, $"{Enumerable.Range(0, 4).Count(c => g.Kitchen.Pieces.Count(p => p.Column == c && p.OnPlate) == 4)} / 4", new Point(rx, 292), 34, White, center: true);
        }
        Text(dc, "AVABURGERT", new Point(rx, 540), 24, Dim, center: true);
        Text(dc, "Arrows or WASD move", new Point(rx, 570), 20, Dim, center: true);
        Text(dc, "Space shakes pepper", new Point(rx, 590), 20, Dim, center: true);
        Text(dc, "Enter starts", new Point(rx, 610), 20, Dim, center: true);
        Text(dc, "M mute  P pause", new Point(rx, 640), 20, Dim, center: true);
        Text(dc, "Ctrl+S screenshot", new Point(rx, 660), 20, Dim, center: true);
    }

    private static void DrawPanelSprite(DrawingContext dc, Sprites.Sprite s, double left, double top)
    {
        const double px = 1.6;
        for (int r = 0; r < s.Height; r++)
            for (int c = 0; c < s.Width; c++)
            {
                char ch = s.Rows[r][c];
                if (ch == '.') continue;
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(left + c * px, top + r * px, px + 0.2, px + 0.2));
            }
    }

    private static void Panel(DrawingContext dc, double top, double bottom) =>
        dc.FillRectangle(Brush(0xE0000010), new Rect(P(10, top), P(Field.Width - 10, bottom)));

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 4, 252);
        Text(dc, "AVA BURGER TIME", P(cx, 22), 70, Yellow, center: true);
        Text(dc, "AFTER THE 1982 ARCADE CLASSIC", P(cx, 37), 26, Dim, center: true);
        DrawSprite(dc, Sprites.ChefWalk[(int)(_clock * 4) % 2], cx - 40, 46);
        DrawSprite(dc, Sprites.SausageWalk[(int)(_clock * 4) % 2], cx - 16, 46, flip: true);
        DrawSprite(dc, Sprites.EggWalk[(int)(_clock * 4) % 2], cx + 2, 46, flip: true);
        DrawSprite(dc, Sprites.PickleWalk[(int)(_clock * 4) % 2], cx + 20, 46, flip: true);
        Text(dc, "WALK OVER THE LAYERS TO DROP THEM", P(cx, 72), 28, White, center: true);
        Text(dc, "AND BUILD FOUR BURGERS ON THE PLATES", P(cx, 82), 28, White, center: true);
        Row(98, "A LAYER DROPPED (OR KNOCKED DOWN)", "50");
        Row(110, "AN ENEMY SQUASHED UNDER ONE", "500");
        Row(122, "ENEMIES RIDING A LAYER DOWN", "500-8000");
        Row(134, "A BURGER FINISHED", "500");
        Row(146, "A TREAT (AND A SHAKE OF PEPPER)", "500+");
        Text(dc, "PEPPER STUNS AN ENEMY: SPACE", P(cx, 164), 24, Cyan, center: true);
        Text(dc, "BONUS CHEF AT 10000, THEN EVERY 20000", P(cx, 176), 24, Cyan, center: true);
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO START", P(cx, 214), 32, White, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(cx, 240), 22, Dim, center: true);

        void Row(double y, string name, string points)
        {
            Text(dc, name, P(22, y), 25, White);
            Text(dc, points, P(212, y), 25, White, right: true);
        }
    }

    private void DrawHighScores(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 10, 236);
        Text(dc, "TOP CHEFS", P(cx, 30), 60, Yellow, center: true);
        Text(dc, "RANK    SCORE   NAME  LEVEL", P(cx, 50), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Red : White;
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}  {e.Stage,4}", P(cx, 66 + i * 14), 32, brush, center: true);
        }
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO PLAY", P(cx, 220), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        dc.FillRectangle(Brush(0xE8000000), new Rect(P(16, 92), P(208, 170)));
        double cx = Field.Width / 2.0;
        Text(dc, "A NEW HIGH SCORE!", P(cx, 102), 44, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(cx, 116), 40, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(cx - 12 + i * 12, 138);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 70, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 16, at.Y + 32, 32, 5));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(cx, 162), 22, Dim, center: true);
    }
}
