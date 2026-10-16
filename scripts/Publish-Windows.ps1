param([string]$OutputPath = (Join-Path $PSScriptRoot '../artifacts/LegacyMediaPlayer-win-x64'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
dotnet publish (Join-Path $repoRoot 'src/LegacyMediaPlayer.App') -p:PublishProfile=Windows -o $OutputPath
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
foreach ($relative in @('LegacyMediaPlayer.exe', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll',
    'libvlc/win-x64/libvlc.dll', 'libvlc/win-x64/libvlccore.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $OutputPath $relative))) { throw "Missing published dependency: $relative" }
}
$plugins = Get-ChildItem -LiteralPath (Join-Path $OutputPath 'libvlc/win-x64/plugins') -Filter '*.dll' -Recurse
if ($plugins.Count -lt 1) { throw 'LibVLC plugin tree is empty.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/windows-player.md') -Destination (Join-Path $OutputPath 'START-HERE.md')
Write-Host "Ready: $(Join-Path $OutputPath 'LegacyMediaPlayer.exe') ($($plugins.Count) native plugins)"
