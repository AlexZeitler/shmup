using System.Collections.Generic;
using Godot;

namespace Shmup;

/// <summary>Sound files from res://Sounds. Their volume is set in the files themselves.</summary>
public static class Sounds
{
    public static readonly AudioStream Shot = Load("shot.wav");
    public static readonly AudioStream Hit = Load("hit.wav");
    public static readonly AudioStream[] Beam = { Load("beam1.wav"), Load("beam2.wav"), Load("beam3.wav") };
    public static readonly AudioStream ExplosionSmall = Load("explosion_small.wav");
    public static readonly AudioStream ExplosionLarge = Load("explosion_large.wav");
    public static readonly AudioStream GameOver = Load("game_over.wav");
    public static readonly AudioStream Charge = Looping((AudioStreamWav)Load("charge.wav"));
    public static readonly AudioStream Music = Looping((AudioStreamOggVorbis)Load("music.ogg"));

    private static AudioStream Load(string file) => GD.Load<AudioStream>($"res://Sounds/{file}");

    private static AudioStream Looping(AudioStreamWav wav)
    {
        wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        wav.LoopBegin = 0;
        wav.LoopEnd = Mathf.RoundToInt(wav.GetLength() * wav.MixRate);
        return wav;
    }

    private static AudioStream Looping(AudioStreamOggVorbis ogg)
    {
        ogg.Loop = true;
        return ogg;
    }
}

/// <summary>
/// One player per effect, so the same sound can overlap itself (polyphony) without
/// cutting off others. Music and the charge hum have their own players because they
/// loop and must pause with the game.
/// </summary>
public partial class Audio : Node
{
    private const int Voices = 4;

    private readonly Dictionary<AudioStream, AudioStreamPlayer> _effects = new();
    private AudioStreamPlayer _music = null!;
    private AudioStreamPlayer _charge = null!;
    private AudioStreamPlayer _gameOver = null!;

    public override void _Ready()
    {
        _music = Add(Sounds.Music);
        _charge = Add(Sounds.Charge);
        _gameOver = Add(Sounds.GameOver);
    }

    public void Play(AudioStream stream)
    {
        if (!_effects.TryGetValue(stream, out var player))
        {
            player = Add(stream);
            player.MaxPolyphony = Voices;
            _effects[stream] = player;
        }
        player.Play();
    }

    /// <summary>Called every tick. progress 0..1 raises the hum by up to an octave, null silences it.</summary>
    public void Charge(float? progress)
    {
        if (progress is not { } p)
        {
            _charge.Stop();
            return;
        }
        _charge.PitchScale = 1 + p;
        if (!_charge.Playing) _charge.Play();
    }

    public void StartMusic()
    {
        _gameOver.Stop();
        _music.Play();
    }

    /// <summary>Music and charge hum hold their position; short effects play out.</summary>
    public void SetPaused(bool paused)
    {
        _music.StreamPaused = paused;
        _charge.StreamPaused = paused;
    }

    public void GameOver()
    {
        _music.Stop();
        _charge.Stop();
        _gameOver.Play();
    }

    private AudioStreamPlayer Add(AudioStream stream)
    {
        var player = new AudioStreamPlayer { Stream = stream };
        AddChild(player);
        return player;
    }
}
