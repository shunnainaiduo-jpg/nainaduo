using Autodesk.AutoCAD.Runtime;

namespace CadLibraryManager;

public sealed class Commands
{
    [CommandMethod("W1")]
    public void ShowLibrary()
    {
        LibraryPalette.Toggle();
    }
}
