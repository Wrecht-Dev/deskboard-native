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

if ($LocalZip) {
    $zip = $LocalZip
} else {
    $url = if ($Version -eq "latest") {
        "https://github.com/$Repo/releases/latest/download/$Asset"
    } else {
        "https://github.com/$Repo/releases/download/$Version/$Asset"
    }
    $zip = Join-Path $env:TEMP $Asset
    Write-Host "Downloading $Asset..."
    Invoke-WebRequest -Uri $url -OutFile $zip
}

$stage = Join-Path $env:TEMP ("deskboard-" + [guid]::NewGuid().ToString("N"))
Write-Host "Installing to $InstallDir"
if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $stage
$inner = Get-ChildItem $stage -Directory | Select-Object -First 1
New-Item -ItemType Directory -Force -Path (Split-Path $InstallDir) | Out-Null
Move-Item $inner.FullName $InstallDir
Remove-Item $stage -Recurse -Force

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
