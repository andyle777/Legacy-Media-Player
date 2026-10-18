param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference = 'Stop'
foreach ($relative in @('LegacyMediaPlayer.exe', 'LegacyMediaPlayer.dll',
    'LegacyMediaPlayer.deps.json', 'LegacyMediaPlayer.runtimeconfig.json',
    'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll',
    'libvlc/win-x64/libvlc.dll', 'libvlc/win-x64/libvlccore.dll',
    'libvlc/win-x64/plugins/plugins.dat', 'libvlc/win-x64/plugins/lmp-cache.json', 'START-HERE.md', 'RELEASE-NOTES.md',
    'LICENSE', 'THIRD-PARTY-NOTICES.md', 'licenses/Avalonia-LICENSE.txt', 'licenses/Avalonia-NOTICE.txt',
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
if ($plugins.Count -ne 321) { throw 'Expected all 321 pinned LibVLC x64 plugins.' }
foreach ($architecture in @('win-x86', 'win-arm64')) {
    if (Test-Path -LiteralPath (Join-Path $Path "libvlc/$architecture")) { throw "Unexpected unused LibVLC architecture: $architecture" }
}
$nativeRoot = [IO.Path]::GetFullPath((Join-Path $Path 'libvlc/win-x64'))
$fingerprints = Get-Content -LiteralPath (Join-Path $nativeRoot 'plugins/lmp-cache.json') -Raw | ConvertFrom-Json
$nativeFiles = @((Get-Item (Join-Path $nativeRoot 'libvlc.dll')), (Get-Item (Join-Path $nativeRoot 'libvlccore.dll')), (Get-Item (Join-Path $nativeRoot 'plugins/plugins.dat'))) + $plugins
if (@($fingerprints.PSObject.Properties).Count -ne $nativeFiles.Count) { throw 'Native fingerprint inventory differs.' }
foreach ($file in $nativeFiles) {
    $relative = $file.FullName.Substring($nativeRoot.Length + 1).Replace('\', '/')
    $expected = $fingerprints.PSObject.Properties[$relative].Value
    if (!$expected -or $file.Length -ne $expected.Length -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $expected.Sha256) {
        throw "Native fingerprint mismatch: $relative"
    }
}
$probe = Start-Process -FilePath (Join-Path $Path 'LegacyMediaPlayer.exe') -ArgumentList '--verify-plugin-cache' -PassThru -WindowStyle Hidden
if (!$probe.WaitForExit(15000)) { $probe.Kill(); throw 'Cache-only native startup verification timed out.' }
if ($probe.ExitCode -ne 0) { throw 'Cache-only native startup verification failed.' }
Write-Host "Package dependencies and cache-only native startup verified ($($plugins.Count) plugins): $Path"
