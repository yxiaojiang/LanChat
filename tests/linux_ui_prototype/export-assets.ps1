param([Parameter(Mandatory = $true)][string]$Output)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$outputPath = New-Item -ItemType Directory -Path $Output -Force
$executable = Join-Path $outputPath.FullName 'ExportAssets.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$generator = Join-Path $PSScriptRoot 'ExportAssets.cs'
$sourcePatterns = @('src\*.cs', 'src\Controllers\*.cs', 'src\Services\*.cs', 'src\UI\Renderers\*.cs',
    'src\UI\Controls\*.cs', 'src\UI\Controls\SettingsPanels\*.cs')
$sources = @($sourcePatterns | ForEach-Object { Get-ChildItem -Path (Join-Path $repo $_) -File } |
    Select-Object -ExpandProperty FullName)
$arguments = @('/nologo', '/main:ExportAssets', '/r:System.Drawing.dll,System.Windows.Forms.dll',
    "/out:$executable", $generator) + $sources
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $executable $outputPath.FullName
exit $LASTEXITCODE
