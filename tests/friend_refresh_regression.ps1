$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $repo ('bin\friend-refresh-round2\' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($output)
$encoding = [Text.UTF8Encoding]::new($false)

function Write-FixtureSource($name, $source, $replacements) {
    $contents = [IO.File]::ReadAllText((Join-Path $repo "src\$source"))
    foreach ($replacement in $replacements.GetEnumerator()) {
        if (!$contents.Contains($replacement.Key)) { throw "Missing fixture source marker in $source" }
        $contents = $contents.Replace($replacement.Key, $replacement.Value)
    }
    $path = Join-Path $output $name
    [IO.File]::WriteAllText($path, $contents, $encoding)
    return $path
}

# 先按应用入口编译生产源码，再为夹具单独生成仅回环的源码副本。
$patterns = @('Models.cs', 'NetworkService*.cs', 'MainForm*.cs', 'Program.cs', 'Controllers\*.cs',
    'UI\Controls\*.cs', 'UI\Controls\SettingsPanels\*.cs', 'UI\Renderers\*.cs', 'Services\*.cs')
$sources = foreach ($pattern in $patterns) { (Get-ChildItem (Join-Path $repo "src\$pattern") -File).FullName }
$compilerOptions = @('/nologo', '/codepage:65001', '/r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll')
$icon = Join-Path $repo 'assets\app.ico'
& $compiler @compilerOptions /target:winexe /main:LanChat.Program "/win32icon:$icon" "/res:$icon,LanChat.app.ico" "/res:$(Join-Path $repo 'assets\WeChatEmoji.pack'),LanChat.WeChatEmoji.pack" "/out:$output\LanChat.exe" @sources
if ($LASTEXITCODE -ne 0) { throw 'Native application compilation failed' }
Write-Output 'Native Windows compile: ok'

$udp = [Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback, 0))
$tcp = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
try {
    $udpPort = $udp.Client.LocalEndPoint.Port
    $tcp.Start()
    $tcpPort = $tcp.LocalEndpoint.Port
} finally {
    $udp.Dispose()
    $tcp.Stop()
}
$fixtureSources = @(
    Write-FixtureSource 'NetworkService.cs' 'NetworkService.cs' @{
        'UDP_PORT = 38888' = "UDP_PORT = $udpPort"
        'TCP_BASE_PORT = 38889' = "TCP_BASE_PORT = $tcpPort"
        'NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;' = ''
    }
    Write-FixtureSource 'NetworkService.Interface.cs' 'NetworkService.Interface.cs' @{
        'private static List<NetworkInterfaceDetail> DiscoverInterfaces()' = 'private static List<NetworkInterfaceDetail> DiscoverInterfaces() { return FriendRefreshRegression.FixtureInterfaces(); } private static List<NetworkInterfaceDetail> DiscoverHostInterfaces()'
        'BindUdpListener(IPAddress.Any)' = 'BindUdpListener(IPAddress.Loopback)'
    }
    Write-FixtureSource 'NetworkService.Discovery.cs' 'NetworkService.Discovery.cs' @{
        'SendDatagram(socket, data, IPAddress.Broadcast, UDP_PORT);' = ''
    }
    Write-FixtureSource 'NetworkService.Transfer.cs' 'NetworkService.Transfer.cs' @{
        'new TcpListener(IPAddress.Any, port)' = 'new TcpListener(IPAddress.Loopback, port)'
    }
)
$sources = @($sources | Where-Object { (Split-Path $_ -Leaf) -notin @($fixtureSources | ForEach-Object { Split-Path $_ -Leaf }) })
$fixture = Join-Path $PSScriptRoot 'FriendRefreshRegression.cs'
& $compiler @compilerOptions /target:exe /main:LanChat.FriendRefreshRegression "/out:$output\FriendRefreshRegression.exe" @sources @fixtureSources $fixture
if ($LASTEXITCODE -ne 0) { throw 'Regression fixture compilation failed' }
& "$output\FriendRefreshRegression.exe"
if ($LASTEXITCODE -ne 0) { throw 'Friend refresh regression failed' }
