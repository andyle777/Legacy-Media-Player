$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$json = dotnet msbuild (Join-Path $repoRoot 'src/LegacyMediaPlayer.App') -nologo -p:PublishProfile=Windows -target:CollectVlcFilesToCopyWindows -getItem:Content
if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate Windows native content.' }
$items = ($json -join "`n" | ConvertFrom-Json).Items.Content
$links = @($items | ForEach-Object { $_.Link -replace '\\', '/' })
if (@($links | Where-Object { $_ -match '^libvlc/win-(x86|arm64)/' }).Count) {
    throw 'Windows x64 publish includes unused x86/ARM64 LibVLC trees.'
}
if (@($links | Where-Object { $_ -match '^libvlc/win-x64/plugins/.+\.dll$' }).Count -ne 321) {
    throw 'Expected the complete pinned x64 plugin tree.'
}
Write-Host 'Windows publish selects all 321 x64 plugins and no foreign architecture trees.'
