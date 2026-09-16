using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ymm4HighlightNavigator.Core;

public sealed record AnalysisProgress(string Stage, double SourceSeconds, double Fraction);
public sealed record MediaInfo(double DurationSeconds, bool HasAudio)
{
    // DurationSeconds is the first video stream's usable source domain, not a longer audio tail.
    public double ContainerDurationSeconds { get; init; } = DurationSeconds;
    public string DurationBasis { get; init; } = "container-fallback";
}

public sealed class FfmpegBackend
{
    public string FfmpegPath { get; }
    public string FfprobePath { get; }
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".mov", ".webm", ".avi", ".m4v", ".ts" };
    public FfmpegBackend(string ffmpegPath, string ffprobePath)
    {
        FfmpegPath = RequireFile(ffmpegPath); FfprobePath = RequireFile(ffprobePath);
    }
    private static string RequireFile(string path)
    {
        string full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("Required local file was not found.", full);
        return full;
    }
    public async Task<MediaInfo> ProbeAsync(string path, CancellationToken token = default)
    {
        path = RequireFile(path);
        if (!MediaExtensions.Contains(Path.GetExtension(path))) throw new NotSupportedException("A local recording file is required; playlists/URLs are not accepted.");
        string json = await ChildProcess.CaptureAsync(FfprobePath, ["-v", "error", "-protocol_whitelist", "file,pipe", "-show_entries", "format=duration,start_time,format_name:stream=codec_type,start_time,duration:stream_tags=DURATION", "-of", "json", path], TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var streams = doc.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        var videos = streams.Where(s => s.GetProperty("codec_type").GetString() == "video").ToArray();
        if (videos.Length == 0) throw new InvalidDataException("No video stream.");
        var format = doc.RootElement.GetProperty("format");
        double seconds = Number(format, "duration") ?? 0;
        if (seconds <= 0) throw new InvalidDataException("Unknown recording duration.");
        double origin = Number(format, "start_time") ?? 0;
        var video = videos[0];
        double? videoEnd = null; string basis = "container-fallback";
        double? duration = Number(video, "duration");
        if (duration is > 0)
        {
            videoEnd = (Number(video, "start_time") ?? origin) - origin + duration;
            basis = "video-stream-duration";
        }
        else
        {
            string formatName = format.TryGetProperty("format_name", out var name) ? name.GetString() ?? "" : "";
            if ((formatName.Contains("matroska", StringComparison.Ordinal) || formatName.Contains("webm", StringComparison.Ordinal))
                && video.TryGetProperty("tags", out var tags))
            {
                foreach (var tag in tags.EnumerateObject())
                    if (tag.Name.Equals("DURATION", StringComparison.OrdinalIgnoreCase) && Clock(tag.Value.GetString()) is double endpoint)
                    {
                        // FFmpeg's Matroska DURATION tag is the track end timestamp (max packet timestamp + duration).
                        videoEnd = endpoint - origin; basis = "matroska-video-end-tag"; break;
                    }
            }
        }
        if (videoEnd is not > 0 || !double.IsFinite(videoEnd.Value)) { videoEnd = seconds; basis = "container-fallback"; }
        return new(Math.Min(seconds, videoEnd.Value), streams.Any(s => s.GetProperty("codec_type").GetString() == "audio"))
            { ContainerDurationSeconds = seconds, DurationBasis = basis };
    }
    private static double? Number(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        string text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.GetRawText();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) && double.IsFinite(result) ? result : null;
    }
    private static double? Clock(string? value)
    {
        if (value == null) return null;
        var parts = value.Split(':');
        if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int hours) || hours < 0
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) || minutes is < 0 or > 59
            || !double.TryParse(parts[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds) || seconds < 0 || seconds >= 60 || !double.IsFinite(seconds)) return null;
        return hours * 3600d + minutes * 60d + seconds;
    }

    public async Task<FeaturePack> ExtractAsync(string path, double startSeconds = 0, double? endSeconds = null,
        int fps = 2, IProgress<AnalysisProgress>? progress = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); path = RequireFile(path);
        if (fps is not (1 or 2 or 4)) throw new ArgumentOutOfRangeException(nameof(fps));
        var stamp = new FileInfo(path); long initialSize = stamp.Length, initialWrite = stamp.LastWriteTimeUtc.Ticks;
        var info = await ProbeAsync(path, token).ConfigureAwait(false);
        double requestedEnd = endSeconds ?? info.DurationSeconds;
        Guard.Range(startSeconds, requestedEnd);
        if (requestedEnd > info.ContainerDurationSeconds + .00001) throw new ArgumentOutOfRangeException(nameof(endSeconds), "Requested range exceeds the recording.");
        double end = Math.Min(requestedEnd, info.DurationSeconds);
        Guard.Range(startSeconds, end);
        // Do not synthesize a final video frame merely to fill an audio-only container tail.
        if (requestedEnd > end) progress?.Report(new("映像のある範囲を解析", end, 0));
        if ((end - startSeconds) * fps > 500_000 || (end - startSeconds) * 20 > 2_000_000)
            throw new InvalidDataException("Split this recording into bounded source ranges.");
        string identity;
        await using (var input = File.OpenRead(path)) identity = Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false)).ToLowerInvariant();
        string backend = (await ChildProcess.CaptureAsync(FfmpegPath, ["-version"], TimeSpan.FromSeconds(10), token).ConfigureAwait(false)).Split('\n')[0].Trim();
        if (backend.Length > 512) backend = backend[..512];
        string Text(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        var common = new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-xerror", "-threads", "1", "-filter_threads", "1", "-protocol_whitelist", "file,pipe", "-ss", Text(startSeconds), "-i", path, "-t", Text(end - startSeconds) };
        var video = ImmutableArray.CreateBuilder<VideoFeature>(); var extractor = new VisualExtractor();
        byte[] frame = new byte[FeatureFormat.Width * FeatureFormat.Height * 3];
        await ChildProcess.StreamAsync(FfmpegPath, common.Concat(["-map", "0:v:0", "-an", "-vf", $"fps={fps}:start_time=0:round=up,scale=64:36:flags=area", "-pix_fmt", "rgb24", "-fps_mode", "passthrough", "-f", "rawvideo", "pipe:1"]),
            async (stream, ct) =>
            {
                int index = 0;
                while (await ReadBlockAsync(stream, frame, ct).ConfigureAwait(false))
                {
                    double time = startSeconds + index++ / (double)fps;
                    if (time >= end) continue;
                    video.Add(extractor.Extract(frame, time));
                    if (video.Count % fps == 0) progress?.Report(new("映像を解析", time, .8 * (time - startSeconds) / (end - startSeconds)));
                }
            }, TimeSpan.FromHours(2), token).ConfigureAwait(false);
        var audio = ImmutableArray.CreateBuilder<AudioFeature>();
        if (info.HasAudio)
        {
            var audioExtractor = new AudioExtractor();
            await ChildProcess.StreamAsync(FfmpegPath, common.Concat(["-map", "0:a:0", "-vn", "-ac", "1", "-af", "aresample=8000:async=1:first_pts=0,apad", "-ar", "8000", "-f", "f32le", "pipe:1"]),
                async (stream, ct) =>
                {
                    byte[] block = new byte[4 * FeatureFormat.AudioHop]; long samples = 0;
                    while (true)
                    {
                        int used = 0, read;
                        while (used < block.Length && (read = await stream.ReadAsync(block.AsMemory(used), ct).ConfigureAwait(false)) > 0) used += read;
                        if (used == 0) break;
                        if (used % 4 != 0) throw new InvalidDataException("Truncated PCM float.");
                        for (int i = 0; i < used; i += 4)
                        {
                            samples++;
                            var point = audioExtractor.Push(BinaryPrimitives.ReadSingleLittleEndian(block.AsSpan(i, 4)), startSeconds);
                            if (point != null) audio.Add(point);
                        }
                        if (samples % FeatureFormat.AudioRate == 0) progress?.Report(new("音声を解析", startSeconds + samples / 8000d, .8 + .2 * samples / 8000d / (end - startSeconds)));
                    }
                    var tail = audioExtractor.Finish(startSeconds); if (tail != null) audio.Add(tail);
                }, TimeSpan.FromHours(2), token).ConfigureAwait(false);
        }
        stamp.Refresh();
        if (!stamp.Exists || stamp.Length != initialSize || stamp.LastWriteTimeUtc.Ticks != initialWrite)
            throw new IOException("The source changed during analysis; no Pack was committed.");
        var pack = new FeaturePack(new(FeatureFormat.Version, FeatureFormat.Extractor, fps, startSeconds, end, info.HasAudio, identity, backend), video.ToImmutable(), audio.ToImmutable());
        pack.Validate(); progress?.Report(new("特徴データを検証", end, 1)); return pack;
    }
    private static async Task<bool> ReadBlockAsync(Stream stream, Memory<byte> block, CancellationToken token)
    {
        int used = 0;
        while (used < block.Length)
        {
            int read = await stream.ReadAsync(block[used..], token).ConfigureAwait(false);
            if (read == 0) { if (used == 0) return false; throw new InvalidDataException("Truncated raw video frame."); }
            used += read;
        }
        return true;
    }
}

public static class ChildProcess
{
    public static async Task<string> CaptureAsync(string executable, IEnumerable<string> arguments, TimeSpan timeout, CancellationToken token)
    {
        using var data = new MemoryStream();
        await StreamAsync(executable, arguments, async (source, ct) =>
        {
            byte[] buffer = new byte[4096]; int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) != 0)
            {
                if (data.Length + read > 1024 * 1024) throw new InvalidDataException("Child text output exceeded its budget.");
                data.Write(buffer, 0, read);
            }
        }, timeout, token).ConfigureAwait(false);
        return Encoding.UTF8.GetString(data.ToArray());
    }
    public static async Task StreamAsync(string executable, IEnumerable<string> arguments,
        Func<Stream, CancellationToken, Task> consumer, TimeSpan timeout, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(timeout);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start }; if (!process.Start()) throw new IOException("Could not start the analysis backend.");
        void Kill() { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
        using var cancellation = deadline.Token.Register(Kill);
        Task<string> stderr = DrainErrorAsync(process.StandardError);
        try
        {
            await consumer(process.StandardOutput.BaseStream, deadline.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            string error = await stderr.ConfigureAwait(false); deadline.Token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new IOException($"Backend failed (exit {process.ExitCode}): {error}");
        }
        catch (Exception ex) when (deadline.IsCancellationRequested && ex is not OutOfMemoryException)
        {
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            throw new TimeoutException("Analysis backend exceeded its time budget.", ex);
        }
        finally
        {
            Kill(); if (!process.WaitForExit(5000)) throw new IOException("Backend did not terminate after cancellation.");
            _ = await stderr.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
    }
    private static async Task<string> DrainErrorAsync(StreamReader reader)
    {
        var tail = new StringBuilder(); char[] block = new char[2048]; int read;
        while ((read = await reader.ReadAsync(block).ConfigureAwait(false)) > 0)
        { tail.Append(block, 0, read); if (tail.Length > 8192) tail.Remove(0, tail.Length - 8192); }
        return tail.ToString();
    }
}
