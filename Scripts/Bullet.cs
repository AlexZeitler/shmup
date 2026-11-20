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

    public Rect2 Rect => new(Position, Size);

    public static Bullet Shot(Vector2 at) => new()
    {
        Position = at,
        Velocity = new Vector2(5, 0),
        Size = new Vector2(6, 2),
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
        };
    }

    public static Bullet Aimed(Vector2 from, Vector2 target, float speed) => new()
    {
        Position = from,
        Velocity = (target - from).Normalized() * speed,
        Size = new Vector2(3, 3),
        IsEnemy = true,
    };

    public void Step()
    {
        Position += Velocity;
        if (Position.X > Main.ScreenW + 20 || Position.X < -40 ||
            Position.Y < -10 || Position.Y > Main.ScreenH + 10)
            Dead = true;
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        if (IsEnemy)
        {
            DrawRect(r, Palette.EnemyShot);
            DrawRect(new Rect2(1, 1, 1, 1), Palette.White);
        }
        else if (Pierce)
        {
            DrawRect(r, Palette.Beam);
            DrawRect(new Rect2(2, Size.Y / 2 - 1, Size.X - 2, 2), Palette.White);
        }
        else
        {
            DrawRect(r, Palette.Shot);
        }
    }
}
