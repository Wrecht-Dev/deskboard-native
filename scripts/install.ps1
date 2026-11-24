# Deskboard Native - Windows installer
#   irm https://raw.githubusercontent.com/Wrecht-Dev/deskboard-native/main/scripts/install.ps1 | iex
param(
    [string]$Version = "latest",
    [string]$InstallDir = "$env:LOCALAPPDATA\Deskboard",
    [string]$LocalZip = ""
)
$ErrorActionPreference = "Stop"
$Repo = "Wrecht-Dev/deskboard-native"
$Asset = "deskboard-native-win-x64.zip"

$stage = Join-Path $env:TEMP ("deskboard-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    if ($LocalZip) {
        $zip = $LocalZip
    } else {
        $url = if ($Version -eq "latest") {
            "https://github.com/$Repo/releases/latest/download/$Asset"
        } else {
            "https://github.com/$Repo/releases/download/$Version/$Asset"
        }
        $zip = Join-Path $stage $Asset
        Write-Host "Downloading $Asset..."
        Invoke-WebRequest -Uri $url -OutFile $zip
    }

    $payload = Join-Path $stage "payload"
    Expand-Archive -LiteralPath $zip -DestinationPath $payload
    $inner = Join-Path $payload "deskboard-native-win-x64"
    if (-not (Test-Path -LiteralPath (Join-Path $inner "Deskboard.App.exe") -PathType Leaf)) {
        throw "Archive is missing Deskboard.App.exe."
    }

    $InstallDir = [System.IO.Path]::GetFullPath($InstallDir).TrimEnd([char[]]@('\', '/'))
    $backup = "$InstallDir.old-$([guid]::NewGuid().ToString('N'))"
    Write-Host "Installing to $InstallDir"
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $InstallDir) | Out-Null
    if (Test-Path -LiteralPath $InstallDir) {
        if (-not (Test-Path -LiteralPath (Join-Path $InstallDir "Deskboard.App.exe") -PathType Leaf)) {
            throw "Choose an empty installation path or an existing Deskboard installation."
        }
        Move-Item -LiteralPath $InstallDir -Destination $backup
    }
    try {
        Move-Item -LiteralPath $inner -Destination $InstallDir
    } catch {
        if (Test-Path -LiteralPath $backup) {
            Move-Item -LiteralPath $backup -Destination $InstallDir
        }
        throw
    }
    if (Test-Path -LiteralPath $backup) {
        Remove-Item -LiteralPath $backup -Recurse -Force
    }
} finally {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

$exe = Join-Path $InstallDir "Deskboard.App.exe"
# Start menu shortcut
$startMenu = [Environment]::GetFolderPath("Programs")
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startMenu "Deskboard.lnk"))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $InstallDir
$shortcut.IconLocation = $exe
$shortcut.Save()

# Add to user PATH
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$userPath;$InstallDir", "User")
}

Write-Host "Deskboard Native installed. Launch it from the Start Menu or: $exe" -ForegroundColor Green
