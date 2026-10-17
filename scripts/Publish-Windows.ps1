param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '../artifacts/LegacyMediaPlayer-win-x64'),
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$publishArguments = @('publish', (Join-Path $repoRoot 'src/LegacyMediaPlayer.App'), '-p:PublishProfile=Windows', '-o', $OutputPath)
if ($NoRestore) { $publishArguments += '--no-restore' }
dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
foreach ($relative in @('LegacyMediaPlayer.exe', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll',
    'libvlc/win-x64/libvlc.dll', 'libvlc/win-x64/libvlccore.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $OutputPath $relative))) { throw "Missing published dependency: $relative" }
}
$plugins = Get-ChildItem -LiteralPath (Join-Path $OutputPath 'libvlc/win-x64/plugins') -Filter '*.dll' -Recurse
if ($plugins.Count -lt 1) { throw 'LibVLC plugin tree is empty.' }
# Generate metadata against this exact published plugin tree, once per package.
# Normal app launches read this cache and never reset/rebuild it.
$cacheProcess = Start-Process -FilePath (Join-Path $OutputPath 'LegacyMediaPlayer.exe') -ArgumentList '--generate-plugin-cache' -PassThru -WindowStyle Hidden
if (!$cacheProcess.WaitForExit(180000)) {
    $cacheProcess.Kill()
    throw 'Plugin cache generation exceeded 180 seconds.'
}
$cachePath = Join-Path $OutputPath 'libvlc/win-x64/plugins/plugins.dat'
if ($cacheProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $cachePath) -or (Get-Item -LiteralPath $cachePath).Length -eq 0) {
    throw 'The published LibVLC plugin cache could not be generated.'
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/windows-player.md') -Destination (Join-Path $OutputPath 'START-HERE.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/video-verification.md') -Destination (Join-Path $OutputPath 'video-verification.md')
Write-Host "Ready: $(Join-Path $OutputPath 'LegacyMediaPlayer.exe') ($($plugins.Count) native plugins)"
