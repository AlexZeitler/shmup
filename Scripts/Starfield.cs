using Godot;

namespace Shmup;

/// <summary>Three parallax layers of single-pixel stars.</summary>
public partial class Starfield : Node2D
{
    private const int Count = 90;
    private static readonly float[] Speeds = { 0.25f, 0.6f, 1.3f };
    private static readonly byte[] Shades = { 70, 130, 220 };

    private readonly Vector2[] _pos = new Vector2[Count];
    private readonly int[] _layer = new int[Count];
    private readonly RandomNumberGenerator _rng = new() { Seed = 64 };

    public override void _Ready()
    {
        ZIndex = -10;
        for (var i = 0; i < Count; i++)
        {
            _layer[i] = i % 3;
            _pos[i] = new Vector2(_rng.RandfRange(0, Main.ScreenW), _rng.RandfRange(0, Main.ScreenH));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GetParent<Main>().Paused) return;
        for (var i = 0; i < Count; i++)
        {
            _pos[i].X -= Speeds[_layer[i]];
            if (_pos[i].X < 0)
                _pos[i] = new Vector2(_pos[i].X + Main.ScreenW, _rng.RandfRange(0, Main.ScreenH));
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (var i = 0; i < Count; i++)
        {
            var s = Shades[_layer[i]];
            var c = Color.Color8(s, s, (byte)Mathf.Min(255, s + 30));
            DrawRect(new Rect2(_pos[i].Floor(), Vector2.One), c);
        }
    }
}
