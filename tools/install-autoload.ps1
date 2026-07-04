param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('User', 'AllUsers')]
    [string]$Scope = 'User',

    [string]$AutoCADInstallDir = '',

    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'CadLibraryManager.csproj'
$bundleSource = Join-Path $projectRoot 'bundle\PackageContents.xml'
$outputRoot = Join-Path $projectRoot "bin\$Configuration"
$outputDir = ''
$pluginDll = ''

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Project file not found: $projectFile"
}

if (-not (Test-Path -LiteralPath $bundleSource)) {
    throw "PackageContents.xml not found: $bundleSource"
}

if (-not $NoBuild) {
    if ([string]::IsNullOrWhiteSpace($AutoCADInstallDir)) {
        $autodeskRoot = Join-Path $env:ProgramFiles 'Autodesk'
        if (Test-Path -LiteralPath $autodeskRoot) {
            $AutoCADInstallDir = Get-ChildItem -LiteralPath $autodeskRoot -Directory |
                Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'AcMgd.dll') } |
                Sort-Object Name -Descending |
                Select-Object -First 1 -ExpandProperty FullName
        }

        if ([string]::IsNullOrWhiteSpace($AutoCADInstallDir)) {
            throw 'AutoCADInstallDir was not provided and no AutoCAD installation with AcMgd.dll was found under Program Files\Autodesk.'
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($AutoCADInstallDir)) {
        foreach ($fileName in @('AcMgd.dll', 'AcDbMgd.dll', 'AcCoreMgd.dll')) {
            $referencePath = Join-Path $AutoCADInstallDir $fileName
            if (-not (Test-Path -LiteralPath $referencePath)) {
                throw "AutoCAD reference not found: $referencePath"
            }
        }
    }

    $buildArgs = @('build', $projectFile, '-c', $Configuration)
    if (-not [string]::IsNullOrWhiteSpace($AutoCADInstallDir)) {
        $buildArgs += "-p:AutoCADInstallDir=$AutoCADInstallDir"
    }

    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw 'Build failed. Check AutoCADInstallDir and referenced AutoCAD DLLs.'
    }
}

$pluginDll = Get-ChildItem -LiteralPath $outputRoot -Recurse -File -Filter 'CadLibraryManager.dll' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if ([string]::IsNullOrWhiteSpace($pluginDll) -or -not (Test-Path -LiteralPath $pluginDll)) {
    throw "Plugin DLL not found under: $outputRoot. Build first or remove -NoBuild."
}

$outputDir = Split-Path -Parent $pluginDll

$applicationPluginsRoot = if ($Scope -eq 'AllUsers') {
    Join-Path $env:ProgramData 'Autodesk\ApplicationPlugins'
} else {
    Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins'
}

$bundleRoot = Join-Path $applicationPluginsRoot 'CadLibraryManager.bundle'
$bundleContents = Join-Path $bundleRoot 'Contents\Windows'

if (Test-Path -LiteralPath $bundleRoot) {
    Remove-Item -LiteralPath $bundleRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $bundleContents -Force | Out-Null
Copy-Item -LiteralPath $bundleSource -Destination (Join-Path $bundleRoot 'PackageContents.xml') -Force

Get-ChildItem -LiteralPath $outputDir -File |
    Where-Object { $_.Extension -in '.dll', '.pdb', '.config', '.json' } |
    Copy-Item -Destination $bundleContents -Force

"Installed AutoCAD autoload bundle: $bundleRoot"
"Restart AutoCAD, then run W1."
