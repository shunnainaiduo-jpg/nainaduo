namespace CadLibraryManager;

internal sealed class InsertOptions
{
    public double Scale { get; set; } = 1.0;

    public double RotationDegrees { get; set; }

    public bool RotateOnInsert { get; set; }

    public string LayerName { get; set; } = string.Empty;

    public bool RepeatPlacement { get; set; }

    public bool InsertAsBlock { get; set; } = true;

    public bool AllowExplode { get; set; }

    public bool EditAttributes { get; set; }

    public bool UseCurrentLayer { get; set; } = true;

    public bool CreateUniqueBlockOnConflict { get; set; }
}
