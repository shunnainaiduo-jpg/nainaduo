using Gssoft.Gscad.Windows;

namespace CadLibraryManager;

internal static class LibraryPalette
{
    private static PaletteSet? _paletteSet;

    public static void Toggle()
    {
        if (_paletteSet?.Visible == true)
        {
            Close();
            return;
        }

        Show();
    }

    public static void Show()
    {
        if (_paletteSet == null)
        {
            _paletteSet = new PaletteSet("CAD 图库管理")
            {
                DockEnabled = DockSides.Left | DockSides.Right,
                MinimumSize = new System.Drawing.Size(860, 560),
                Size = new System.Drawing.Size(980, 680)
            };

            _paletteSet.Add("图库", new LibraryControl());
        }

        _paletteSet.Visible = true;
    }

    public static void Close()
    {
        if (_paletteSet == null)
        {
            return;
        }

        _paletteSet.Visible = false;
    }
}
