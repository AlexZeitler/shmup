using System.Collections.Generic;
using Godot;

namespace Shmup;

/// <summary>
/// Game state and fixed 60 Hz tick. All entities are advanced here in
/// _PhysicsProcess in a well-defined order.
/// </summary>
public partial class Main : Node2D
{
    public const int ScreenW = 320;
    public const int ScreenH = 180;
    public const int StartingLives = 3;

    public enum State { Play, Over }

    public RandomNumberGenerator Rng { get; } = new() { Seed = 1985 };
    public Player Player { get; private set; } = null!;
    public int Score { get; private set; }
    public int HiScore { get; private set; }
    public int Lives { get; private set; }
    public int Tick { get; private set; }
    public State CurrentState { get; private set; }
    public bool Paused { get; private set; }

    private readonly List<Enemy> _enemies = new();
    private readonly List<Bullet> _bullets = new();
    private readonly WaveSpawner _spawner = new();
    private Node2D _world = null!;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(Palette.Background);
        AddChild(new Starfield { Name = "Starfield" });
        _world = new Node2D { Name = "World" };
        AddChild(_world);
        AddChild(new Hud { Name = "Hud" });
        Reset();
    }

    private void Reset()
    {
        foreach (var child in _world.GetChildren())
            child.QueueFree();
        _enemies.Clear();
        _bullets.Clear();

        Player = new Player { Name = "Player" };
        _world.AddChild(Player);

        Lives = StartingLives;
        Score = 0;
        Tick = 0;
        _spawner.Reset();
        CurrentState = State.Play;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed("quit"))
        {
            GetTree().Quit();
            return;
        }
        if (Input.IsActionJustPressed("fullscreen"))
            ToggleFullscreen();
        if (Input.IsActionJustPressed("pause") && CurrentState == State.Play)
            Paused = !Paused;
        if (Paused)
            return;

        if (CurrentState == State.Over)
        {
            if (Input.IsActionJustPressed("restart") || Input.IsActionJustPressed("fire"))
                Reset();
            return;
        }

        Tick++;
        Player.Step(this);
        _spawner.Step(this);
        foreach (var e in _enemies) e.Step(this);
        foreach (var b in _bullets) b.Step();
        Collide();
        RemoveDead();
    }

    // ------------------------------------------------------------ Spawning

    public void SpawnBullet(Bullet b)
    {
        _bullets.Add(b);
        _world.AddChild(b);
    }

    public void SpawnEnemy(Enemy e)
    {
        _enemies.Add(e);
        _world.AddChild(e);
    }

    public void Explode(Vector2 at, Color color, int amount) =>
        _world.AddChild(Effects.Explosion(at, color, amount));

    // ------------------------------------------------------------ Collisions

    private void Collide()
    {
        foreach (var b in _bullets)
        {
            if (b.Dead || b.IsEnemy) continue;
            foreach (var e in _enemies)
            {
                if (e.Dead || !b.Rect.Intersects(e.Rect)) continue;
                // A beam hits each enemy only once.
                if (!b.AlreadyHit.Add(e.GetInstanceId())) continue;

                e.Hit(b.Damage);
                if (e.Dead)
                {
                    Score += e.Points;
                    Explode(e.Rect.GetCenter(), e.Tint, 18);
                }
                if (!b.Pierce)
                {
                    b.Dead = true;
                    break;
                }
                // A beam loses some power with every hit.
                if (--b.Damage <= 0)
                {
                    b.Dead = true;
                    break;
                }
            }
        }

        if (Player.Invulnerable) return;

        var hitbox = Player.Hitbox;
        var hit = false;
        foreach (var b in _bullets)
        {
            if (b.IsEnemy && !b.Dead && b.Rect.Intersects(hitbox))
            {
                b.Dead = true;
                hit = true;
            }
        }
        foreach (var e in _enemies)
        {
            if (!e.Dead && e.Rect.Intersects(hitbox))
            {
                if (e.DiesOnRam) e.Dead = true;
                hit = true;
            }
        }
        if (hit) PlayerHit();
    }

    private void PlayerHit()
    {
        var center = Player.Position + new Vector2(8, 4);
        Explode(center, Palette.Hull, 40);
        Explode(center, Palette.Flame, 25);
        Lives--;

        if (Lives <= 0)
        {
            CurrentState = State.Over;
            HiScore = Mathf.Max(HiScore, Score);
            Player.Visible = false;
            return;
        }
        Player.Respawn();
    }

    private void RemoveDead()
    {
        _bullets.RemoveAll(b =>
        {
            if (b.Dead) b.QueueFree();
            return b.Dead;
        });
        _enemies.RemoveAll(e =>
        {
            if (e.Dead) e.QueueFree();
            return e.Dead;
        });
    }

    private static void ToggleFullscreen()
    {
        var mode = DisplayServer.WindowGetMode();
        DisplayServer.WindowSetMode(mode == DisplayServer.WindowMode.Fullscreen
            ? DisplayServer.WindowMode.Windowed
            : DisplayServer.WindowMode.Fullscreen);
    }
}
