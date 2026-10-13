using AVABurgerT.Models;

namespace AVABurgerT.Engine;

/// <summary>The chef: on a floor, or on a ladder between two.</summary>
public sealed class Chef
{
    public double X { get; set; }
    /// <summary>Feet.</summary>
    public double Y { get; set; }
    public Dir Facing { get; set; } = Dir.Right;
    public Ladder? Ladder { get; set; }
    public bool Moving { get; set; }
    public double Stride { get; set; }
    public Vec Pos => new(X, Y);
    public int Floor => Ladder is null ? Field.FloorAt(Y) : -1;
}

public enum EnemyKind { Sausage, Egg, Pickle }

public sealed class Enemy
{
    public required EnemyKind Kind { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public Dir Dir { get; set; } = Dir.Left;
    public Ladder? Ladder { get; set; }
    /// <summary>Peppered: frozen and harmless, seconds left.</summary>
    public double Stunned { get; set; }
    /// <summary>Riding a dropped layer down.</summary>
    public Piece? Riding { get; set; }
    /// <summary>Gone (squashed or ridden down); back after a while.</summary>
    public double Away { get; set; }
    /// <summary>Waiting to walk on at the start.</summary>
    public double Delay { get; set; }
    public double Think { get; set; }
    public bool Active => Away <= 0 && Delay <= 0;
    public Vec Pos => new(X, Y);
    public int Floor => Ladder is null ? Field.FloorAt(Y) : -1;
}

/// <summary>A shake of pepper in the air.</summary>
public sealed class Pepper
{
    public required Vec Pos { get; init; }
    public double Age { get; set; }
    public const double Life = 0.45;
}

public enum Treat { IceCream, Coffee, Fries }

public enum EffectKind { Score }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 1;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>The controls for one step: a direction to walk or climb, and the pepper.</summary>
public record struct Controls(Dir Move, bool Pepper);
