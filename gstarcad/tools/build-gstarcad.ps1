param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputFolder = ''
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'CadLibraryManager.GstarCAD.csproj'

if ([string]::IsNullOrWhiteSpace($OutputFolder)) {
    $OutputFolder = Join-Path $projectRoot 'dist\GstarCAD 2026 (net8.0)'
}

dotnet restore $projectFile
if ($LASTEXITCODE -ne 0) {
    throw 'Restore failed.'
}

dotnet build $projectFile -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}

$buildOutput = Join-Path $projectRoot "bin\$Configuration\net8.0-windows"
if (-not (Test-Path -LiteralPath $buildOutput)) {
    throw "Build output not found: $buildOutput"
}

New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null

Get-ChildItem -LiteralPath $buildOutput -File |
    Where-Object {
        $_.Name -in @(
            'CadLibraryManager.GstarCAD.dll',
            'CadLibraryManager.GstarCAD.pdb',
            'CadLibraryManager.GstarCAD.deps.json',
            'LiteDB.dll'
        )
    } |
    Copy-Item -Destination $OutputFolder -Force

"GstarCAD build output: $OutputFolder"
"Load CadLibraryManager.GstarCAD.dll with NETLOAD, then run W1."
