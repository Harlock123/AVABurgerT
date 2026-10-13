namespace AVABurgerT.Models;

/// <summary>A 2D point or vector (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

public enum Dir { None, Up, Down, Left, Right }

public static class Dirs
{
    public static int Dx(this Dir d) => d switch { Dir.Left => -1, Dir.Right => 1, _ => 0 };
    public static int Dy(this Dir d) => d switch { Dir.Up => -1, Dir.Down => 1, _ => 0 };
    public static bool Horizontal(this Dir d) => d is Dir.Left or Dir.Right;
}

/// <summary>
/// The screen in game pixels: 224 by 256, portrait like the arcade's. Six floors, 32 pixels apart; the
/// plates below the lowest; four burgers to build, one in each column between the ladders.
/// </summary>
public static class Field
{
    public const int Width = 224;
    public const int Height = 256;
    public const double Step = 1.0 / 60;

    /// <summary>Where feet stand on each floor, top to bottom.</summary>
    public static readonly double[] Floors = [56, 88, 120, 152, 184, 216];
    public const double PlateY = 250;
    public const double Left = 4, Right = 220;

    /// <summary>The left edge of each burger column; a layer is 32 pixels wide.</summary>
    public static readonly double[] Columns = [24, 72, 120, 168];
    public const double PieceWidth = 32;
    /// <summary>The places ladders can stand: the edges and the gaps between the burgers.</summary>
    public static readonly double[] LadderSpots = [12, 64, 112, 160, 212];

    public static int FloorAt(double y) => Array.FindIndex(Floors, f => Math.Abs(f - y) < 0.5);
}
