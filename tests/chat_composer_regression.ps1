param([switch]$ExpectLocalPack)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $repo ('bin\chat-composer\' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($output)
$patterns = @('Models.cs', 'NetworkService*.cs', 'MainForm*.cs', 'Program.cs', 'Controllers\*.cs',
    'UI\Controls\*.cs', 'UI\Controls\SettingsPanels\*.cs', 'UI\Renderers\*.cs', 'Services\*.cs')
$sources = foreach ($pattern in $patterns) { (Get-ChildItem (Join-Path $repo "src\$pattern") -File).FullName }
$test = Join-Path $PSScriptRoot 'ChatComposerRegression.cs'
$flags = @('/nologo', '/codepage:65001', '/target:exe', '/main:LanChat.ChatComposerRegression',
    "/res:$(Join-Path $repo 'assets\WeChatEmoji.pack'),LanChat.WeChatEmoji.pack",
    '/r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll', "/out:$output\ChatComposerRegression.exe")
& $compiler @flags @sources $test
if ($LASTEXITCODE -ne 0) { throw 'Chat composer fixture compilation failed' }
& "$output\ChatComposerRegression.exe" $output $ExpectLocalPack.IsPresent.ToString()
if ($LASTEXITCODE -ne 0) { throw 'Chat composer regression failed' }
