using System.Collections.Generic;
using Godot;

namespace Shmup;

public partial class Bullet : Node2D
{
    public Vector2 Velocity;
    public Vector2 Size;
    public int Damage = 1;
    public bool Pierce;   // beam passes through enemies
    public bool IsEnemy;
    public bool Dead;
    public readonly HashSet<ulong> AlreadyHit = new();

    private Texture2D _texture = null!;

    public Rect2 Rect => new(Position, Size);

    public static Bullet Shot(Vector2 at) => new()
    {
        Position = at,
        Velocity = new Vector2(5, 0),
        Size = new Vector2(6, 2),
        _texture = Sprites.Shot,
    };

    public static Bullet Beam(Vector2 nose, int level)
    {
        var h = 3 + level * 2.5f;
        return new Bullet
        {
            Position = nose - new Vector2(0, h / 2),
            Velocity = new Vector2(6, 0),
            Size = new Vector2(14 + level * 6, h),
            Damage = 2 * level,
            Pierce = true,
            _texture = Sprites.Beam[level - 1],
        };
    }

    public static Bullet Aimed(Vector2 from, Vector2 target, float speed) => new()
    {
        Position = from,
        Velocity = (target - from).Normalized() * speed,
        Size = new Vector2(3, 3),
        IsEnemy = true,
        ZIndex = 2, // above the ships, which are drawn above the player's own shots
        _texture = Sprites.EnemyShot,
    };

    public void Step()
    {
        Position += Velocity;
        if (Position.X > Main.ScreenW + 20 || Position.X < -40 ||
            Position.Y < -10 || Position.Y > Main.ScreenH + 10)
            Dead = true;
    }

    public override void _Ready() => AddChild(Sprites.Around(_texture, Size));
}
