namespace CustomAssetLoader.Managers.SFX;

/// <summary>
/// A scheduled sound event. Ported from LEAGUE-OF-NINE/motions.
///
/// This is a struct, matching the source: the tick loop copies each element out,
/// mutates it (Triggered, ActiveChannel), and writes it back into the list. Making
/// it a class would silently break that pattern's semantics — callers would still
/// write back but the identity would differ from what they expect if the list were
/// ever cloned.
/// </summary>
public struct SoundCue
{
    /// <summary>When in the timeline this cue fires (seconds).</summary>
    public float StartTime;

    /// <summary>Offset into the audio file where playback starts (clip.clipIn).</summary>
    public float ClipIn;

    /// <summary>How long the clip plays for (clip.duration); 0 = play to end.</summary>
    public float Duration;

    /// <summary>Pre-converted WAV bytes, played via FMOD Core.</summary>
    public byte[] WavData;

    /// <summary>Set true the moment the cue first fires, so it doesn't re-trigger on seek-back.</summary>
    public bool Triggered;

    /// <summary>Filled at runtime so we can stop it when done.</summary>
    public FMOD.Channel ActiveChannel;
}
