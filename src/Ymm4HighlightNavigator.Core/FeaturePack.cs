using System.Collections.Immutable;

namespace Ymm4HighlightNavigator.Core;

public static class FeatureFormat
{
    public const int Version = 1;
    public const string Extractor = "tiny-rgb-audio-v1";
    public const int Width = 64, Height = 36, DescriptorWidth = 32, DescriptorHeight = 18;
    public const int Bins = 16, Cells = 16, AudioRate = 8000, AudioHop = 400, AudioWindow = 800;
    public const int MaxPayloadBytes = 128 * 1024 * 1024;
}

public sealed record FeatureHeader(int SchemaVersion, string Extractor, int VideoFps,
    double StartSeconds, double EndSeconds, bool HasAudio, string SourceHash, string Backend);

public sealed record VideoFeature(double TimeSeconds, float Luma, float Contrast, float Chroma,
    float Delta, float Edges, float Extremes, ImmutableArray<float> Histogram,
    ImmutableArray<float> GridLuma, ImmutableArray<float> GridDelta, ImmutableArray<byte> Descriptor);

// TimeSeconds is the END of the observed audio window, not the start of the video frame.
public sealed record AudioFeature(double TimeSeconds, float Rms, float Peak, int WindowSamples);

public sealed record FeaturePack(FeatureHeader Header, ImmutableArray<VideoFeature> Video, ImmutableArray<AudioFeature> Audio)
{
    public void Validate()
    {
        if (Header is null || Header.SchemaVersion != FeatureFormat.Version || Header.Extractor != FeatureFormat.Extractor)
            throw new InvalidDataException("Unsupported feature schema/extractor; missing fields are not zero.");
        if (Header.VideoFps is not (1 or 2 or 4)) throw new InvalidDataException("Unsupported sampling density.");
        Guard.Range(Header.StartSeconds, Header.EndSeconds);
        if (Header.SourceHash is null || Header.SourceHash.Length != 64 || !Header.SourceHash.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid source fingerprint.");
        if (Header.Backend is null || Header.Backend.Length > 512) throw new InvalidDataException("Invalid backend identity.");
        if (Video.IsDefaultOrEmpty || Video.Length > 500_000 || Audio.IsDefault || Audio.Length > 2_000_000)
            throw new InvalidDataException("Invalid feature counts.");
        for (int i = 0; i < Video.Length; i++)
        {
            var v = Video[i] ?? throw new InvalidDataException("Missing video row.");
            double expected = Header.StartSeconds + i / (double)Header.VideoFps;
            if (!double.IsFinite(v.TimeSeconds) || Math.Abs(v.TimeSeconds - expected) > 1e-6 || v.TimeSeconds >= Header.EndSeconds)
                throw new InvalidDataException("Invalid video sample clock.");
            Guard.Unit(v.Luma); Guard.Unit(v.Contrast); Guard.Unit(v.Chroma);
            Guard.Unit(v.Delta); Guard.Unit(v.Edges); Guard.Unit(v.Extremes);
            ValidateVector(v.Histogram, FeatureFormat.Bins);
            ValidateVector(v.GridLuma, FeatureFormat.Cells);
            ValidateVector(v.GridDelta, FeatureFormat.Cells);
            if (Math.Abs(v.Histogram.Sum(x => (double)x) - 1) > 0.001) throw new InvalidDataException("Histogram mass is not one.");
            if (v.Descriptor.IsDefault || v.Descriptor.Length != FeatureFormat.DescriptorWidth * FeatureFormat.DescriptorHeight)
                throw new InvalidDataException("Invalid descriptor.");
        }
        if (Header.EndSeconds - Video[^1].TimeSeconds > 1d / Header.VideoFps + 1e-5)
            throw new InvalidDataException("Incomplete video coverage.");
        if (Header.HasAudio != (Audio.Length > 0)) throw new InvalidDataException("Missing audio is not silent audio.");
        double previous = Header.StartSeconds;
        foreach (var a in Audio)
        {
            if (a is null || !double.IsFinite(a.TimeSeconds) || a.TimeSeconds <= previous || a.TimeSeconds > Header.EndSeconds + 1d / FeatureFormat.AudioRate + 1e-6)
                throw new InvalidDataException("Invalid audio sample clock.");
            Guard.Unit(a.Rms); Guard.Unit(a.Peak);
            if (a.Rms > a.Peak + 1e-5 || a.WindowSamples < 1 || a.WindowSamples > FeatureFormat.AudioWindow)
                throw new InvalidDataException("Invalid audio window.");
            previous = a.TimeSeconds;
        }
        if (Header.HasAudio && Header.EndSeconds - previous > 0.051)
            throw new InvalidDataException("Incomplete audio coverage.");
    }

    private static void ValidateVector(ImmutableArray<float> values, int count)
    {
        if (values.IsDefault || values.Length != count) throw new InvalidDataException("Missing feature vector.");
        foreach (float v in values) Guard.Unit(v);
    }
}

internal static class Guard
{
    internal static void Range(double start, double end)
    {
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start)
            throw new ArgumentOutOfRangeException(nameof(start), "Expected a finite nonempty half-open interval.");
    }
    internal static void Unit(float value)
    {
        if (!float.IsFinite(value) || value < 0 || value > 1.00001f) throw new InvalidDataException("Invalid normalized primitive.");
    }
}
