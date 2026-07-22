param(
    [string]$EAInstallDir = 'C:\Program Files\Sparx Systems\EA',
    [string]$InstallDir = "$env:LOCALAPPDATA\EA17LinkMLExporter",
    [switch]$BuildFromSource
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'EA17LinkMLExporter.csproj'
$publish = Join-Path $PSScriptRoot 'publish'
$prebuilt = Join-Path $PSScriptRoot 'prebuilt'
if ($BuildFromSource -or -not (Test-Path (Join-Path $prebuilt 'EA17LinkMLExporter.comhost.dll'))) {
    dotnet publish $project -c Release --self-contained false -p:EAInstallDir="$EAInstallDir" -o $publish
    $source = $publish
} else {
    $source = $prebuilt
}
if (Test-Path $InstallDir) { Remove-Item -LiteralPath $InstallDir -Recurse -Force }
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $InstallDir -Recurse -Force

$clsid = '{82F1E748-F71A-4AC9-B12A-53EF197C2EF8}'
$progId = 'EA17LinkMLExporter.Addin'
$comHost = Join-Path $InstallDir 'EA17LinkMLExporter.comhost.dll'
New-Item "HKCU:\Software\Classes\CLSID\$clsid\InprocServer32" -Force | Out-Null
Set-Item "HKCU:\Software\Classes\CLSID\$clsid\InprocServer32" $comHost
New-ItemProperty "HKCU:\Software\Classes\CLSID\$clsid\InprocServer32" -Name ThreadingModel -Value Both -PropertyType String -Force | Out-Null
New-Item "HKCU:\Software\Classes\CLSID\$clsid\ProgID" -Force | Out-Null
Set-Item "HKCU:\Software\Classes\CLSID\$clsid\ProgID" $progId
New-Item "HKCU:\Software\Classes\$progId\CLSID" -Force | Out-Null
Set-Item "HKCU:\Software\Classes\$progId\CLSID" $clsid
New-Item 'HKCU:\Software\Sparx Systems\EAAddins64\EA17LinkMLExporter' -Force | Out-Null
Set-Item 'HKCU:\Software\Sparx Systems\EAAddins64\EA17LinkMLExporter' $progId
# Remove registration created by version 1.0 of this installer; 64-bit EA does not scan it.
if (Test-Path 'HKCU:\Software\Sparx Systems\EAAddins\EA17LinkMLExporter') {
    Remove-Item -LiteralPath 'HKCU:\Software\Sparx Systems\EAAddins\EA17LinkMLExporter' -Recurse -Force
}
Write-Host "Installed to $InstallDir. Restart Enterprise Architect."
