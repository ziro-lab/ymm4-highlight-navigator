using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Ymm4HighlightNavigator.Core;

namespace Ymm4HighlightNavigator.Plugin;

public sealed record NavigatorStorageResolution(string Path, bool MigratedLegacy);

/// <summary>
/// Portable user-data root for Navigator. Existing plugin-local data is authoritative.
/// Legacy LocalAppData is only a one-time migration source and is retained as backup.
/// </summary>
public static class NavigatorStorage
{
    private static int learningMigrated, reviewMigrated;

    public static string PluginDirectory
    {
        get
        {
            string location = typeof(NavigatorPlugin).Assembly.Location;
            if (string.IsNullOrWhiteSpace(location))
                throw new InvalidOperationException("プラグインのインストール場所を取得できません。データは移行・保存していません。");
            return Path.GetFullPath(Path.GetDirectoryName(location)
                ?? throw new InvalidOperationException("プラグインのインストールフォルダーを取得できません。データは移行・保存していません。"));
        }
    }

    public static string DataDirectory => Path.Combine(PluginDirectory, "Data");
    public static string LearningDirectory => Path.Combine(DataDirectory, "Learning");
    public static string ReviewDirectory => Path.Combine(DataDirectory, "Review");

    public static string LegacyBaseDirectory
    {
        get
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
                throw new IOException("旧データの保存先を取得できません。");
            return Path.Combine(appData, "Ymm4HighlightNavigator");
        }
    }

    public static string LegacyLearningDirectory => Path.Combine(LegacyBaseDirectory, "Learning");
    public static string LegacyReviewDirectory => Path.Combine(LegacyBaseDirectory, "Review");
    public static bool MigratedLegacyLearningThisProcess => Volatile.Read(ref learningMigrated) != 0;
    public static bool MigratedLegacyReviewThisProcess => Volatile.Read(ref reviewMigrated) != 0;

    public static NavigatorStorageResolution PrepareLearning()
    {
        var result = PrepareDirectory(LearningDirectory, LegacyLearningDirectory, ValidateLearning);
        if (result.MigratedLegacy) Interlocked.Exchange(ref learningMigrated, 1);
        return result;
    }

    public static NavigatorStorageResolution PrepareReview()
    {
        var result = PrepareDirectory(ReviewDirectory, LegacyReviewDirectory, ValidateReview);
        if (result.MigratedLegacy) Interlocked.Exchange(ref reviewMigrated, 1);
        return result;
    }

    public static void OpenDataDirectory() => OpenDirectory(DataDirectory);

    public static void OpenDirectory(string path)
    {
        string target = Path.GetFullPath(path);
        Directory.CreateDirectory(target);
        RejectLinks(target);
        Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
    }

    private static NavigatorStorageResolution PrepareDirectory(string portableRoot, string legacyRoot, Action<string> validate)
    {
        portableRoot = Path.GetFullPath(portableRoot);
        legacyRoot = Path.GetFullPath(legacyRoot);
        if (string.Equals(portableRoot, legacyRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Portable保存先と旧保存先が同じです。");

        // Once the portable root exists it is the sole authority. Never fall back to legacy data.
        if (Directory.Exists(portableRoot)) return new(portableRoot, false);
        if (!Directory.Exists(legacyRoot)) return new(portableRoot, false);

        RejectLinks(PluginDirectory);
        Directory.CreateDirectory(DataDirectory);
        RejectLinks(DataDirectory);
        using var migrationLock = new FileStream(Path.Combine(DataDirectory, ".migration.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        if (Directory.Exists(portableRoot)) return new(portableRoot, false);
        if (!Directory.Exists(legacyRoot)) return new(portableRoot, false);

        RejectLinks(legacyRoot);
        string legacyWriterPath = Path.Combine(legacyRoot, ".writer.lock");
        using var legacyWriter = new FileStream(legacyWriterPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        // Reject broken legacy authority before copying anything into the portable root.
        validate(legacyRoot);

        string staging = portableRoot + ".migrate-" + Guid.NewGuid().ToString("N");
        try
        {
            CopyTreeVerified(legacyRoot, staging);
            validate(staging);
            if (Directory.Exists(portableRoot))
                throw new IOException("Portableデータが移行中に作成されました。既存のPortableデータを優先するため、旧データは移行していません。YMM4を開き直してください。");
            Directory.Move(staging, portableRoot);
            return new(portableRoot, true);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    private static void ValidateLearning(string root)
    {
        var store = new CorpusStore(root);
        var snapshot = store.Read();
        foreach (var sample in snapshot.Samples) _ = store.LoadPack(sample);
        _ = new FilterStore(store).ReadAll();
    }

    private static void ValidateReview(string root) => _ = new ReviewSetStore(root).Read();

    private static void CopyTreeVerified(string sourceRoot, string destinationRoot)
    {
        if (Directory.Exists(destinationRoot)) throw new IOException("移行用の一時保存先が既に存在します。");
        Directory.CreateDirectory(destinationRoot);
        CopyDirectory(sourceRoot, destinationRoot);
    }

    private static void CopyDirectory(string source, string destination)
    {
        RejectLinks(source);
        foreach (string entry in Directory.EnumerateFileSystemEntries(source))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("リンクを含むデータフォルダーは移行できません。");

            string name = Path.GetFileName(entry);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                string child = Path.Combine(destination, name);
                Directory.CreateDirectory(child);
                CopyDirectory(entry, child);
                continue;
            }

            // Locks and transactional leftovers are runtime state, not user authority.
            if (string.Equals(name, ".writer.lock", StringComparison.OrdinalIgnoreCase)
                || name.Contains(".tmp-", StringComparison.OrdinalIgnoreCase))
                continue;

            string target = Path.Combine(destination, name);
            using (var input = new FileStream(entry, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
                output.Flush(true);
            }
            if (!CryptographicOperations.FixedTimeEquals(FileHash(entry), FileHash(target)))
                throw new IOException("データ移行中にファイルが変更されました。旧データは保持しています。YMM4を開き直してください。");
        }
    }

    private static byte[] FileHash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return SHA256.HashData(stream);
    }

    private static void RejectLinks(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current != null)
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("リンク経由の保存先・データはこの版では対応していません。");
            current = Path.GetDirectoryName(current);
        }
    }
}
