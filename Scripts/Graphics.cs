using System.Collections.Generic;
using Godot;

namespace Shmup;

public static class Palette
{
    public static readonly Color Background = Color.Color8(8, 6, 20);
    public static readonly Color Hull = Color.Color8(170, 190, 220);
    public static readonly Color HullDark = Color.Color8(90, 100, 140);
    public static readonly Color Canopy = Color.Color8(90, 220, 255);
    public static readonly Color Flame = Color.Color8(255, 150, 40);
    public static readonly Color EnemyA = Color.Color8(220, 80, 120);
    public static readonly Color EnemyB = Color.Color8(140, 220, 90);
    public static readonly Color EnemyC = Color.Color8(240, 200, 70);
    public static readonly Color White = Color.Color8(255, 255, 255);
    public static readonly Color Shot = Color.Color8(255, 240, 140);
    public static readonly Color Beam = Color.Color8(120, 200, 255);
    public static readonly Color EnemyShot = Color.Color8(255, 90, 60);
}

/// <summary>Pixel art from ASCII – can be replaced by real sprites later.</summary>
public static class Sprites
{
    public static readonly Texture2D Player = Make(new[]
    {
        "...dd...........",
        "..dhhh..........",
        "f.dhhhhhh.......",
        "ffhhhhhcchhh....",
        "ffhhhhhhhhhhhhh.",
        "f.dhhhhhhhhh....",
        "..dhhh..........",
        "...dd...........",
    }, new() { ['h'] = Palette.Hull, ['d'] = Palette.HullDark, ['c'] = Palette.Canopy, ['f'] = Palette.Flame });

    public static readonly Texture2D Drone = Make(new[]
    {
        "...aaaa.",
        ".aaaaaaa",
        "aaeeaaa.",
        "aaeeaaaa",
        ".aaaaaaa",
        "...aaaa.",
    }, new() { ['a'] = Palette.EnemyA, ['e'] = Palette.White });

    public static readonly Texture2D Heavy = Make(new[]
    {
        "....bbbbbb....",
        "..bbbbbbbbbb..",
        ".bbbbeeeebbbbb",
        "bbbbeeeeeebbbb",
        "bbbbeeeeeebbbb",
        ".bbbbeeeebbbbb",
        "..bbbbbbbbbb..",
        "....bbbbbb....",
    }, new() { ['b'] = Palette.EnemyB, ['e'] = Palette.White });

    public static readonly Texture2D Chaser = Make(new[]
    {
        "..cc....",
        ".cccc...",
        "ccccccc.",
        "ecccccc.",
        ".cccc...",
        "..cc....",
    }, new() { ['c'] = Palette.EnemyC, ['e'] = Palette.White });

    private static Texture2D Make(string[] rows, Dictionary<char, Color> palette)
    {
        var img = Image.CreateEmpty(rows[0].Length, rows.Length, false, Image.Format.Rgba8);
        for (var y = 0; y < rows.Length; y++)
            for (var x = 0; x < rows[y].Length; x++)
                if (palette.TryGetValue(rows[y][x], out var c))
                    img.SetPixel(x, y, c);
        return ImageTexture.CreateFromImage(img);
    }
}

public static class Effects
{
    public static Gradient Fade(Color c)
    {
        var g = new Gradient();
        g.SetColor(0, c);
        g.SetColor(1, new Color(c, 0));
        return g;
    }

    public static CpuParticles2D Explosion(Vector2 at, Color color, int amount)
    {
        var p = new CpuParticles2D
        {
            Position = at,
            Amount = amount,
            OneShot = true,
            Explosiveness = 1,
            Lifetime = 0.6,
            Spread = 180,
            Gravity = Vector2.Zero,
            InitialVelocityMin = 30,
            InitialVelocityMax = 120,
            DampingMin = 60,
            DampingMax = 120,
            ColorRamp = Fade(color),
            Emitting = true,
        };
        p.Finished += p.QueueFree;
        return p;
    }
}
