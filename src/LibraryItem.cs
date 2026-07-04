using System;
using System.Collections.Generic;
using System.IO;

namespace CadLibraryManager;

internal sealed class LibraryItem
{
    public LibraryItem(string filePath, LibraryMetadata metadata)
    {
        FilePath = filePath;
        Metadata = metadata;
        RefreshCaches();
    }

    public string FilePath { get; }

    public LibraryMetadata Metadata { get; }

    public string FileName { get; private set; } = string.Empty;

    public string SearchText { get; private set; } = string.Empty;

    public string[] TagList { get; private set; } = Array.Empty<string>();

    public string Name => string.IsNullOrWhiteSpace(Metadata.DisplayName)
        ? FileName
        : Metadata.DisplayName;

    public string Category => Metadata.Category ?? string.Empty;

    public string Tags => Metadata.Tags ?? string.Empty;

    public bool IsFavorite => Metadata.IsFavorite;

    public void RefreshCaches()
    {
        FileName = Path.GetFileNameWithoutExtension(FilePath);
        TagList = SplitTags(Tags);
        SearchText = string.Join("\n", Name, Category, Tags, FileName, FilePath);
    }

    public override string ToString() => IsFavorite ? $"\u2605 {Name}" : Name;

    private static string[] SplitTags(string tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return Array.Empty<string>();
        }

        var parts = tags.Split(new[] { ',', '\uFF0C' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result.ToArray();
    }
}
