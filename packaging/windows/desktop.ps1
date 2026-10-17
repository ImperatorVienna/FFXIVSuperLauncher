$ErrorActionPreference = 'Stop'
$root = $env:FFXIV_LAUNCHER_ROOT
$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
if (-not $root -or -not [IO.Directory]::Exists($root) -or -not $desktop) { throw 'Launcher or desktop directory is missing.' }
$shell = New-Object -ComObject WScript.Shell
try {
    $shortcut = $shell.CreateShortcut((Join-Path $desktop 'FFXIV Super Launcher.lnk'))
    $shortcut.TargetPath = Join-Path $root 'launch.bat'
    $executable = [IO.File]::ReadAllText((Join-Path $root 'launcher-executable.txt')).Trim()
    if ([IO.Path]::GetFileName($executable) -ne $executable -or -not [IO.File]::Exists((Join-Path $root $executable))) { throw 'Launcher executable is missing.' }
    $shortcut.IconLocation = (Join-Path $root $executable) + ',0'
    $shortcut.WorkingDirectory = $root
    $shortcut.WindowStyle = 7
    $shortcut.Description = 'FFXIV Super Launcher'
    $shortcut.Save()
} finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
