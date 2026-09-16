using System.IO;
using Ymm4HighlightNavigator.Core;
using YukkuriMovieMaker.Plugin.FileSource.FFmpeg;

namespace Ymm4HighlightNavigator.Plugin;

public sealed record Ymm4FfmpegPaths(string FfmpegPath, string FfprobePath);

/// <summary>
/// Resolves the FFmpeg binaries owned by the current YMM4 host.
/// No external PATH lookup, Plugin-private copy, or guessed YMM4-relative path is used.
/// </summary>
public static class Ymm4FfmpegLocator
{
    public static Ymm4FfmpegPaths Resolve()
    {
        string ffmpegDirectory = Path.GetFullPath(FFmpegResourceLocator.GetFFmpegDirectory());
        string ffmpeg = Path.GetFullPath(FFmpegResourceLocator.GetFFmpegExePath());
        string ffprobe = Path.GetFullPath(Path.Combine(ffmpegDirectory, "ffprobe.exe"));

        if (!Directory.Exists(ffmpegDirectory))
            throw new DirectoryNotFoundException("YMM4のFFmpegフォルダーを取得できませんでした。");
        if (!File.Exists(ffmpeg))
            throw new FileNotFoundException("YMM4同梱のffmpeg.exeが見つかりません。", ffmpeg);
        if (!File.Exists(ffprobe))
            throw new FileNotFoundException("YMM4同梱のffprobe.exeが見つかりません。", ffprobe);

        string? actualFfmpegDirectory = Path.GetDirectoryName(ffmpeg);
        if (actualFfmpegDirectory is null || !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(actualFfmpegDirectory), ffmpegDirectory))
            throw new InvalidDataException("YMM4のFFmpeg locatorが一貫しない場所を返しました。");

        return new(ffmpeg, ffprobe);
    }

    public static FfmpegBackend CreateBackend()
    {
        var paths = Resolve();
        return new(paths.FfmpegPath, paths.FfprobePath);
    }
}
