param([string]$InstallDir = "$env:LOCALAPPDATA\EAJsonModelImporter")
$ErrorActionPreference = 'Stop'
$prebuilt = Join-Path $PSScriptRoot 'prebuilt'
if (-not (Test-Path (Join-Path $prebuilt 'EAJsonModelImporter.comhost.dll'))) {
    dotnet publish (Join-Path $PSScriptRoot 'EAJsonModelImporter.csproj') -c Release -o $prebuilt
}
if (Test-Path $InstallDir) { Remove-Item -LiteralPath $InstallDir -Recurse -Force }
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item (Join-Path $prebuilt '*') $InstallDir -Recurse -Force
$clsid='{C9D3AA11-5139-4F2E-BA00-58AAE6B1DB06}'; $progId='EAJsonModelImporter.Addin'
$comHost=Join-Path $InstallDir 'EAJsonModelImporter.comhost.dll'
New-Item "HKCU:\Software\Classes\CLSID\$clsid\InprocServer32" -Force | Out-Null
Set-Item "HKCU:\Software\Classes\CLSID\$clsid\InprocServer32" $comHost
New-ItemProperty "HKCU:\Software\Classes\CLSID\$clsid\InprocServer32" -Name ThreadingModel -Value Both -Force | Out-Null
New-Item "HKCU:\Software\Classes\CLSID\$clsid\ProgID" -Force | Out-Null; Set-Item "HKCU:\Software\Classes\CLSID\$clsid\ProgID" $progId
New-Item "HKCU:\Software\Classes\$progId\CLSID" -Force | Out-Null; Set-Item "HKCU:\Software\Classes\$progId\CLSID" $clsid
New-Item 'HKCU:\Software\Sparx Systems\EAAddins64\EAJsonModelImporter' -Force | Out-Null
Set-Item 'HKCU:\Software\Sparx Systems\EAAddins64\EAJsonModelImporter' $progId
Write-Host 'Installed. Restart Enterprise Architect.'
