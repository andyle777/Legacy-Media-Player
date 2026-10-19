param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string[]]$Media,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(1, 20)][int]$Samples = 3,
    [ValidateRange(1, 10)][int]$EvidenceSeconds = 3,
    [ValidateRange(5, 300)][int]$TimeoutSeconds = 150
)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$Media = @($Media | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Choose a new measurement output directory.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$previousLog = $env:LEGACY_MEDIA_PLAYER_STARTUP_LOG
$results = @()
try {
    for ($sample = 1; $sample -le $Samples; $sample++) {
        foreach ($path in $Media) {
            $name = "$sample-$([IO.Path]::GetFileName($path))"
            $log = Join-Path $OutputDirectory "$name.jsonl"
            $env:LEGACY_MEDIA_PLAYER_STARTUP_LOG = $log
            $info = New-Object Diagnostics.ProcessStartInfo
            $info.FileName = $Executable
            $info.UseShellExecute = $false
            $info.RedirectStandardError = $true
            $info.Arguments = '"' + (($path -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $player = [Diagnostics.Process]::Start($info)
            $nativeErrors = $player.StandardError.ReadToEndAsync()
            try {
                $windowMs = $null; $playingAt = $null; $events = @()
                while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
                    $player.Refresh()
                    if ($player.HasExited) { throw "Player exited early ($($player.ExitCode)): $name" }
                    if ($null -eq $windowMs -and $player.MainWindowHandle -ne 0) { $windowMs = $watch.Elapsed.TotalMilliseconds }
                    if (Test-Path -LiteralPath $log) {
                        # A writer may be appending its final line. Ignore that partial line.
                        $events = @(Get-Content -LiteralPath $log | ForEach-Object { try { $_ | ConvertFrom-Json } catch {} })
                        if ($null -eq $playingAt -and $events.stage -contains 'playback.playing') { $playingAt = $watch.Elapsed.TotalMilliseconds }
                        # Allow output/clock evidence to arrive, without equating it with pixels/sound.
                        if ($null -ne $playingAt -and $watch.Elapsed.TotalMilliseconds - $playingAt -ge $EvidenceSeconds * 1000) { break }
                    }
                    Start-Sleep -Milliseconds 25
                }
                if ($null -eq $playingAt) { throw "No Playing event in $TimeoutSeconds seconds: $name" }
                $stages = @{}; foreach ($event in $events) { if (!$stages.ContainsKey($event.stage)) { $stages[$event.stage] = [double]$event.processMs } }
                $result = [pscustomobject]@{
                    Sample=$sample; Media=[IO.Path]::GetFileName($path); ProcessId=$player.Id
                    WindowMs=[Math]::Round($windowMs,3); ProcessPlayingMs=$stages['playback.playing']
                    PreparePlayingMs=[Math]::Round($stages['playback.playing']-$stages['playback.prepare.begin'],3)
                    RequestPlayingMs=$(if ($stages.ContainsKey('playback.request')) { [Math]::Round($stages['playback.playing']-$stages['playback.request'],3) } else { $null })
                    Stages=$stages
                }
                $results += $result; $result | ConvertTo-Json -Depth 4 | Write-Output
                $results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Encoding UTF8
            }
            finally {
                # Only the process this trial created is closed; never other player instances.
                if (!$player.HasExited) {
                    [void]$player.CloseMainWindow()
                    if (!$player.WaitForExit(10000)) { $player.Kill(); throw "Trial process did not close: $name" }
                }
                [IO.File]::WriteAllText("$log.native.log", $nativeErrors.GetAwaiter().GetResult())
                $player.Dispose()
            }
        }
    }
}
finally { $env:LEGACY_MEDIA_PLAYER_STARTUP_LOG = $previousLog }
# These are fresh-process first-file trials in one OS session, not reboot-cold
# measurements. Playing, clock and Vout are not confirmations of audible/pixel output.
