using Godot;

namespace Shmup;

public partial class Player : Node2D
{
    public const float Speed = 1.7f;
    public const int ChargeMin = 25; // frames of charge before a beam is fired
    public const int ChargeMax = 90;
    public const int InvulnFrames = 120;

    /// <summary>Movement bounds; the sprite is drawn larger and centred on this area.</summary>
    public static readonly Vector2 Size = new(16, 8);

    private static readonly Vector2 SpawnPosition = new(30, Main.ScreenH / 2f - 4);

    public int Charge { get; private set; }
    public bool Invulnerable => _invuln > 0;

    /// <summary>Small hitbox inside the hull – feels fairer than the full sprite.</summary>
    public Rect2 Hitbox => new(Position + new Vector2(3, 2), new Vector2(9, 4));

    private Sprite2D _sprite = null!;
    private CpuParticles2D _chargeFx = null!;
    private int _invuln;
    private bool _fireHeld;

    public override void _Ready()
    {
        _sprite = Sprites.Around(Sprites.Player, Size);
        _sprite.ZIndex = 1; // own shots start inside the hull and emerge at the nose
        AddChild(_sprite);

        // Engine exhaust: emits in world space so it leaves a trail.
        AddChild(new CpuParticles2D
        {
            Position = new Vector2(-8, 4),
            Amount = 10,
            Lifetime = 0.15,
            LocalCoords = false,
            Direction = Vector2.Left,
            Spread = 8,
            Gravity = Vector2.Zero,
            InitialVelocityMin = 50,
            InitialVelocityMax = 80,
            ColorRamp = Effects.Fade(Palette.Flame),
        });

        // Charge effect at the nose: particles are pulled inwards.
        _chargeFx = new CpuParticles2D
        {
            Position = new Vector2(23, 4),
            Emitting = false,
            Amount = 16,
            Lifetime = 0.25,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.SphereSurface,
            EmissionSphereRadius = 7,
            Gravity = Vector2.Zero,
            RadialAccelMin = -120,
            RadialAccelMax = -120,
            ColorRamp = Effects.Fade(Palette.Beam),
        };
        AddChild(_chargeFx);

        Respawn();
    }

    public void Respawn()
    {
        Position = SpawnPosition;
        _invuln = InvulnFrames;
        Charge = 0;
        _chargeFx.Emitting = false;
        Visible = true;
    }

    public void Step(Main game)
    {
        var dir = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var p = Position + dir * Speed;
        Position = new Vector2(
            Mathf.Clamp(p.X, 0, Main.ScreenW - Size.X),
            Mathf.Clamp(p.Y, 10, Main.ScreenH - 14 - Size.Y));

        if (_invuln > 0) _invuln--;
        _sprite.Visible = _invuln == 0 || (_invuln / 4) % 2 == 0;

        // R-Type style: tap = shot, hold = charge, release = beam.
        var fire = Input.IsActionPressed("fire");
        if (fire && !_fireHeld)
        {
            game.SpawnBullet(Bullet.Shot(Position + new Vector2(14, 3)));
            game.Audio.Play(Sounds.Shot);
            Charge = 0;
        }
        else if (fire && _fireHeld)
        {
            if (Charge < ChargeMax) Charge++;
            _chargeFx.Emitting = Charge > ChargeMin;
        }
        else if (!fire && _fireHeld)
        {
            if (Charge >= ChargeMin)
            {
                var level = 1 + (Charge - ChargeMin) * 3 / (ChargeMax - ChargeMin + 1); // 1..3
                game.SpawnBullet(Bullet.Beam(Position + new Vector2(14, 4), level));
                game.Audio.Play(Sounds.Beam[level - 1]);
            }
            Charge = 0;
            _chargeFx.Emitting = false;
        }
        _fireHeld = fire;
        game.Audio.Charge(Charge > ChargeMin ? (Charge - ChargeMin) / (float)(ChargeMax - ChargeMin) : null);
    }
}
