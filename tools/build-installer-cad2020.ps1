param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$AutoCADInstallDir = ''
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$pluginProject = Join-Path $projectRoot 'CadLibraryManager.CAD2020.csproj'
$installerProject = Join-Path $projectRoot 'installer\CadLibraryManagerInstaller.csproj'
$payloadRoot = Join-Path $projectRoot 'installer\Payload'
$payloadBundleRoot = Join-Path $payloadRoot 'CadLibraryManager.bundle'
$payloadBundleContents = Join-Path $payloadBundleRoot 'Contents\Windows'
$bundleSource = Join-Path $projectRoot 'bundle\PackageContents-CAD2020.xml'
$installerOutput = Join-Path $projectRoot 'installer\dist-cad2020'

if (-not (Test-Path -LiteralPath $pluginProject)) {
    throw "CAD 2020 plugin project not found: $pluginProject"
}

if (-not (Test-Path -LiteralPath $installerProject)) {
    throw "Installer project not found: $installerProject"
}

if (-not (Test-Path -LiteralPath $bundleSource)) {
    throw "CAD 2020 PackageContents.xml not found: $bundleSource"
}

if ([string]::IsNullOrWhiteSpace($AutoCADInstallDir)) {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Autodesk\AutoCAD 2020'),
        'C:\Autodesk\AutoCAD_2020_Simplified_Chinese_Win_64bit_dlm\x64\acad\PF\Root'
    )

    foreach ($candidate in $candidates) {
        if ((Test-Path -LiteralPath $candidate) -and (Test-Path -LiteralPath (Join-Path $candidate 'AcMgd.dll'))) {
            $AutoCADInstallDir = $candidate
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($AutoCADInstallDir)) {
    throw 'AutoCADInstallDir was not provided and AutoCAD 2018-2024 API DLLs were not found. Pass -AutoCADInstallDir with the folder containing AcMgd.dll, AcDbMgd.dll, and AcCoreMgd.dll.'
}

foreach ($fileName in @('AcMgd.dll', 'AcDbMgd.dll', 'AcCoreMgd.dll')) {
    $referencePath = Join-Path $AutoCADInstallDir $fileName
    if (-not (Test-Path -LiteralPath $referencePath)) {
        throw "AutoCAD 2020 reference not found: $referencePath"
    }
}

& dotnet build $pluginProject -c $Configuration -p:AutoCADInstallDir="$AutoCADInstallDir"
if ($LASTEXITCODE -ne 0) {
    throw 'CAD 2020 plugin build failed.'
}

$pluginOutputRoot = Join-Path $projectRoot "bin\$Configuration"
$pluginDll = Get-ChildItem -LiteralPath $pluginOutputRoot -Recurse -File -Filter 'CadLibraryManager.dll' |
    Where-Object { $_.FullName -like "*\net48\*" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if ([string]::IsNullOrWhiteSpace($pluginDll) -or -not (Test-Path -LiteralPath $pluginDll)) {
    throw "CAD 2020 plugin DLL not found under: $pluginOutputRoot"
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
    throw 'CAD 2020 installer publish failed.'
}

$installerExe = Join-Path $installerOutput 'CadLibraryManagerInstaller.exe'
$cad2020InstallerExe = Join-Path $installerOutput 'CadLibraryManagerInstaller-CAD2020.exe'
if (-not (Test-Path -LiteralPath $installerExe)) {
    throw "Installer EXE not found: $installerExe"
}

Move-Item -LiteralPath $installerExe -Destination $cad2020InstallerExe -Force
"CAD 2018-2024 installer created: $cad2020InstallerExe"
