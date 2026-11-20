using Godot;

namespace Shmup;

/// <summary>
/// Random waves that get denser over time. Placeholder until levels are
/// built in the editor (TileMap + Path2D for formations).
/// </summary>
public sealed class WaveSpawner
{
    private int _timer;

    public void Reset() => _timer = 60;

    public void Step(Main game)
    {
        if (--_timer > 0) return;

        var difficulty = Mathf.Min(1f, game.Tick / (60f * 120)); // full difficulty after 2 minutes
        _timer = (int)(150 - 80 * difficulty);

        var rng = game.Rng;
        var roll = rng.RandiRange(0, 9);
        if (roll < 5)
        {
            var y = rng.RandfRange(30, Main.ScreenH - 50);
            for (var i = 0; i < 5; i++)
                game.SpawnEnemy(Drone.Create(new Vector2(Main.ScreenW + i * 14, y), i * 0.6f));
        }
        else if (roll < 8)
        {
            for (var i = 0; i < 3; i++)
                game.SpawnEnemy(Chaser.Create(new Vector2(Main.ScreenW + i * 30, rng.RandfRange(20, Main.ScreenH - 30))));
        }
        else
        {
            game.SpawnEnemy(Heavy.Create(new Vector2(Main.ScreenW, rng.RandfRange(30, Main.ScreenH - 50))));
        }
    }
}
