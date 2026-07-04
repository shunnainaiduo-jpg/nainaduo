using System;

namespace CadLibraryManager;

internal sealed class LibraryMetadata
{
    public string Id { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
