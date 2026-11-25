param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $repo ('bin\file-transfer-stability\' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($output)
$patterns = @('Models.cs', 'NetworkService*.cs')
$sources = foreach ($pattern in $patterns) { (Get-ChildItem (Join-Path $repo "src\$pattern") -File).FullName }
$test = Join-Path $PSScriptRoot 'FileTransferStabilityRegression.cs'
$flags = @('/nologo', '/codepage:65001', '/target:exe', '/main:FileTransferStabilityRegression',
    '/r:System.dll,System.Core.dll', "/out:$output\FileTransferStabilityRegression.exe")
& $compiler @flags @sources $test
if ($LASTEXITCODE -ne 0) { throw 'File transfer stability fixture compilation failed' }
& "$output\FileTransferStabilityRegression.exe" $output
if ($LASTEXITCODE -ne 0) { throw 'File transfer stability regression failed' }
