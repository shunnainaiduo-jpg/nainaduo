param(
    [string]$InstallRoot = '',

    [switch]$KeepFiles
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $env:APPDATA 'Gstarsoft\GstarCAD\ApplicationPlugins\CadLibraryManager.GstarCAD'
}

$appKeyName = 'CadLibraryManager.GstarCAD'
$registryRoots = @(
    'HKCU:\SOFTWARE\Gstarsoft\GstarCAD',
    'HKLM:\SOFTWARE\Gstarsoft\GstarCAD',
    'HKCU:\SOFTWARE\WOW6432Node\Gstarsoft\GstarCAD',
    'HKLM:\SOFTWARE\WOW6432Node\Gstarsoft\GstarCAD'
)

foreach ($root in $registryRoots) {
    if (-not (Test-Path -LiteralPath $root)) {
        continue
    }

    foreach ($versionKey in Get-ChildItem -LiteralPath $root -ErrorAction SilentlyContinue) {
        foreach ($profileKey in Get-ChildItem -LiteralPath $versionKey.PSPath -ErrorAction SilentlyContinue) {
            $appKey = Join-Path (Join-Path $profileKey.PSPath 'Applications') $appKeyName
            if (Test-Path -LiteralPath $appKey) {
                Remove-Item -LiteralPath $appKey -Recurse -Force
                Write-Output "Removed autoload: $appKey"
            }
        }
    }
}

if (-not $KeepFiles -and (Test-Path -LiteralPath $InstallRoot)) {
    Remove-Item -LiteralPath $InstallRoot -Recurse -Force
    Write-Output "Removed files: $InstallRoot"
}

Write-Output 'GstarCAD autoload uninstall complete.'
