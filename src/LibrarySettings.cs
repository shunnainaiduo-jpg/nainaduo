using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CadLibraryManager;

internal static class LibrarySettings
{
    public static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CadLibraryManager");

    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "library-folder.txt");
    private static readonly string RootsFile = Path.Combine(SettingsFolder, "library-roots.txt");
    private static readonly string ViewStateFile = Path.Combine(SettingsFolder, "view-state.json");

    public static readonly string DatabaseFile = Path.Combine(SettingsFolder, "library.db");

    public static string GetLibraryFolder()
    {
        if (File.Exists(SettingsFile))
        {
            var configuredPath = File.ReadAllText(SettingsFile).Trim();
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CadLibraryManager");
    }

    public static void SaveLibraryFolder(string folder)
    {
        Directory.CreateDirectory(SettingsFolder);
        var normalizedFolder = folder.Trim();
        File.WriteAllText(SettingsFile, normalizedFolder);
        SaveLibraryRoots(GetLibraryRoots().Append(normalizedFolder));
    }

    public static List<string> GetLibraryRoots()
    {
        if (!File.Exists(RootsFile))
        {
            return new List<string> { GetLibraryFolder() };
        }

        return File.ReadAllLines(RootsFile)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void SaveLibraryRoots(IEnumerable<string> roots)
    {
        Directory.CreateDirectory(SettingsFolder);
        var normalizedRoots = roots
            .Select(root => root.Trim())
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        File.WriteAllLines(RootsFile, normalizedRoots);
    }

    public static void RemoveLibraryRoot(string root)
    {
        var remainingRoots = GetLibraryRoots()
            .Where(existingRoot => !string.Equals(existingRoot, root, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (remainingRoots.Count == 0)
        {
            remainingRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CadLibraryManager"));
        }

        SaveLibraryRoots(remainingRoots);
        SaveLibraryFolder(remainingRoots[0]);
    }

    public static LibraryViewState GetViewState()
    {
        if (!File.Exists(ViewStateFile))
        {
            return new LibraryViewState();
        }

        try
        {
            return JsonSerializer.Deserialize<LibraryViewState>(File.ReadAllText(ViewStateFile)) ?? new LibraryViewState();
        }
        catch
        {
            return new LibraryViewState();
        }
    }

    public static void SaveViewState(LibraryViewState state)
    {
        Directory.CreateDirectory(SettingsFolder);
        File.WriteAllText(ViewStateFile, JsonSerializer.Serialize(state));
    }
}
