param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference = 'Stop'
foreach ($relative in @('LegacyMediaPlayer.exe', 'LegacyMediaPlayer.dll',
    'LegacyMediaPlayer.deps.json', 'LegacyMediaPlayer.runtimeconfig.json',
    'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll',
    'libvlc/win-x64/libvlc.dll', 'libvlc/win-x64/libvlccore.dll',
    'libvlc/win-x64/plugins/plugins.dat', 'START-HERE.md', 'RELEASE-NOTES.md',
    'THIRD-PARTY-NOTICES.md', 'licenses/Avalonia-LICENSE.txt', 'licenses/Avalonia-NOTICE.txt',
    'licenses/MicroCom-LICENSE.txt', 'licenses/LibVLCSharp-LICENSE.txt',
    'licenses/LibVLC-COPYING.LIB.txt', 'licenses/LibVLC-COPYING.txt', 'licenses/LibVLC-AUTHORS.txt',
    'licenses/dotnet-LICENSE.txt', 'licenses/dotnet-THIRD-PARTY-NOTICES.txt',
    'licenses/skiasharp-LICENSE.txt', 'licenses/skiasharp-THIRD-PARTY-NOTICES.txt',
    'licenses/harfbuzzsharp-LICENSE.txt', 'licenses/harfbuzzsharp.nativeassets.win32-THIRD-PARTY-NOTICES.txt',
    'licenses/avalonia.angle.windows.natives-LICENSE.txt')) {
    $file = Get-Item -LiteralPath (Join-Path $Path $relative) -ErrorAction SilentlyContinue
    if (!$file -or $file.PSIsContainer -or $file.Length -eq 0) { throw "Missing/empty package dependency: $relative" }
}
$runtime = Get-Content -LiteralPath (Join-Path $Path 'LegacyMediaPlayer.runtimeconfig.json') -Raw | ConvertFrom-Json
if (!$runtime.runtimeOptions.includedFrameworks -or $runtime.runtimeOptions.framework -or $runtime.runtimeOptions.frameworks) {
    throw 'The package must include a self-contained runtime.'
}
$plugins = @(Get-ChildItem -LiteralPath (Join-Path $Path 'libvlc/win-x64/plugins') -Filter '*.dll' -Recurse)
if ($plugins.Count -lt 1) { throw 'LibVLC plugin tree is empty.' }
Write-Host "Package dependencies verified ($($plugins.Count) plugins): $Path"
