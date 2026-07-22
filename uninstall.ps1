param([string]$InstallDir = "$env:LOCALAPPDATA\EASemanticModelToolkit")
$ErrorActionPreference = 'Stop'
$clsid = '{6BDE33B3-C200-4DEC-B692-CBC4293E07F0}'
$progId = 'EASemanticModelToolkit.Addin'
$paths = @(
    'HKCU:\Software\Sparx Systems\EAAddins64\EASemanticModelToolkit',
    "HKCU:\Software\Classes\$progId",
    "HKCU:\Software\Classes\CLSID\$clsid"
)
foreach ($path in $paths) {
    if (Test-Path $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}
if (Test-Path $InstallDir) { Remove-Item -LiteralPath $InstallDir -Recurse -Force }
Write-Host 'Semantic Model Toolkit uninstalled.'

