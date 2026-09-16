using System.IO;
using System.Reflection;
using Ymm4HighlightNavigator.Core;

namespace Ymm4HighlightNavigator.Plugin;

public sealed record Ymm4FfmpegPaths(string FfmpegPath, string FfprobePath);

/// <summary>
/// Resolves the FFmpeg binaries owned by the current YMM4 host.
/// The optional FFmpeg host assembly is loaded lazily so a future dependency change can disable
/// analysis with a user-facing message instead of preventing the whole Tool from loading.
/// </summary>
public static class Ymm4FfmpegLocator
{
    private const string AssemblyName = "YukkuriMovieMaker.Plugin.FileSource.FFmpeg";
    private const string LocatorTypeName = "YukkuriMovieMaker.Plugin.FileSource.FFmpeg.FFmpegResourceLocator";

    public static Ymm4FfmpegPaths Resolve()
    {
        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => StringComparer.Ordinal.Equals(x.GetName().Name, AssemblyName))
                ?? Assembly.Load(new AssemblyName(AssemblyName));
            var locator = assembly.GetType(LocatorTypeName, throwOnError: false)
                ?? throw new TypeLoadException(LocatorTypeName);

            string ffmpegDirectory = Path.GetFullPath(InvokeString(locator, "GetFFmpegDirectory"));
            string ffmpeg = Path.GetFullPath(InvokeString(locator, "GetFFmpegExePath"));
            string ffprobe = Path.GetFullPath(Path.Combine(ffmpegDirectory, "ffprobe.exe"));

            if (!Directory.Exists(ffmpegDirectory) || !File.Exists(ffmpeg) || !File.Exists(ffprobe))
                throw new FileNotFoundException("YMM4 bundled FFmpeg capability is incomplete.");

            string? actualFfmpegDirectory = Path.GetDirectoryName(ffmpeg);
            if (actualFfmpegDirectory is null || !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(actualFfmpegDirectory), ffmpegDirectory))
                throw new InvalidDataException("YMM4 FFmpeg locator returned inconsistent paths.");

            return new(ffmpeg, ffprobe);
        }
        catch (Exception ex) when (ex is FileNotFoundException
            or FileLoadException
            or BadImageFormatException
            or TypeLoadException
            or MissingMemberException
            or TargetInvocationException
            or InvalidDataException)
        {
            throw new NotSupportedException("YMM4の更新でFFmpeg連携の依存関係が変更されたため、解析機能は現在使用できません。", ex);
        }
    }

    private static string InvokeString(Type locator, string name)
    {
        var method = locator.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(x => x.Name == name && x.GetParameters().Length == 0)
            ?? throw new MissingMethodException(locator.FullName, name);
        return method.Invoke(null, null) as string ?? throw new InvalidDataException($"{name} returned no path.");
    }

    public static FfmpegBackend CreateBackend()
    {
        var paths = Resolve();
        return new(paths.FfmpegPath, paths.FfprobePath);
    }
}
