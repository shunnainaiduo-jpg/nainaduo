using Gssoft.Gscad.Runtime;

namespace CadLibraryManager;

public sealed class Plugin : IExtensionApplication
{
    public void Initialize()
    {
    }

    public void Terminate()
    {
        LibraryPalette.Close();
    }
}
