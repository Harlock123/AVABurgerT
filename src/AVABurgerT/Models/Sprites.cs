namespace AVABurgerT.Models;

/// <summary>
/// The game's pixel art, this game's own drawings. The hero is drawn row by row; the bigger and
/// rounder things are painted from ellipses and boxes into a pixel grid, which keeps them easy to read
/// and to change. Each letter is a colour (see <see cref="Colours"/>); '.' is clear.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>A pixel grid to paint shapes into.</summary>
    private sealed class Canvas(int width, int height)
    {
        private readonly char[,] _px = Fill(width, height);

        private static char[,] Fill(int w, int h)
        {
            var g = new char[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++) g[x, y] = '.';
            return g;
        }

        public Canvas Ellipse(double cx, double cy, double rx, double ry, char c)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    if (dx * dx + dy * dy <= 1) _px[x, y] = c;
                }
            return this;
        }

        public Canvas Box(int x0, int y0, int x1, int y1, char c)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++) _px[x, y] = c;
            return this;
        }

        public Canvas Dot(int x, int y, char c) => Box(x, y, x, y, c);

        public Sprite Done()
        {
            var rows = new string[height];
            for (int y = 0; y < height; y++)
            {
                var chars = new char[width];
                for (int x = 0; x < width; x++) chars[x] = _px[x, y];
                rows[y] = new string(chars);
            }
            return new Sprite(rows);
        }
    }

    public static readonly IReadOnlyDictionary<char, uint> Colours = new Dictionary<char, uint>
    {
        ['w'] = 0xFFF4F4F4, // white
        ['W'] = 0xFFC8C8D4, // shaded white
        ['k'] = 0xFF14141C, // black
        ['s'] = 0xFFFFC08A, // skin
        ['r'] = 0xFFE83A3A, // red
        ['R'] = 0xFF9A1E1E, // dark red
        ['y'] = 0xFFFFE14A, // yellow
        ['o'] = 0xFFFF8A1E, // orange
        ['g'] = 0xFF3CD84A, // green
        ['G'] = 0xFF1E8A2A, // dark green
        ['b'] = 0xFF8A5A2A, // brown
        ['B'] = 0xFF5A3416, // dark brown
        ['u'] = 0xFF2A6AFF, // blue
        ['p'] = 0xFFFF7AC8, // pink
        ['c'] = 0xFF8FE4FF, // pale blue
        ['m'] = 0xFF9A9AAA, // grey
    };

    // ----- the chef (facing right; mirrored for left) --------------------------------------------

    private static Canvas Chef(bool step) => new Canvas(16, 16)
        .Ellipse(8, 2.5, 4.5, 2.5, 'w').Box(5, 3, 11, 4, 'w')                 // the toque
        .Ellipse(8, 7, 3.5, 3, 's').Dot(10, 6, 'k').Box(9, 8, 11, 8, 'B')   // face, eye, moustache
        .Box(4, 10, 12, 13, 'w').Box(7, 10, 8, 13, 'W').Box(12, 10, 14, 11, 's')
        .Box(step ? 4 : 5, 14, step ? 6 : 7, 15, 'u').Box(step ? 10 : 9, 14, step ? 12 : 11, 15, 'u');

    public static readonly Sprite[] ChefWalk = [Chef(false).Done(), Chef(true).Done()];

    /// <summary>Climbing, from behind.</summary>
    public static readonly Sprite[] ChefClimb =
    [
        new Canvas(16, 16).Ellipse(8, 2.5, 4.5, 2.5, 'w').Box(5, 3, 11, 4, 'w').Ellipse(8, 7, 3.5, 3, 'B')
            .Box(4, 10, 12, 13, 'w').Box(2, 8, 3, 10, 's').Box(13, 11, 14, 12, 's').Box(5, 14, 6, 15, 'u').Box(10, 13, 11, 15, 'u').Done(),
        new Canvas(16, 16).Ellipse(8, 2.5, 4.5, 2.5, 'w').Box(5, 3, 11, 4, 'w').Ellipse(8, 7, 3.5, 3, 'B')
            .Box(4, 10, 12, 13, 'w').Box(13, 8, 14, 10, 's').Box(2, 11, 3, 12, 's').Box(5, 13, 6, 15, 'u').Box(10, 14, 11, 15, 'u').Done(),
    ];

    /// <summary>Caught: the chef flat on his back, hat off.</summary>
    public static readonly Sprite ChefDown = new Canvas(16, 16).Box(1, 11, 11, 14, 'w').Ellipse(13, 12, 2.5, 2.5, 's')
        .Box(0, 14, 2, 15, 'u').Box(4, 14, 6, 15, 'u').Ellipse(13, 6, 3, 1.8, 'w').Done();

    // ----- the enemies ----------------------------------------------------------------------------

    private static Canvas Sausage(bool step) => new Canvas(16, 16)
        .Ellipse(8, 7, 4.5, 6.5, 'r').Ellipse(8, 7, 3, 5, 'o').Ellipse(8, 7, 4.5, 6.5, 'r').Box(6, 2, 10, 12, 'r')
        .Box(5, 5, 7, 7, 'w').Box(9, 5, 11, 7, 'w').Dot(6, 6, 'k').Dot(10, 6, 'k').Box(6, 9, 10, 9, 'k')
        .Box(step ? 4 : 5, 13, step ? 5 : 6, 15, 'y').Box(step ? 11 : 10, 13, step ? 12 : 11, 15, 'y');

    private static Canvas Egg(bool step) => new Canvas(16, 16)
        .Ellipse(8, 7.5, 6, 6.5, 'w').Ellipse(8, 8, 3.5, 3.5, 'y')
        .Box(5, 6, 6, 7, 'k').Box(10, 6, 11, 7, 'k').Box(7, 10, 9, 10, 'o')
        .Box(step ? 4 : 5, 14, step ? 5 : 6, 15, 'o').Box(step ? 11 : 10, 14, step ? 12 : 11, 15, 'o');

    private static Canvas Pickle(bool step) => new Canvas(16, 16)
        .Ellipse(8, 7, 4, 7, 'g').Dot(6, 3, 'G').Dot(10, 5, 'G').Dot(6, 10, 'G').Dot(9, 12, 'G')
        .Box(5, 6, 7, 7, 'w').Box(9, 6, 11, 7, 'w').Dot(6, 7, 'k').Dot(10, 7, 'k').Box(7, 9, 9, 9, 'k')
        .Box(step ? 4 : 5, 14, step ? 5 : 6, 15, 'G').Box(step ? 11 : 10, 14, step ? 12 : 11, 15, 'G');

    public static readonly Sprite[] SausageWalk = [Sausage(false).Done(), Sausage(true).Done()];
    public static readonly Sprite[] EggWalk = [Egg(false).Done(), Egg(true).Done()];
    public static readonly Sprite[] PickleWalk = [Pickle(false).Done(), Pickle(true).Done()];

    /// <summary>Peppered: little stars spinning over the head.</summary>
    public static readonly Sprite[] Dizzy =
    [
        new Canvas(16, 6).Dot(2, 2, 'y').Dot(1, 2, 'y').Dot(3, 2, 'y').Dot(2, 1, 'y').Dot(2, 3, 'y').Dot(9, 3, 'w').Dot(13, 2, 'y').Dot(12, 2, 'y').Dot(14, 2, 'y').Dot(13, 1, 'y').Dot(13, 3, 'y').Done(),
        new Canvas(16, 6).Dot(6, 2, 'y').Dot(5, 2, 'y').Dot(7, 2, 'y').Dot(6, 1, 'y').Dot(6, 3, 'y').Dot(2, 3, 'w').Dot(11, 3, 'w').Done(),
    ];

    public static readonly Sprite PepperCloud = new Canvas(16, 16).Dot(3, 4, 'w').Dot(7, 2, 'w').Dot(11, 5, 'w').Dot(5, 8, 'm').Dot(9, 9, 'w')
        .Dot(13, 9, 'm').Dot(2, 11, 'w').Dot(7, 13, 'm').Dot(11, 12, 'w').Dot(14, 3, 'm').Dot(4, 14, 'w').Done();

    // ----- treats ---------------------------------------------------------------------------------

    public static readonly Sprite IceCream = new Canvas(16, 16).Ellipse(8, 5, 5, 4.5, 'p').Ellipse(6, 4, 1.5, 1.2, 'w')
        .Box(4, 8, 12, 9, 'o').Box(5, 10, 11, 11, 'o').Box(6, 12, 10, 13, 'o').Box(7, 14, 9, 15, 'o').Done();
    public static readonly Sprite Coffee = new Canvas(16, 16).Box(3, 5, 11, 14, 'w').Box(4, 6, 10, 7, 'B').Ellipse(12.5, 9.5, 2.5, 2.5, 'w')
        .Ellipse(12.5, 9.5, 1.2, 1.2, '.').Box(5, 1, 5, 3, 'm').Box(8, 0, 8, 3, 'm').Done();
    public static readonly Sprite Fries = new Canvas(16, 16).Box(4, 1, 5, 8, 'y').Box(7, 0, 8, 8, 'y').Box(10, 2, 11, 8, 'y').Box(6, 3, 6, 8, 'y')
        .Box(3, 8, 12, 15, 'r').Box(5, 10, 10, 11, 'w').Done();

    public static Sprite TreatArt(Engine.Treat t) => t switch { Engine.Treat.IceCream => IceCream, Engine.Treat.Coffee => Coffee, _ => Fries };
}
