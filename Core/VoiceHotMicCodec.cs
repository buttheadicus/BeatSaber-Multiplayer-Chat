using System;
using System.Text;
using UnityEngine;

namespace MultiplayerChat.Core;

public static class VoiceHotMicCodec
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("VHOT");
    private const byte Version = 1;
    private const int HeaderSize = 4 + 1 + 4 + 2 + 4;

    public const int MaxChunkPlainBytes = 131_072;

    public static bool IsHotMicBlob(byte[]? blob)
    {
        if (blob == null || blob.Length < 4) return false;
        return blob[0] == Magic[0] && blob[1] == Magic[1] && blob[2] == Magic[2] && blob[3] == Magic[3];
    }

    public static byte[]? EncodeChunk(float[] interleavedSamples, int channels, int sampleRate)
    {
        if (interleavedSamples == null || interleavedSamples.Length == 0 || channels < 1 || sampleRate < 8000)
            return null;
        if (interleavedSamples.Length % channels != 0)
            return null;

        var frameCount = interleavedSamples.Length / channels;
        var byteLen = HeaderSize + interleavedSamples.Length * 4;
        if (byteLen > MaxChunkPlainBytes)
            return null;

        var buffer = new byte[byteLen];
        Magic.CopyTo(buffer, 0);
        buffer[4] = Version;
        BitConverter.GetBytes(sampleRate).CopyTo(buffer, 5);
        BitConverter.GetBytes((ushort)channels).CopyTo(buffer, 9);
        BitConverter.GetBytes(frameCount).CopyTo(buffer, 11);

        var offset = HeaderSize;
        for (var i = 0; i < interleavedSamples.Length; i++)
        {
            BitConverter.GetBytes(interleavedSamples[i]).CopyTo(buffer, offset);
            offset += 4;
        }

        return buffer;
    }

    public static bool TryGetDurationMs(byte[] blob, out float ms)
    {
        ms = 0f;
        if (!IsHotMicBlob(blob) || blob.Length < HeaderSize)
            return false;
        if (blob[4] != Version)
            return false;
        var sampleRate = BitConverter.ToInt32(blob, 5);
        var channels = BitConverter.ToUInt16(blob, 9);
        var frameCount = BitConverter.ToInt32(blob, 11);
        if (sampleRate < 8000 || channels < 1 || frameCount < 1)
            return false;
        if (blob.Length != HeaderSize + frameCount * channels * 4)
            return false;
        ms = (float)(frameCount * 1000.0 / sampleRate);
        return true;
    }

    public static bool TryDecodeToFloatSamples(byte[] blob, out float[] interleavedSamples, out int sampleRate, out int channels)
    {
        interleavedSamples = Array.Empty<float>();
        sampleRate = 0;
        channels = 0;

        if (!IsHotMicBlob(blob) || blob.Length < HeaderSize)
            return false;

        if (blob[4] != Version)
            return false;

        sampleRate = BitConverter.ToInt32(blob, 5);
        channels = BitConverter.ToUInt16(blob, 9);
        var frameCount = BitConverter.ToInt32(blob, 11);

        if (sampleRate < 8000 || channels < 1 || frameCount < 1)
            return false;

        var expected = HeaderSize + frameCount * channels * 4;
        if (blob.Length != expected)
            return false;

        var sampleValues = new float[frameCount * channels];
        var offset = HeaderSize;
        for (var i = 0; i < sampleValues.Length; i++)
        {
            sampleValues[i] = BitConverter.ToSingle(blob, offset);
            offset += 4;
        }

        interleavedSamples = sampleValues;
        return true;
    }

    public static AudioClip? CreateAudioClip(byte[] blob)
    {
        if (!TryDecodeToFloatSamples(blob, out var samples, out var rate, out var ch))
            return null;

        return CreateAudioClipFromDecodedSamples(samples, ch, rate);
    }

    public static AudioClip? CreateAudioClipFromDecodedSamples(float[] interleavedSamples, int channels, int sampleRate)
    {
        if (interleavedSamples == null || interleavedSamples.Length == 0 || channels < 1 || sampleRate < 8000)
            return null;
        if (interleavedSamples.Length % channels != 0) return null;
        var frameCount = interleavedSamples.Length / channels;
        var clip = AudioClip.Create("VoiceHotMic", frameCount, channels, sampleRate, false);
        clip.SetData(interleavedSamples, 0);
        return clip;
    }
}
