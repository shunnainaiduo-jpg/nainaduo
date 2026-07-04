param(
    [ValidateSet('User', 'AllUsers')]
    [string]$Scope = 'User'
)

$ErrorActionPreference = 'Stop'

$applicationPluginsRoot = if ($Scope -eq 'AllUsers') {
    Join-Path $env:ProgramData 'Autodesk\ApplicationPlugins'
} else {
    Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins'
}

$bundleRoot = Join-Path $applicationPluginsRoot 'CadLibraryManager.bundle'

if (Test-Path -LiteralPath $bundleRoot) {
    Remove-Item -LiteralPath $bundleRoot -Recurse -Force
    "Removed AutoCAD autoload bundle: $bundleRoot"
} else {
    "Autoload bundle is not installed: $bundleRoot"
}
