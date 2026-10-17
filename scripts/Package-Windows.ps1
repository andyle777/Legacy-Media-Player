param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts'),
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
[xml]$properties = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = [string]$properties.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+-[0-9A-Za-z.-]+$') { throw 'Expected an explicit prerelease Version in Directory.Build.props.' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$name = "LegacyMediaPlayer-v$version-win-x64"
$publish = Join-Path $OutputDirectory $name
$zip = Join-Path $OutputDirectory "$name.zip"
$verify = Join-Path $OutputDirectory "verified-$name"
if ((Test-Path -LiteralPath $publish) -or (Test-Path -LiteralPath $zip) -or (Test-Path -LiteralPath $verify)) {
    throw 'Package/publish/verification output already exists. Choose a new clean OutputDirectory; existing releases are never overwritten.'
}
$arguments = @{OutputPath=$publish}
if ($NoRestore) { $arguments.NoRestore = $true }
& (Join-Path $PSScriptRoot 'Publish-Windows.ps1') @arguments
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($publish, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
[IO.Compression.ZipFile]::ExtractToDirectory($zip, $verify)
$relocated = Join-Path $verify $name
& (Join-Path $PSScriptRoot 'Test-WindowsPackage.ps1') -Path $relocated
$sourceFiles = @(Get-ChildItem -LiteralPath $publish -File -Recurse)
$copyFiles = @(Get-ChildItem -LiteralPath $relocated -File -Recurse)
if ($sourceFiles.Count -ne $copyFiles.Count) { throw 'Extracted package file count differs.' }
foreach ($file in $sourceFiles) {
    $relative = $file.FullName.Substring($publish.Length + 1)
    $copy = Get-Item -LiteralPath (Join-Path $relocated $relative)
    if ($file.Length -ne $copy.Length -or
        (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $copy.FullName -Algorithm SHA256).Hash) {
        throw "Extracted file integrity check failed: $relative"
    }
    # The native cache includes plugin mtimes; ZIP extraction must preserve them too.
    if ($relative -like 'libvlc*plugins*' -and $file.Extension -eq '.dll' -and
        $file.LastWriteTimeUtc -ne $copy.LastWriteTimeUtc) {
        throw "Plugin timestamp changed in ZIP: $relative"
    }
}
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$zip.sha256" -Value "$hash  $name.zip" -Encoding ASCII
Write-Host "ZIP and relocated dependency/integrity checks passed ($($sourceFiles.Count) files)."
[pscustomobject]@{
    Version=$version; Zip=$zip; Bytes=(Get-Item -LiteralPath $zip).Length; SHA256=$hash
    Executable=(Join-Path $publish 'LegacyMediaPlayer.exe')
    ExtractedExecutable=(Join-Path $relocated 'LegacyMediaPlayer.exe')
}
