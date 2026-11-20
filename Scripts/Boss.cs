using Godot;

namespace Shmup;

/// <summary>
/// Waits in the arena at the end of a run: an armoured hull with two turrets
/// and a core. The core opens once both turrets are destroyed. Each part is an
/// enemy of its own; the boss moves them together and fires for them.
/// </summary>
public sealed class Boss
{
    private const float HoldX = Main.ScreenW - 72; // left edge of the hull once it has arrived
    private const float BaseY = Main.ScreenH / 2f - 48;
    private const int TurretHp = 40, CoreHp = 80;
    private const int TurretCooldown = 80, CoreCooldown = 90;
    private const int FanShots = 5;
    private const int ExplosionTicks = 90;

    // Centres of the parts, relative to the top-left corner of the hull sprite.
    private static readonly Vector2 HullAt = new(32, 48);
    private static readonly Vector2[] TurretAt = { new(6, 18), new(6, 78) };
    private static readonly Vector2 CoreAt = new(10, 48);

    private readonly BossPart _hull, _core;
    private readonly BossPart[] _turrets = new BossPart[2];
    private readonly int[] _turretCooldown = { TurretCooldown / 2, TurretCooldown };
    private readonly RandomNumberGenerator _rng = new() { Seed = 2001 };
    private Vector2 _origin = new(Main.ScreenW + 8, BaseY);
    private int _age;
    private int _coreCooldown = CoreCooldown;
    private int _explosionsLeft;

    public bool Defeated { get; private set; }

    public Boss(Main game)
    {
        for (var i = 0; i < _turrets.Length; i++)
            _turrets[i] = BossPart.Turret(TurretHp);
        _core = BossPart.Core(CoreHp);
        _hull = BossPart.Hull();
        Place();
        // The hull comes last: a shot that touches a part and the hull at once hits the part.
        foreach (var turret in _turrets) game.SpawnEnemy(turret);
        game.SpawnEnemy(_core);
        game.SpawnEnemy(_hull);
    }

    public void Step(Main game)
    {
        if (Defeated)
        {
            ExplodeHull(game);
            return;
        }
        // The core was removed in the last tick, so it must not be placed again.
        if (_core.Dead)
        {
            Defeated = true;
            _explosionsLeft = ExplosionTicks;
            return;
        }

        _age++;
        _origin = new Vector2(Mathf.Max(HoldX, _origin.X - 0.6f), BaseY + Mathf.Sin(_age * 0.015f) * 26);
        Place();
        if (_origin.X > HoldX) return;

        var target = game.Player.Position + new Vector2(8, 4);
        var turretsLeft = 0;
        for (var i = 0; i < _turrets.Length; i++)
        {
            var turret = _turrets[i];
            if (turret.Dead) continue;
            turretsLeft++;
            if (--_turretCooldown[i] > 0) continue;
            _turretCooldown[i] = TurretCooldown;
            game.SpawnBullet(Bullet.Aimed(turret.Rect.GetCenter() - new Vector2(10, 1), target, 1.6f));
        }

        if (turretsLeft == 0 && _core.Armored)
            _core.Open();

        if (!_core.Armored && --_coreCooldown <= 0)
        {
            _coreCooldown = CoreCooldown;
            var from = _core.Rect.GetCenter() - new Vector2(8, 1);
            var aim = (target - from).Angle();
            for (var i = 0; i < FanShots; i++)
            {
                var angle = aim + (i - FanShots / 2) * 0.25f;
                game.SpawnBullet(Bullet.Aimed(from, from + Vector2.FromAngle(angle), 1.4f));
            }
        }
    }

    private void Place()
    {
        _hull.Centre(_origin + HullAt);
        _core.Centre(_origin + CoreAt);
        for (var i = 0; i < _turrets.Length; i++)
            if (!_turrets[i].Dead)
                _turrets[i].Centre(_origin + TurretAt[i]);
    }

    /// <summary>Large explosions spread over the hull, which vanishes with the last one.</summary>
    private void ExplodeHull(Main game)
    {
        if (_hull.Dead) return;
        if (--_explosionsLeft % 6 == 0)
        {
            var at = _origin + new Vector2(_rng.RandfRange(4, 60), _rng.RandfRange(4, 92));
            game.Explode(at, Sprites.ExplosionLarge);
            game.Audio.Play(Sounds.ExplosionLarge);
        }
        if (_explosionsLeft <= 0)
            _hull.Dead = true;
    }
}

/// <summary>Hull, turret or core of the boss. The boss moves it; it does not move on its own.</summary>
public partial class BossPart : Enemy
{
    private bool _warnWhenLow;
    private int _maxHp;

    public static BossPart Hull()
    {
        var hull = new BossPart { Armored = true, DiesOnRam = false, Points = 0 };
        hull.Init(Sprites.BossHull, new Vector2(48, 84), Vector2.Zero);
        return hull;
    }

    public static BossPart Turret(int hp)
    {
        var turret = new BossPart { Hp = hp, _maxHp = hp, _warnWhenLow = true, DiesOnRam = false, Points = 0, ZIndex = 1 };
        turret.Init(Sprites.BossTurret, new Vector2(16, 10), Vector2.Zero);
        return turret;
    }

    /// <summary>Starts closed and armoured.</summary>
    public static BossPart Core(int hp)
    {
        var core = new BossPart
        {
            Hp = hp, _maxHp = hp, _warnWhenLow = true, Armored = true, DiesOnRam = false, Points = 0, ZIndex = 1,
        };
        core.Init(Sprites.BossCoreClosed, new Vector2(14, 14), Vector2.Zero);
        return core;
    }

    public void Open()
    {
        Armored = false;
        Sprite.Texture = Sprites.BossCoreOpen;
    }

    public void Centre(Vector2 at) => Position = (at - Rect.Size / 2).Floor();

    /// <summary>Blinks red once less than a quarter of its energy is left.</summary>
    protected override Color Tint =>
        _warnWhenLow && Hp * 4 < _maxHp && Age % 8 < 4 ? new Color(1.8f, 0.5f, 0.5f) : Colors.White;
}
