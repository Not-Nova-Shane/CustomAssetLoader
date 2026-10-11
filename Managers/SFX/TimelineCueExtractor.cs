using System;
using System.Collections.Generic;
using Il2CppSystem;
using UnityEngine.Timeline;

namespace CustomAssetLoader.Managers.SFX;

/// <summary>
/// Resolves a Timeline clip's displayName to raw audio bytes. CustomAssetLoader has no
/// per-appearance asset store, so callers supply this. A closure capturing an
/// appearanceID or bundle is the idiomatic use.
///
/// Return null to signal "no audio for this clip" — the extractor skips it silently,
/// which is the desired behaviour for clips whose displayName overlaps with a VFX
/// or animation clip.
/// </summary>
public delegate byte[] AudioByteLookup(string clipName);

/// <summary>
/// Extracts SoundCues from a Timeline and strips the audio tracks once they're read.
///
/// Ported from LEAGUE-OF-NINE/motions (Motions/CueExtractor.cs). Two behaviours are
/// preserved exactly:
///
///   1. Extraction iterates timeline.flattenedTracks -> track.clips, lowercases
///      clip.displayName, and asks the lookup for bytes. A null result skips the clip.
///
///   2. Stripping removes Unity AudioTracks and any track carrying a clip whose asset
///      type name contains "FMOD". FMOD tracks must go, otherwise the game's own
///      RuntimeManager tries to resolve a Studio event that was authored against the
///      mod's own master bank — the exact failure the port exists to avoid.
///
/// Both operations mutate the timeline. Clone before calling if the original is
/// shared or addressable-cached; see <see cref="CloneExtractStripRegister"/> for the
/// whole pipeline in one call.
/// </summary>
public static class TimelineCueExtractor
{
    /// <summary>
    /// Walks every flattened track in <paramref name="timeline"/> and returns one cue
    /// per clip whose displayName resolves to audio bytes. Does not mutate the timeline.
    /// </summary>
    public static List<SoundCue> ExtractSoundCues(TimelineAsset timeline, AudioByteLookup lookup)
    {
        var cues = new List<SoundCue>();
        if (timeline == null || lookup == null) return cues;

        foreach (var track in timeline.flattenedTracks)
        {
            foreach (var clip in track.clips)
            {
                string clipName = clip.displayName?.ToLower();
                if (string.IsNullOrEmpty(clipName)) continue;

                var bytes = lookup(clipName);
                if (bytes == null) continue;

                Plugin.Log.LogInfo(
                    $"[Sfx] cue: {clipName} | {bytes.Length} bytes | " +
                    $"start={clip.start:F3}s clipIn={clip.clipIn:F3}s duration={clip.duration:F3}s");

                cues.Add(new SoundCue
                {
                    StartTime = (float)clip.start,
                    ClipIn = (float)clip.clipIn,
                    Duration = (float)clip.duration,
                    WavData = bytes,
                    Triggered = false
                });
            }
        }

        return cues;
    }

    /// <summary>
    /// Removes Unity AudioTracks and any track whose clips carry FMOD assets. Mutates
    /// the timeline. Safe to call multiple times; already-removed tracks are gone.
    /// </summary>
    public static void StripAudioTracks(TimelineAsset timeline)
    {
        if (timeline == null) return;

        var tracksToRemove = new List<TrackAsset>();

        foreach (var track in timeline.flattenedTracks)
        {
            var typeName = track.GetIl2CppType().Name;

            bool isUnityAudio = typeName.Contains("AudioTrack");
            bool hasFMODClips = false;

            foreach (var clip in track.clips)
            {
                var asset = clip.asset;
                if (asset == null) continue;

                var assetType = asset.GetIl2CppType().Name;
                if (assetType.Contains("FMOD"))
                {
                    hasFMODClips = true;
                    break;
                }
            }

            if (isUnityAudio || hasFMODClips)
                tracksToRemove.Add(track);
        }

        foreach (var track in tracksToRemove)
        {
            timeline.DeleteTrack(track);
            Plugin.Log.LogInfo($"[Sfx] stripped track: {track.name}");
        }
    }

    /// <summary>
    /// End-to-end helper: clones the source timeline, extracts cues into an out
    /// parameter, strips audio tracks from the clone, registers the cues against the
    /// clone, and returns the clone.
    ///
    /// Mirrors CueExtractor.GetOrCacheTimeline from the motions project. The clone is
    /// required because StripAudioTracks mutates the asset, and addressable-cached
    /// Timelines are shared across every play — stripping the original would break
    /// vanilla playback for anything else using that asset.
    /// </summary>
    public static TimelineAsset CloneExtractStripRegister(
        TimelineAsset source,
        AudioByteLookup lookup,
        out List<SoundCue> cues)
    {
        cues = null;
        if (source == null) return null;

        var clone = UnityEngine.Object.Instantiate(source);

        cues = ExtractSoundCues(clone, lookup);
        StripAudioTracks(clone);

        if (cues.Count > 0)
            SoundCueCache.Register(clone, cues);

        return clone;
    }
}
