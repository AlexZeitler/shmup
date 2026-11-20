using Godot;

namespace Shmup;

public partial class Hud : Node2D
{
    public override void _Ready() => ZIndex = 10;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var game = GetParent<Main>();
        const int w = Main.ScreenW, h = Main.ScreenH;

        PixelFont.Draw(this, $"SCORE {game.Score:D7}", new Vector2(4, 3), Palette.White);
        var hi = $"HI {Mathf.Max(game.HiScore, game.Score):D7}";
        PixelFont.Draw(this, hi, new Vector2(w - 4 - PixelFont.Width(hi), 3), Palette.White);

        // Beam gauge at the bottom, like the original
        var y = h - 10f;
        PixelFont.Draw(this, "BEAM", new Vector2(4, y), Palette.White);
        const float bx = 24, bw = 120;
        DrawRect(new Rect2(bx - 1, y - 1, bw + 2, 7), Palette.HullDark);
        DrawRect(new Rect2(bx, y, bw, 5), Palette.Background);
        var charge = game.Player.Charge;
        var full = charge >= Player.ChargeMax && game.Tick % 8 < 4;
        DrawRect(new Rect2(bx, y, bw * charge / Player.ChargeMax, 5), full ? Palette.White : Palette.Beam);
        DrawRect(new Rect2(bx + bw * Player.ChargeMin / Player.ChargeMax, y - 1, 1, 7), Palette.Hull);

        var icon = Sprites.Player.GetSize() * 0.6f;
        for (var i = 0; i < game.Lives; i++)
            DrawTextureRect(Sprites.Player, new Rect2(new Vector2(w - 14 - i * 12, y - 1), icon), false);

        if (game.Paused)
            Centered("PAUSE", h / 2f - 5, 2);
        if (game.CurrentState == Main.State.Over)
        {
            Centered("GAME OVER", h / 2f - 12, 2);
            Centered("PRESS ENTER", h / 2f + 4, 1);
        }
    }

    private void Centered(string text, float y, int scale) =>
        PixelFont.Draw(this, text, new Vector2((Main.ScreenW - PixelFont.Width(text, scale)) / 2f, y), Palette.White, scale);
}

/// <summary>3×5 pixel font that suits the 320×180 resolution.</summary>
public static class PixelFont
{
    private static readonly System.Collections.Generic.Dictionary<char, string> Glyphs = new()
    {
        ['0'] = "111101101101111", ['1'] = "010110010010111", ['2'] = "111001111100111",
        ['3'] = "111001111001111", ['4'] = "101101111001001", ['5'] = "111100111001111",
        ['6'] = "111100111101111", ['7'] = "111001010010010", ['8'] = "111101111101111",
        ['9'] = "111101111001111", ['A'] = "010101111101101", ['B'] = "110101110101110",
        ['C'] = "011100100100011", ['D'] = "110101101101110", ['E'] = "111100110100111",
        ['F'] = "111100110100100", ['G'] = "011100101101011", ['H'] = "101101111101101",
        ['I'] = "111010010010111", ['J'] = "001001001101010", ['K'] = "101101110101101",
        ['L'] = "100100100100111", ['M'] = "101111111101101", ['N'] = "110101101101101",
        ['O'] = "010101101101010", ['P'] = "110101110100100", ['Q'] = "010101101110011",
        ['R'] = "110101110101101", ['S'] = "011100010001110", ['T'] = "111010010010010",
        ['U'] = "101101101101111", ['V'] = "101101101101010", ['W'] = "101101111111101",
        ['X'] = "101101010101101", ['Y'] = "101101010010010", ['Z'] = "111001010100111",
        ['='] = "000111000111000", [':'] = "000010000010000", ['-'] = "000000111000000",
        ['!'] = "010010010000010", ['.'] = "000000000000010", [' '] = "000000000000000",
    };

    public static int Width(string text, int scale = 1) => (text.Length * 4 - 1) * scale;

    public static void Draw(CanvasItem canvas, string text, Vector2 at, Color color, int scale = 1)
    {
        var x = at.X;
        foreach (var ch in text.ToUpperInvariant())
        {
            if (Glyphs.TryGetValue(ch, out var g))
                for (var i = 0; i < 15; i++)
                    if (g[i] == '1')
                        canvas.DrawRect(new Rect2(x + i % 3 * scale, at.Y + i / 3 * scale, scale, scale), color);
            x += 4 * scale;
        }
    }
}
