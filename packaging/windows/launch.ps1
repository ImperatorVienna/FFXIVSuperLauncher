$ErrorActionPreference = 'Stop'
$root = $env:FFXIV_LAUNCHER_ROOT
if (-not $root -or -not [IO.Directory]::Exists($root)) { throw 'Launcher directory is missing.' }
function Test-Launcher([string]$file) {
    if (-not [IO.File]::Exists($file)) { return $false }
    $item = Get-Item -LiteralPath $file
    if ($item.Extension -ine '.exe' -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { return $false }
    if ($item.Name -match '(\.previous|\.tmp|\.partial|\.download)(\.|$)') { return $false }
    return [Diagnostics.FileVersionInfo]::GetVersionInfo($file).ProductName -ceq 'FFXIV Super Launcher'
}
$target = $null
$record = Join-Path $root 'launcher-executable.txt'
if ([IO.File]::Exists($record)) {
    $name = [IO.File]::ReadAllText($record).Trim()
    if ($name -and [IO.Path]::GetFileName($name) -ceq $name) {
        $candidate = Join-Path $root $name
        if (Test-Launcher $candidate) { $target = $candidate }
    }
}
if (-not $target) {
    $candidates = @(Get-ChildItem -LiteralPath $root -Filter '*.exe' -File | Where-Object { Test-Launcher $_.FullName })
    if ($candidates.Count -ne 1) { throw 'Run the intended launcher EXE once to repair this shortcut.' }
    $target = $candidates[0].FullName
}
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $target
$start.WorkingDirectory = $root
$start.UseShellExecute = $false
$process = [Diagnostics.Process]::Start($start)
$process.WaitForExit()
exit $process.ExitCode
