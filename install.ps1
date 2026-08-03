param(
    [string]$InstallDir = "$env:LOCALAPPDATA\EASemanticModelToolkit",
    [switch]$BuildFromSource
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'EASemanticModelToolkit.csproj'
$publish = Join-Path $PSScriptRoot 'publish'
$prebuilt = Join-Path $PSScriptRoot 'prebuilt'
$prebuiltHost = Join-Path $prebuilt 'EASemanticModelToolkit.comhost.dll'
$hasProject = Test-Path -LiteralPath $project
$hasPrebuilt = Test-Path -LiteralPath $prebuiltHost

if ($BuildFromSource -and $hasProject) {
    dotnet publish $project -c Release --self-contained false -o $publish
    if ($LASTEXITCODE -ne 0) { throw "Building EASemanticModelToolkit failed with exit code $LASTEXITCODE." }
    $source = $publish
} elseif ($hasPrebuilt) {
    if ($BuildFromSource) {
        Write-Warning "-BuildFromSource was requested, but this binary package does not include the project source. Installing the included prebuilt add-in instead."
    }
    $source = $prebuilt
} elseif ($hasProject) {
    dotnet publish $project -c Release --self-contained false -o $publish
    if ($LASTEXITCODE -ne 0) { throw "Building EASemanticModelToolkit failed with exit code $LASTEXITCODE." }
    $source = $publish
} else {
    throw "This package contains neither prebuilt\EASemanticModelToolkit.comhost.dll nor EASemanticModelToolkit.csproj. Download the complete release package."
}
if (Test-Path $InstallDir) { Remove-Item -LiteralPath $InstallDir -Recurse -Force }
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $InstallDir -Recurse -Force

$clsid = '{6BDE33B3-C200-4DEC-B692-CBC4293E07F0}'
$progId = 'EASemanticModelToolkit.Addin'
$comHost = Join-Path $InstallDir 'EASemanticModelToolkit.comhost.dll'
New-Item "HKCU:\Software\Classes\CLSID\$clsid\InprocServer32" -Force | Out-Null
Set-Item "HKCU:\Software\Classes\CLSID\$clsid\InprocServer32" $comHost
New-ItemProperty "HKCU:\Software\Classes\CLSID\$clsid\InprocServer32" -Name ThreadingModel -Value Both -PropertyType String -Force | Out-Null
New-Item "HKCU:\Software\Classes\CLSID\$clsid\ProgID" -Force | Out-Null
Set-Item "HKCU:\Software\Classes\CLSID\$clsid\ProgID" $progId
New-Item "HKCU:\Software\Classes\$progId\CLSID" -Force | Out-Null
Set-Item "HKCU:\Software\Classes\$progId\CLSID" $clsid
New-Item 'HKCU:\Software\Sparx Systems\EAAddins64\EASemanticModelToolkit' -Force | Out-Null
Set-Item 'HKCU:\Software\Sparx Systems\EAAddins64\EASemanticModelToolkit' $progId
Write-Host "Installed to $InstallDir. Restart Enterprise Architect."

