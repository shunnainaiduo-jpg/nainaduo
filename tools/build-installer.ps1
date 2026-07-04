param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$AutoCADInstallDir = ''
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$pluginProject = Join-Path $projectRoot 'CadLibraryManager.csproj'
$installerProject = Join-Path $projectRoot 'installer\CadLibraryManagerInstaller.csproj'
$payloadRoot = Join-Path $projectRoot 'installer\Payload'
$payloadBundleRoot = Join-Path $payloadRoot 'CadLibraryManager.bundle'
$payloadBundleContents = Join-Path $payloadBundleRoot 'Contents\Windows'
$bundleSource = Join-Path $projectRoot 'bundle\PackageContents.xml'
$installerOutput = Join-Path $projectRoot 'installer\dist'

if (-not (Test-Path -LiteralPath $pluginProject)) {
    throw "Plugin project not found: $pluginProject"
}

if (-not (Test-Path -LiteralPath $installerProject)) {
    throw "Installer project not found: $installerProject"
}

if (-not (Test-Path -LiteralPath $bundleSource)) {
    throw "PackageContents.xml not found: $bundleSource"
}

if ([string]::IsNullOrWhiteSpace($AutoCADInstallDir)) {
    $autodeskRoot = Join-Path $env:ProgramFiles 'Autodesk'
    if (Test-Path -LiteralPath $autodeskRoot) {
        $AutoCADInstallDir = Get-ChildItem -LiteralPath $autodeskRoot -Directory |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'AcMgd.dll') } |
            Sort-Object Name -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }
}

$buildArgs = @('build', $pluginProject, '-c', $Configuration)
if (-not [string]::IsNullOrWhiteSpace($AutoCADInstallDir)) {
    $buildArgs += "-p:AutoCADInstallDir=$AutoCADInstallDir"
}

& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw 'Plugin build failed.'
}

$pluginOutputRoot = Join-Path $projectRoot "bin\$Configuration"
$pluginDll = Get-ChildItem -LiteralPath $pluginOutputRoot -Recurse -File -Filter 'CadLibraryManager.dll' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if ([string]::IsNullOrWhiteSpace($pluginDll) -or -not (Test-Path -LiteralPath $pluginDll)) {
    throw "Plugin DLL not found under: $pluginOutputRoot"
}

$pluginOutputDir = Split-Path -Parent $pluginDll

if (Test-Path -LiteralPath $payloadRoot) {
    Remove-Item -LiteralPath $payloadRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $payloadBundleContents -Force | Out-Null
Copy-Item -LiteralPath $bundleSource -Destination (Join-Path $payloadBundleRoot 'PackageContents.xml') -Force

Get-ChildItem -LiteralPath $pluginOutputDir -File |
    Where-Object { $_.Extension -in '.dll', '.pdb', '.config', '.json' } |
    Copy-Item -Destination $payloadBundleContents -Force

if (Test-Path -LiteralPath $installerOutput) {
    Remove-Item -LiteralPath $installerOutput -Recurse -Force
}

& dotnet publish $installerProject -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $installerOutput

if ($LASTEXITCODE -ne 0) {
    throw 'Installer publish failed.'
}

$installerExe = Join-Path $installerOutput 'CadLibraryManagerInstaller.exe'
if (-not (Test-Path -LiteralPath $installerExe)) {
    throw "Installer EXE not found: $installerExe"
}

"Installer created: $installerExe"
