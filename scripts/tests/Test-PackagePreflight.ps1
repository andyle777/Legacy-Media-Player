$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
[xml]$properties = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = [string]$properties.Project.PropertyGroup.Version
$checkRoot = Join-Path ([IO.Path]::GetTempPath()) ("lmp-package-preflight-" + [Guid]::NewGuid().ToString('N'))
$folder = Join-Path $checkRoot "LegacyMediaPlayer-v$version-win-x64"
New-Item -ItemType Directory -Path $folder | Out-Null
Set-Content -LiteralPath (Join-Path $folder 'canary.txt') -Value 'preserve me'
# Scope the external build substitute to this script, never the caller's session.
function dotnet { throw 'Publish was attempted on an existing output folder.' }
try {
    & (Join-Path $repoRoot 'scripts/Package-Windows.ps1') -OutputDirectory $checkRoot -NoRestore
    throw 'Expected existing output refusal.'
}
catch {
    if ($_.Exception.Message -notmatch 'output already exists') { throw }
}
if ((Get-Content -LiteralPath (Join-Path $folder 'canary.txt') -Raw).Trim() -ne 'preserve me') { throw 'Existing content changed.' }
Write-Host 'Package preflight preserves existing output and does not invoke publish.'
