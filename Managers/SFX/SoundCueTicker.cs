using System;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.Playables;

namespace CustomAssetLoader.Managers.SFX;

/// <summary>
/// Per-director MonoBehaviour that fires SoundCues off a PlayableDirector's clock.
///
/// TickSoundCues is a verbatim port of SidecarSyncBehavior.TickSoundCues from
/// LEAGUE-OF-NINE/motions. The only structural difference is that the ticker reads
/// Director.time directly rather than the sidecar's synced slave time — CustomAssetLoader
/// has no sidecar to sync against, and Director.time is what the cues were authored
/// against anyway.
///
/// Must be registered with ClassInjector before AddComponent:
///   ClassInjector.RegisterTypeInIl2Cpp&lt;SoundCueTicker&gt;();
/// MotionSfxPatches.Apply does this for you.
/// </summary>
public class SoundCueTicker : MonoBehaviour
{
    // IL2CPP-injected MonoBehaviours need this constructor.
    public SoundCueTicker(IntPtr ptr) : base(ptr) { }

    /// <summary>Director whose time drives the cues. Null disables the ticker.</summary>
    public PlayableDirector Director;

    /// <summary>
    /// Cue list. The ticker takes ownership: each PlayableDirector gets its own copy
    /// so replays don't share Triggered flags with sibling instances of the same asset.
    /// </summary>
    public System.Collections.Generic.List<SoundCue> SoundCues = new();

    /// <summary>When true, the ticker is not auto-destroyed after the last cue finishes.</summary>
    public bool KeepAlive = false;

    private void Update()
    {
        if (Director == null || SoundCues == null || SoundCues.Count == 0)
        {
            if (!KeepAlive) Destroy(this);
            return;
        }

        // The source project only ticked while the game's own motion was active
        // (IsModdedSkillActive). Without that flag we approximate: keep ticking as
        // long as the director is actually playing or paused mid-timeline. Paused is
        // included so scrubbing in-editor still updates cues.
        var state = Director.state;
        if (state != PlayState.Playing && state != PlayState.Paused) return;

        TickSoundCues((float)Director.time);
    }

    private void OnDestroy()
    {
        StopAllCues();
    }

    /// <summary>
    /// Fires each cue as its time is reached, and stops the ones that have run their
    /// authored duration. Cues are structs, so each edit is written back into the list.
    ///
    /// Verbatim from SidecarSyncBehavior.TickSoundCues — do not "fix" the manual write-back.
    /// </summary>
    [HideFromIl2Cpp]
    public void TickSoundCues(float t)
    {
        for (int i = 0; i < SoundCues.Count; i++)
        {
            var cue = SoundCues[i];

            if (!cue.Triggered && t >= cue.StartTime)
            {
                cue.Triggered = true;
                float sfxVol = SoundManager.Instance != null ? SoundManager.Instance.Volume_SFX : 1f;
                cue.ActiveChannel = FMODAudioUtil.PlaySound(cue.WavData, cue.ClipIn, sfxVol);
                SoundCues[i] = cue;
                Plugin.Log.LogInfo(
                    $"[Sfx] fired at t={t:F3}s (clipIn={cue.ClipIn:F3}s dur={cue.Duration:F3}s)");
            }

            // Duration 0 means "play the whole clip", so only a cue that named one is cut short.
            if (cue.Triggered && cue.Duration > 0f && cue.ActiveChannel.hasHandle()
                && t >= cue.StartTime + cue.Duration)
            {
                cue.ActiveChannel.stop();
                cue.ActiveChannel = default;
                SoundCues[i] = cue;
            }
        }
    }

    /// <summary>
    /// Stops every currently-playing cue. Called from OnDestroy and from the Stop()
    /// patch so a director that halts mid-timeline doesn't leak audio.
    /// </summary>
    [HideFromIl2Cpp]
    public void StopAllCues()
    {
        if (SoundCues == null) return;
        for (int i = 0; i < SoundCues.Count; i++)
        {
            var cue = SoundCues[i];
            if (cue.ActiveChannel.hasHandle())
            {
                cue.ActiveChannel.stop();
                cue.ActiveChannel = default;
                SoundCues[i] = cue;
            }
        }
    }

    /// <summary>
    /// Clears Triggered and ActiveChannel flags so a replay of the same director
    /// fires every cue from the top. The patch calls this when Play is re-invoked.
    /// </summary>
    [HideFromIl2Cpp]
    public void ResetTriggers()
    {
        if (SoundCues == null) return;
        for (int i = 0; i < SoundCues.Count; i++)
        {
            var cue = SoundCues[i];
            if (cue.ActiveChannel.hasHandle())
            {
                cue.ActiveChannel.stop();
                cue.ActiveChannel = default;
            }
            cue.Triggered = false;
            SoundCues[i] = cue;
        }
    }
}
