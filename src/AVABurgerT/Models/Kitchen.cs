namespace AVABurgerT.Models;

public enum Layer { BunTop, Lettuce, Cheese, Patty, Tomato, BunBottom }

/// <summary>A ladder between two floors (by index; Upper is the higher one, a smaller index).</summary>
public sealed record Ladder(double X, int Upper, int Lower);

/// <summary>
/// One layer of a burger, lying across a floor: four quarters that sag as the chef walks over them; when
/// all four have been walked, it drops to the floor below.
/// </summary>
public sealed class Piece
{
    public required Layer Layer { get; init; }
    public required int Column { get; init; }
    public double X => Field.Columns[Column];
    public double Y { get; set; }
    /// <summary>The floor it's lying on (−1 while falling, or once it's on the plate).</summary>
    public int Floor { get; set; }
    public bool[] Stepped { get; } = new bool[4];
    public bool Falling { get; set; }
    public double Vy { get; set; }
    /// <summary>Where it's falling to, and how many more floors to go after that (one for each rider).</summary>
    public double LandY { get; set; }
    public int ExtraFloors { get; set; }
    public bool OnPlate { get; set; }
}

/// <summary>
/// A level's kitchen: the ladders between each pair of floors and where each burger's layers start.
/// Laid out from the level number, so the same level always looks the same; the layers of a burger
/// always start in order, top bun highest.
/// </summary>
public sealed class Kitchen
{
    public List<Ladder> Ladders { get; } = [];
    public List<Piece> Pieces { get; } = [];
    public static readonly Vec ChefStart = new(112, Field.Floors[^1]);

    public static Kitchen Build(int level)
    {
        var rng = new Random(level * 7919 + 17);
        var k = new Kitchen();
        // Ladders: the gaps alternate between the edges-and-middle and the two inner gaps, so every floor
        // is joined to the next; the pattern flips from level to level.
        for (int gap = 0; gap < Field.Floors.Length - 1; gap++)
        {
            bool outer = (gap + level) % 2 == 0;
            double[] xs = outer ? [12, 112, 212] : [64, 160];
            foreach (var x in xs) k.Ladders.Add(new Ladder(x, gap, gap + 1));
        }
        // A burger in each column: four layers on four different floors, in order.
        Layer[][] recipes =
        [
            [Layer.BunTop, Layer.Lettuce, Layer.Patty, Layer.BunBottom],
            [Layer.BunTop, Layer.Cheese, Layer.Patty, Layer.BunBottom],
            [Layer.BunTop, Layer.Tomato, Layer.Patty, Layer.BunBottom],
            [Layer.BunTop, Layer.Patty, Layer.Lettuce, Layer.BunBottom],
        ];
        for (int col = 0; col < 4; col++)
        {
            var floors = Enumerable.Range(0, Field.Floors.Length).OrderBy(_ => rng.Next()).Take(4).OrderBy(f => f).ToArray();
            var recipe = recipes[(col + level - 1) % recipes.Length];
            for (int i = 0; i < 4; i++)
                k.Pieces.Add(new Piece { Layer = recipe[i], Column = col, Floor = floors[i], Y = Field.Floors[floors[i]] });
        }
        return k;
    }

    public IEnumerable<Ladder> LaddersAt(int floor) => Ladders.Where(l => l.Upper == floor || l.Lower == floor);
}
