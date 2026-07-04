using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

namespace CadLibraryManager;

internal sealed class LibraryDatabase : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<LibraryMetadata> _metadata;

    public LibraryDatabase()
    {
        Directory.CreateDirectory(LibrarySettings.SettingsFolder);
        _database = new LiteDatabase(LibrarySettings.DatabaseFile);
        _metadata = _database.GetCollection<LibraryMetadata>("library_items");
        _metadata.EnsureIndex(item => item.FilePath, unique: true);
        _metadata.EnsureIndex(item => item.Category);
        _metadata.EnsureIndex(item => item.IsFavorite);
        _metadata.EnsureIndex(item => item.LastUsedAt);
    }

    public LibraryMetadata GetOrCreate(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        var existing = _metadata.FindOne(item => item.FilePath == normalizedPath);
        if (existing != null)
        {
            return existing;
        }

        var metadata = CreateMetadata(normalizedPath);
        _metadata.Insert(metadata);
        return metadata;
    }

    public Dictionary<string, LibraryMetadata> GetMany(IEnumerable<string> filePaths)
    {
        var normalizedPaths = filePaths
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var metadataByPath = new Dictionary<string, LibraryMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in normalizedPaths.Chunk(500))
        {
            var existingItems = _metadata.Find(Query.In(nameof(LibraryMetadata.FilePath), chunk.Select(path => new BsonValue(path))));
            foreach (var metadata in existingItems)
            {
                metadataByPath[metadata.FilePath] = metadata;
            }
        }

        return metadataByPath;
    }

    public void Save(LibraryMetadata metadata)
    {
        metadata.FilePath = NormalizePath(metadata.FilePath);
        metadata.UpdatedAt = DateTime.Now;
        _metadata.Upsert(metadata);
    }

    public void RenamePath(string oldPath, string newPath, string displayName)
    {
        var metadata = GetOrCreate(oldPath);
        metadata.FilePath = NormalizePath(newPath);
        metadata.DisplayName = displayName;
        Save(metadata);
    }

    public void MarkAsUsed(string filePath)
    {
        var metadata = GetOrCreate(filePath);
        metadata.LastUsedAt = DateTime.Now;
        Save(metadata);
    }

    public void RemoveMissingFiles(string folder, IEnumerable<string> existingPaths)
    {
        var normalizedFolder = NormalizePath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var existing = new HashSet<string>(existingPaths.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);
        foreach (var metadata in _metadata.FindAll()
            .Where(item => IsUnderFolder(item.FilePath, normalizedFolder) && !existing.Contains(item.FilePath))
            .ToList())
        {
            _metadata.Delete(metadata.Id);
        }
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path).Trim();

    private static LibraryMetadata CreateMetadata(string normalizedPath) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        FilePath = normalizedPath,
        DisplayName = Path.GetFileNameWithoutExtension(normalizedPath),
        UpdatedAt = DateTime.Now
    };

    private static bool IsUnderFolder(string path, string normalizedFolder)
    {
        var normalizedPath = NormalizePath(path);
        return normalizedPath.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase);
    }
}
