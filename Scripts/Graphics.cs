using Godot;

namespace Shmup;

public static class Palette
{
    public static readonly Color Background = Color.Color8(8, 6, 20);
    public static readonly Color Hull = Color.Color8(170, 190, 220);
    public static readonly Color HullDark = Color.Color8(90, 100, 140);
    public static readonly Color Flame = Color.Color8(255, 150, 40);
    public static readonly Color White = Color.Color8(255, 255, 255);
    public static readonly Color Beam = Color.Color8(120, 200, 255);
}

/// <summary>PNG sprites from res://Sprites.</summary>
public static class Sprites
{
    public static readonly Texture2D Player = Load("player");
    public static readonly Texture2D Life = Load("life");
    public static readonly Texture2D Drone = Load("drone");
    public static readonly Texture2D Chaser = Load("chaser");
    public static readonly Texture2D Heavy = Load("heavy");
    public static readonly Texture2D Shot = Load("shot");
    public static readonly Texture2D EnemyShot = Load("enemy_shot");
    public static readonly Texture2D[] Beam = { Load("beam1"), Load("beam2"), Load("beam3") };
    public static readonly Texture2D Terrain = Load("terrain"); // 16 tiles, see Terrain.DrawColumn
    public static readonly SpriteFrames ExplosionSmall = Frames("explosion_small", 32);
    public static readonly SpriteFrames ExplosionLarge = Frames("explosion_large", 48);

    /// <summary>
    /// Sprite drawn centred on a hitbox whose top-left corner is the parent's origin.
    /// The graphics are larger than the hitboxes, so the hitboxes keep their old sizes.
    /// </summary>
    public static Sprite2D Around(Texture2D texture, Vector2 hitbox) => new()
    {
        Texture = texture,
        Centered = false,
        Position = ((hitbox - texture.GetSize()) / 2).Floor(),
    };

    private static Texture2D Load(string name) => GD.Load<Texture2D>($"res://Sprites/{name}.png");

    /// <summary>Horizontal sheet of square frames.</summary>
    private static SpriteFrames Frames(string name, int size)
    {
        var sheet = Load(name);
        var frames = new SpriteFrames();
        frames.SetAnimationLoop("default", false);
        frames.SetAnimationSpeed("default", 14);
        for (var x = 0; x < sheet.GetWidth(); x += size)
            frames.AddFrame("default", new AtlasTexture { Atlas = sheet, Region = new Rect2(x, 0, size, size) });
        return frames;
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

    public static AnimatedSprite2D Explosion(Vector2 at, SpriteFrames frames)
    {
        var sprite = new AnimatedSprite2D { Position = at, SpriteFrames = frames };
        sprite.AnimationFinished += sprite.QueueFree;
        sprite.Play();
        return sprite;
    }
}
