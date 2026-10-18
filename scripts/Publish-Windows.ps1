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
# Preserve upstream license/notice texts, including the exact restored runtime.
$licensePath = Join-Path $OutputPath 'licenses'
New-Item -ItemType Directory -Path $licensePath -Force | Out-Null
Copy-Item -Path (Join-Path $repoRoot 'docs/notices/*') -Destination $licensePath
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/THIRD-PARTY-NOTICES.md') -Destination (Join-Path $OutputPath 'THIRD-PARTY-NOTICES.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $OutputPath 'LICENSE')
[xml]$properties = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = [string]$properties.Project.PropertyGroup.Version
Copy-Item -LiteralPath (Join-Path $repoRoot "docs/release-notes-$version.md") -Destination (Join-Path $OutputPath 'RELEASE-NOTES.md')
$assets = Get-Content -LiteralPath (Join-Path $repoRoot 'src/LegacyMediaPlayer.App/obj/project.assets.json') -Raw | ConvertFrom-Json
function Copy-PackageNotice([string]$id, [string]$version, [string]$file, [string]$name) {
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $source = Join-Path $folder "$id/$version/$file"
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $licensePath $name)
            return
        }
    }
    throw "Missing restored upstream notice: $id/$version/$file"
}
foreach ($spec in @(
    @{Id='skiasharp'; File='LICENSE.txt'},
    @{Id='skiasharp'; File='THIRD-PARTY-NOTICES.txt'},
    @{Id='harfbuzzsharp'; File='LICENSE.txt'},
    @{Id='harfbuzzsharp.nativeassets.win32'; File='THIRD-PARTY-NOTICES.txt'},
    @{Id='avalonia.angle.windows.natives'; File='LICENSE'})) {
    $key = @($assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like "$($spec.Id)/*" })
    if ($key.Count -ne 1) { throw "Cannot determine restored notice version: $($spec.Id)" }
    $version = $key[0].Split('/')[1]
    $name = "$($spec.Id)-$($spec.File)"
    if ($spec.File -eq 'LICENSE') { $name += '.txt' }
    Copy-PackageNotice $spec.Id $version $spec.File $name
}
$runtimeConfig = Get-Content -LiteralPath (Join-Path $OutputPath 'LegacyMediaPlayer.runtimeconfig.json') -Raw | ConvertFrom-Json
$runtimeVersion = @($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object { $_.name -eq 'Microsoft.NETCore.App' })[0].version
Copy-PackageNotice 'microsoft.netcore.app.runtime.win-x64' $runtimeVersion 'LICENSE.TXT' 'dotnet-LICENSE.txt'
Copy-PackageNotice 'microsoft.netcore.app.runtime.win-x64' $runtimeVersion 'THIRD-PARTY-NOTICES.TXT' 'dotnet-THIRD-PARTY-NOTICES.txt'

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
# Permit cache-only native startup even after ZIP timestamps change across timezones.
# Hash the exact runtime/cache and every x64 plugin; never omit codecs for size.
$nativeRoot = Join-Path $OutputPath 'libvlc/win-x64'
$fingerprints = @{}
$nativeFiles = @((Get-Item (Join-Path $nativeRoot 'libvlc.dll')), (Get-Item (Join-Path $nativeRoot 'libvlccore.dll')), (Get-Item $cachePath)) + @($plugins)
foreach ($file in $nativeFiles) {
    $relative = $file.FullName.Substring(([IO.Path]::GetFullPath($nativeRoot)).Length + 1).Replace('\', '/')
    $fingerprints[$relative] = @{
        Length = $file.Length
        Modified = ([DateTimeOffset]$file.LastWriteTimeUtc).ToUnixTimeSeconds()
        Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$fingerprints | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $nativeRoot 'plugins/lmp-cache.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/windows-player.md') -Destination (Join-Path $OutputPath 'START-HERE.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/video-verification.md') -Destination (Join-Path $OutputPath 'video-verification.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/drag-drop-verification.md') -Destination (Join-Path $OutputPath 'drag-drop-verification.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/fullscreen-verification.md') -Destination (Join-Path $OutputPath 'fullscreen-verification.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/startup-verification.md') -Destination (Join-Path $OutputPath 'startup-verification.md')
& (Join-Path $PSScriptRoot 'Test-WindowsPackage.ps1') -Path $OutputPath
Write-Host "Ready: $(Join-Path $OutputPath 'LegacyMediaPlayer.exe') ($($plugins.Count) native plugins)"
