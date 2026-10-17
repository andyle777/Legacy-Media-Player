param(
    [Parameter(Mandatory)][string]$Executable,
    [string[]]$PlayerArguments = @(),
    [string]$TimingLog,
    [int]$TimeoutSeconds = 150
)
$ErrorActionPreference = 'Stop'
$resolvedExecutable = (Resolve-Path -LiteralPath $Executable).Path
if ($TimingLog) {
    $env:LEGACY_MEDIA_PLAYER_STARTUP_LOG = [IO.Path]::GetFullPath($TimingLog)
}
# An external process-to-window measurement, not a claim of audible readiness.
# The player is left open for manual testing. Close it before measuring the next run.
$watch = [Diagnostics.Stopwatch]::StartNew()
$start = @{ FilePath = $resolvedExecutable; PassThru = $true }
if ($PlayerArguments.Count) {
    # Start-Process joins its array into one Windows command line, so quote each
    # argument (including embedded quotes and trailing backslashes) explicitly.
    $start.ArgumentList = ($PlayerArguments | ForEach-Object {
        '"' + (($_ -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
    }) -join ' '
}
$player = Start-Process @start
while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    $player.Refresh()
    if ($player.HasExited) { throw "Player exited before a window appeared (code $($player.ExitCode))." }
    if ($player.MainWindowHandle -ne 0) {
        [pscustomobject]@{
            ProcessId = $player.Id
            MainWindowMilliseconds = [Math]::Round($watch.Elapsed.TotalMilliseconds, 3)
            Title = $player.MainWindowTitle
            Executable = $resolvedExecutable
        }
        return
    }
    Start-Sleep -Milliseconds 20
}
throw "No main window within $TimeoutSeconds seconds. Player process ID: $($player.Id)."
