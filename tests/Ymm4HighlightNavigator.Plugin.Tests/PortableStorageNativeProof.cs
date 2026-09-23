using System.IO;
using Ymm4HighlightNavigator.Core;
using Ymm4HighlightNavigator.Plugin;

namespace Ymm4HighlightNavigator.Plugin.Tests;

internal static class PortableStorageNativeProof
{
    internal static void Run(string output, Action<string, bool> check)
    {
        string plugin = Path.GetFullPath(Path.GetDirectoryName(typeof(NavigatorPlugin).Assembly.Location)!);
        check("portable_data_root_plugin_local",
            Path.GetFullPath(NavigatorStorage.PluginDirectory) == plugin
            && Path.GetFullPath(NavigatorStorage.DataDirectory) == Path.Combine(plugin, "Data")
            && Path.GetFullPath(NavigatorStorage.LearningDirectory) == Path.Combine(plugin, "Data", "Learning")
            && Path.GetFullPath(NavigatorStorage.ReviewDirectory) == Path.Combine(plugin, "Data", "Review")
            && !NavigatorStorage.DataDirectory.StartsWith(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(NavigatorStorage.LearningDirectory, NavigatorStorage.LegacyLearningDirectory, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(NavigatorStorage.ReviewDirectory, NavigatorStorage.LegacyReviewDirectory, StringComparison.OrdinalIgnoreCase));

        string? portableLearningBackup = MoveAside(NavigatorStorage.LearningDirectory);
        string? legacyLearningBackup = MoveAside(NavigatorStorage.LegacyLearningDirectory);
        string? portableReviewBackup = MoveAside(NavigatorStorage.ReviewDirectory);
        string? legacyReviewBackup = MoveAside(NavigatorStorage.LegacyReviewDirectory);
        try
        {
            Directory.CreateDirectory(NavigatorStorage.LegacyLearningDirectory);
            string legacySentinel = Path.Combine(NavigatorStorage.LegacyLearningDirectory, "migration-proof.txt");
            File.WriteAllText(legacySentinel, "learning-portable-proof");
            var corpus = NavigatorModel.UserCorpus();
            check("portable_learning_legacy_migration",
                Path.GetFullPath(corpus.Root) == Path.GetFullPath(NavigatorStorage.LearningDirectory)
                && File.ReadAllText(Path.Combine(corpus.Root, "migration-proof.txt")) == "learning-portable-proof"
                && File.ReadAllText(legacySentinel) == "learning-portable-proof"
                && !File.Exists(Path.Combine(corpus.Root, ".writer.lock"))
                && NavigatorStorage.MigratedLegacyLearningThisProcess);

            Directory.CreateDirectory(NavigatorStorage.LegacyReviewDirectory);
            var legacyReview = new ReviewSetStore(NavigatorStorage.LegacyReviewDirectory);
            var config = new ReviewConfiguration([new("seed.visual", true)], 1).Normalize();
            _ = legacyReview.SaveNew("Legacy Review", config, 0);
            byte[] legacyBefore = File.ReadAllBytes(Path.Combine(NavigatorStorage.LegacyReviewDirectory, "review-settings.json"));

            var migrated = NavigatorModel.UserReviewSettings().Read();
            check("portable_review_legacy_migration",
                migrated.Sets.Length == 1 && migrated.Sets[0].Name == "Legacy Review"
                && File.Exists(Path.Combine(NavigatorStorage.ReviewDirectory, "review-settings.json"))
                && File.ReadAllBytes(Path.Combine(NavigatorStorage.LegacyReviewDirectory, "review-settings.json")).SequenceEqual(legacyBefore)
                && NavigatorStorage.MigratedLegacyReviewThisProcess);

            _ = legacyReview.SaveNew("Legacy Changed Later", config, migrated.Revision);
            var portableStillWins = NavigatorModel.UserReviewSettings().Read();
            check("portable_existing_authority_wins",
                portableStillWins.Sets.Length == 1
                && portableStillWins.Sets[0].Name == "Legacy Review"
                && legacyReview.Read().Sets.Length == 2);

            string portableReviewFile = Path.Combine(NavigatorStorage.ReviewDirectory, "review-settings.json");
            File.WriteAllText(portableReviewFile, "{broken-portable");
            check("portable_corrupt_authority_no_legacy_fallback",
                Rejected(() => _ = NavigatorModel.UserReviewSettings().Read())
                && legacyReview.Read().Sets.Length == 2);

            DeleteDirectory(NavigatorStorage.ReviewDirectory);
            DeleteDirectory(NavigatorStorage.LegacyReviewDirectory);
            Directory.CreateDirectory(NavigatorStorage.LegacyReviewDirectory);
            File.WriteAllText(Path.Combine(NavigatorStorage.LegacyReviewDirectory, "review-settings.json"), "{broken-legacy");
            check("portable_corrupt_legacy_not_published",
                Rejected(() => _ = NavigatorModel.UserReviewSettings().Read())
                && !Directory.Exists(NavigatorStorage.ReviewDirectory));
        }
        finally
        {
            DeleteDirectory(NavigatorStorage.LearningDirectory);
            DeleteDirectory(NavigatorStorage.LegacyLearningDirectory);
            DeleteDirectory(NavigatorStorage.ReviewDirectory);
            DeleteDirectory(NavigatorStorage.LegacyReviewDirectory);
            Restore(portableLearningBackup, NavigatorStorage.LearningDirectory);
            Restore(legacyLearningBackup, NavigatorStorage.LegacyLearningDirectory);
            Restore(portableReviewBackup, NavigatorStorage.ReviewDirectory);
            Restore(legacyReviewBackup, NavigatorStorage.LegacyReviewDirectory);
        }

        File.WriteAllText(Path.Combine(output, "portable-storage.txt"),
            $"data={NavigatorStorage.DataDirectory}{Environment.NewLine}legacy={NavigatorStorage.LegacyBaseDirectory}{Environment.NewLine}");
    }

    private static bool Rejected(Action action)
    {
        try { action(); return false; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        { return true; }
    }

    private static string? MoveAside(string path)
    {
        if (!Directory.Exists(path)) return null;
        string backup = path + ".native-proof-backup-" + Guid.NewGuid().ToString("N");
        Directory.Move(path, backup);
        return backup;
    }

    private static void Restore(string? backup, string path)
    {
        if (backup != null && Directory.Exists(backup)) Directory.Move(backup, path);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
