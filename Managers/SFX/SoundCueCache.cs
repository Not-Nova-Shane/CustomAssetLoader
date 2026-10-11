using System;
using System.Collections.Generic;
using UnityEngine;

namespace CustomAssetLoader.Managers.SFX;

/// <summary>
/// Runtime registry of extracted cue lists, keyed by asset name.
///
/// Name, not InstanceID: the game (and the motions project it was ported from) clones
/// TimelineAssets before playing them, and a clone carries the same name but a fresh
/// instance ID. Keying by instance ID means the play-time lookup in MotionSfxPatches
/// misses every clone, which is every timeline that actually plays.
///
/// Cross-mod name collisions are the author's problem, same as they are for any Unity
/// asset reference resolved by name. The manifest pipeline already namespaces keys by
/// label + resourceId; a matching convention on timeline names avoids the issue.
/// </summary>
public static class SoundCueCache
{
    private static readonly Dictionary<string, List<SoundCue>> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Stores a cue list for an asset. Replaces any previous entry and stops any
    /// cues still playing under the old list first.
    /// </summary>
    public static void Register(UnityEngine.Object asset, List<SoundCue> cues)
    {
        if (asset == null || cues == null) return;

        var key = KeyFor(asset);
        if (key == null) return;

        if (_cache.TryGetValue(key, out var old))
            StopAll(old);

        _cache[key] = cues;
    }

    /// <summary>Looks up the cue list for an asset (or any clone of it).</summary>
    public static bool TryGet(UnityEngine.Object asset, out List<SoundCue> cues)
    {
        cues = null;
        var key = KeyFor(asset);
        if (key == null) return false;
        return _cache.TryGetValue(key, out cues);
    }

    /// <summary>Removes and stops the cue list for an asset.</summary>
    public static void Release(UnityEngine.Object asset)
    {
        var key = KeyFor(asset);
        if (key == null) return;

        if (_cache.TryGetValue(key, out var cues))
        {
            StopAll(cues);
            _cache.Remove(key);
        }
    }

    /// <summary>
    /// Stops every playing cue and drops the entire cache. Call on bundle unload /
    /// scene change / plugin unload.
    /// </summary>
    public static void Clear()
    {
        foreach (var list in _cache.Values)
            StopAll(list);
        _cache.Clear();
    }

    private static string KeyFor(UnityEngine.Object asset)
    {
        if (asset == null) return null;
        var name = asset.name;
        // Fall back to instance ID only for unnamed assets — rare, but Unity does not
        // guarantee name uniqueness across bundles the way it does within one.
        return string.IsNullOrEmpty(name)
            ? asset.GetInstanceID().ToString()
            : name;
    }

    private static void StopAll(List<SoundCue> cues)
    {
        for (int i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];
            if (cue.ActiveChannel.hasHandle())
            {
                cue.ActiveChannel.stop();
                cue.ActiveChannel = default;
                cues[i] = cue;
            }
        }
    }
}
