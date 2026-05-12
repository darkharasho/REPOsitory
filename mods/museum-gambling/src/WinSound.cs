using System.IO;
using System.Reflection;
using UnityEngine;

namespace MuseumGambling;

internal static class WinSound
{
    private static Sound? _sound;

    internal static void Play(Vector3 position)
    {
        try
        {
            var sound = ResolveSound();
            if (sound == null)
            {
                Plugin.Log.LogWarning("[MuseumGambling] Could not initialize win jingle.");
                return;
            }
            sound.Play(position);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"WinSound.Play failed: {ex}");
        }
    }

    private static Sound? ResolveSound()
    {
        if (_sound != null) return _sound;

        var clip = LoadEmbeddedClip();
        if (clip == null) return null;

        // Source left null → Sound.Play falls back to AudioManager's mixer-routed
        // AudioHighFalloff prefab, so the jingle respects the player's volume
        // sliders. AudioManager.AudioType.HighFalloff = 0 (enum default).
        _sound = new Sound
        {
            Sounds = new[] { clip },
            Type = AudioManager.AudioType.HighFalloff,
            Volume = 0.7f,
            VolumeRandom = 0f,
            Pitch = 1f,
            PitchRandom = 0f,
            SpatialBlend = 0.7f,
            Doppler = 0f,
            ReverbMix = 1f,
            FalloffMultiplier = 2f,
            OffscreenVolume = 1f,
            OffscreenFalloff = 1f,
        };
        return _sound;
    }

    private static AudioClip? LoadEmbeddedClip()
    {
        var asm = Assembly.GetExecutingAssembly();
        using Stream? stream = asm.GetManifestResourceStream("MuseumGambling.assets.winjingle.wav");
        if (stream == null)
        {
            Plugin.Log.LogError("[MuseumGambling] Embedded winjingle.wav resource not found.");
            return null;
        }
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return WavLoader.Load(ms.ToArray(), name: "MuseumGambling.WinJingle");
    }
}

// Minimal 16-bit PCM WAV reader. The bundled jingle is mono 16-bit 44.1 kHz;
// supports any standard RIFF/WAVE file at those settings (or stereo).
internal static class WavLoader
{
    internal static AudioClip? Load(byte[] data, string name)
    {
        if (data.Length < 44) return null;
        if (data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F') return null;
        if (data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E') return null;

        int channels = System.BitConverter.ToInt16(data, 22);
        int sampleRate = System.BitConverter.ToInt32(data, 24);
        int bitsPerSample = System.BitConverter.ToInt16(data, 34);
        if (bitsPerSample != 16) return null;

        // Find "data" chunk (skip past any fmt extra bytes).
        int offset = 12;
        int dataOffset = -1;
        int dataLength = 0;
        while (offset < data.Length - 8)
        {
            string chunkId = System.Text.Encoding.ASCII.GetString(data, offset, 4);
            int chunkSize = System.BitConverter.ToInt32(data, offset + 4);
            if (chunkId == "data")
            {
                dataOffset = offset + 8;
                dataLength = chunkSize;
                break;
            }
            offset += 8 + chunkSize;
        }
        if (dataOffset < 0) return null;

        int sampleCount = dataLength / 2;
        int samplesPerChannel = sampleCount / channels;
        var pcm = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short s = System.BitConverter.ToInt16(data, dataOffset + i * 2);
            pcm[i] = s / 32768f;
        }

        var clip = AudioClip.Create(name, samplesPerChannel, channels, sampleRate, false);
        clip.SetData(pcm, 0);
        return clip;
    }
}
