namespace CadLibraryManager;

internal sealed class LibraryViewState
{
    public string RootFolder { get; set; } = string.Empty;

    public string CurrentFolder { get; set; } = string.Empty;

    public string SearchText { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public bool FavoriteOnly { get; set; }

    public bool RecentOnly { get; set; }

    public string SelectedFilePath { get; set; } = string.Empty;
}
