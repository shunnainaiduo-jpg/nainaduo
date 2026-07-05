param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$InstallRoot = '',

    [switch]$NoBuild,

    [switch]$EnableAutoload,

    [switch]$AllDetectedProfiles
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'CadLibraryManager.GstarCAD.csproj'

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $env:APPDATA 'Gstarsoft\GstarCAD\ApplicationPlugins\CadLibraryManager.GstarCAD'
}

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Project file not found: $projectFile"
}

if (-not $NoBuild) {
    dotnet restore $projectFile
    if ($LASTEXITCODE -ne 0) {
        throw 'Restore failed.'
    }

    dotnet build $projectFile -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Build failed.'
    }
}

$buildOutput = Join-Path $projectRoot "bin\$Configuration\net8.0-windows"
$pluginDll = Join-Path $buildOutput 'CadLibraryManager.GstarCAD.dll'
if (-not (Test-Path -LiteralPath $pluginDll)) {
    throw "Plugin DLL not found: $pluginDll"
}

if (Test-Path -LiteralPath $InstallRoot) {
    Remove-Item -LiteralPath $InstallRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null

Get-ChildItem -LiteralPath $buildOutput -File |
    Where-Object {
        $_.Name -in @(
            'CadLibraryManager.GstarCAD.dll',
            'CadLibraryManager.GstarCAD.pdb',
            'CadLibraryManager.GstarCAD.deps.json',
            'LiteDB.dll'
        )
    } |
    Copy-Item -Destination $InstallRoot -Force

$installedDll = Join-Path $InstallRoot 'CadLibraryManager.GstarCAD.dll'
$applicationsKeyName = 'Applications'
$appKeyName = 'CadLibraryManager.GstarCAD'

if (-not $EnableAutoload) {
    Write-Output "Installed files: $InstallRoot"
    Write-Output "Autoload was not enabled to avoid slowing or freezing GstarCAD startup."
    Write-Output "In GstarCAD, run NETLOAD and select: $installedDll"
    Write-Output "Then run W1."
    Write-Output "After manual NETLOAD is verified, rerun this script with -EnableAutoload if you want command-triggered autoload."
    exit 0
}

function Get-GstarCadProfileKeys {
    $root = 'HKCU:\SOFTWARE\Gstarsoft\GstarCAD'
    if (-not (Test-Path -LiteralPath $root)) {
        return @()
    }

    $keys = New-Object System.Collections.Generic.List[string]
    foreach ($versionKey in Get-ChildItem -LiteralPath $root -ErrorAction SilentlyContinue) {
        foreach ($profileKey in Get-ChildItem -LiteralPath $versionKey.PSPath -ErrorAction SilentlyContinue) {
            $keys.Add($profileKey.PSPath)
        }
    }

    return $keys.ToArray()
}

$profileKeys = @(Get-GstarCadProfileKeys)
if ($profileKeys.Count -eq 0) {
    Write-Warning 'No GstarCAD profile registry key was found under HKCU:\SOFTWARE\Gstarsoft\GstarCAD.'
    Write-Warning 'Files were installed, but autoload registry entries were not created. Start GstarCAD once, then run this installer again.'
    Write-Output "Installed files: $InstallRoot"
    Write-Output "Manual NETLOAD target: $installedDll"
    exit 0
}

if (-not $AllDetectedProfiles -and $profileKeys.Count -gt 1) {
    $profileKeys = @($profileKeys | Sort-Object | Select-Object -Last 1)
}

foreach ($profileKey in $profileKeys) {
    $applicationsKey = Join-Path $profileKey $applicationsKeyName
    $appKey = Join-Path $applicationsKey $appKeyName
    New-Item -Path $applicationsKey -Force | Out-Null
    New-Item -Path $appKey -Force | Out-Null
    New-ItemProperty -Path $appKey -Name 'DESCRIPTION' -PropertyType String -Value 'CAD library manager plugin for GstarCAD' -Force | Out-Null
    New-ItemProperty -Path $appKey -Name 'LOADCTRLS' -PropertyType DWord -Value 2 -Force | Out-Null
    New-ItemProperty -Path $appKey -Name 'Managed' -PropertyType DWord -Value 1 -Force | Out-Null
    New-ItemProperty -Path $appKey -Name 'Loader' -PropertyType String -Value $installedDll -Force | Out-Null

    $commandsKey = Join-Path $appKey 'Commands'
    New-Item -Path $commandsKey -Force | Out-Null
    New-ItemProperty -Path $commandsKey -Name 'W1' -PropertyType DWord -Value 1 -Force | Out-Null
    Write-Output "Registered command-triggered autoload: $appKey"
}

Write-Output "Installed files: $InstallRoot"
Write-Output 'Restart GstarCAD, then run W1.'
