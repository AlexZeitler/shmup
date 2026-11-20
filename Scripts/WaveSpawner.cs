using Godot;

namespace Shmup;

/// <summary>
/// Random waves that get denser over time. The waves of a terrain section come
/// from that section's seed, so after a rewind the same enemies arrive at the
/// same places. Placeholder until levels are built in the editor (TileMap +
/// Path2D for formations).
/// </summary>
public sealed class WaveSpawner
{
    // Enemy sprites reach a few pixels beyond their hitbox, so spawn them fully off-screen.
    private const float SpawnX = Main.ScreenW + 8;
    // Keeps enemies clear of the score at the top and the beam gauge at the bottom.
    private const float HudTop = 10, HudBottom = Main.ScreenH - 14;

    private readonly RandomNumberGenerator _rng = new();
    private int _section = -1;
    private int _timer;

    /// <summary>Restarts the waves of the current section on the next step.</summary>
    public void Reset() => _section = -1;

    public void Step(Main game)
    {
        var section = game.Terrain.SectionAt(0);
        if (section != _section)
        {
            _section = section;
            _rng.Seed = Main.RunSeed * 7_919UL + (ulong)section;
            _timer = 60;
        }

        if (--_timer > 0) return;

        // Full difficulty after 2 minutes of scrolling; the scroll position repeats after a rewind.
        var difficulty = Mathf.Min(1f, game.Terrain.ScrollPx / (Terrain.ScrollSpeed * 60f * 120));
        _timer = (int)(150 - 80 * difficulty);

        var roll = _rng.RandiRange(0, 9);
        if (roll < 5)
        {
            var y = PickY(game, Sprites.Drone, 22);
            for (var i = 0; i < 5; i++)
                game.SpawnEnemy(Drone.Create(new Vector2(SpawnX + i * 14, y), i * 0.6f));
        }
        else if (roll < 8)
        {
            for (var i = 0; i < 3; i++)
                game.SpawnEnemy(Chaser.Create(new Vector2(SpawnX + i * 30, PickY(game, Sprites.Chaser, 0))));
        }
        else
        {
            game.SpawnEnemy(Heavy.Create(new Vector2(SpawnX, PickY(game, Sprites.Heavy, 12))));
        }
    }

    /// <summary>
    /// Random height inside the free corridor at the spawn point. The margin
    /// leaves room for the vertical swing of the enemy's flight path.
    /// </summary>
    private float PickY(Main game, Texture2D sprite, float margin)
    {
        var size = sprite.GetSize();
        var (top, bottom) = game.Terrain.FreeSpan(new Rect2(new Vector2(SpawnX, Main.ScreenH / 2f), size));
        top = Mathf.Max(top, HudTop) + margin;
        bottom = Mathf.Min(bottom, HudBottom) - margin - size.Y;
        var t = _rng.Randf();
        return bottom > top ? Mathf.Lerp(top, bottom, t) : (top + bottom) / 2;
    }
}
