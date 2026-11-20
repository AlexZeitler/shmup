using Godot;

namespace Shmup;

/// <summary>Base class: damage, hit flash, leaving the screen.</summary>
public partial class Enemy : Node2D
{
    public int Hp = 1;
    public int Points = 100;
    public bool DiesOnRam = true;
    public bool Dead;

    protected int Age;
    protected Sprite2D Sprite = null!;
    private int _flash;

    public Rect2 Rect => new(Position, _size);

    private Vector2 _size;

    /// <summary>The hitbox keeps the given size, the sprite is drawn larger and centred on it.</summary>
    protected void Init(Texture2D texture, Vector2 hitbox, Vector2 at)
    {
        _size = hitbox;
        Sprite = Sprites.Around(texture, hitbox);
        AddChild(Sprite);
        Position = at;
    }

    public void Hit(int damage)
    {
        Hp -= damage;
        _flash = 4;
        if (Hp <= 0) Dead = true;
    }

    public void Step(Main game)
    {
        Age++;
        if (_flash > 0) _flash--;
        Sprite.Modulate = _flash > 0 ? new Color(3, 3, 3) : Colors.White;
        Move(game);
        if (Position.X < -30) Dead = true;
    }

    protected virtual void Move(Main game) { }
}

/// <summary>Flies along a sine wave, arrives in formations.</summary>
public partial class Drone : Enemy
{
    private float _baseY;
    private float _phase;

    public static Drone Create(Vector2 at, float phase)
    {
        var d = new Drone { _baseY = at.Y, _phase = phase, Points = 100 };
        d.Init(Sprites.Drone, new Vector2(8, 6), at);
        return d;
    }

    protected override void Move(Main game) =>
        Position = new Vector2(Position.X - 1.2f, _baseY + Mathf.Sin(Age * 0.07f + _phase) * 22);
}

/// <summary>Fast, slowly homes in on the player's height.</summary>
public partial class Chaser : Enemy
{
    public static Chaser Create(Vector2 at)
    {
        var c = new Chaser { Points = 150 };
        c.Init(Sprites.Chaser, new Vector2(8, 6), at);
        return c;
    }

    protected override void Move(Main game)
    {
        var y = Position.Y;
        var dy = game.Player.Position.Y - y;
        if (Mathf.Abs(dy) > 1) y += Mathf.Sign(dy) * 0.45f;
        Position = new Vector2(Position.X - 1.7f, y);
    }
}

/// <summary>Slows down, hovers and fires aimed shots at the player.</summary>
public partial class Heavy : Enemy
{
    private float _baseY;
    private int _fireCooldown = 60;

    public static Heavy Create(Vector2 at)
    {
        var h = new Heavy { _baseY = at.Y, Hp = 8, Points = 500, DiesOnRam = false };
        h.Init(Sprites.Heavy, new Vector2(14, 8), at);
        return h;
    }

    protected override void Move(Main game)
    {
        var x = Position.X - (Position.X > Main.ScreenW - 70 ? 0.8f : 0.15f);
        Position = new Vector2(x, _baseY + Mathf.Sin(Age * 0.02f) * 12);

        if (--_fireCooldown <= 0 && Position.X < Main.ScreenW - 10)
        {
            _fireCooldown = 70;
            game.SpawnBullet(Bullet.Aimed(
                Position + new Vector2(3, 4),
                game.Player.Position + new Vector2(8, 4),
                1.6f));
        }
    }
}
