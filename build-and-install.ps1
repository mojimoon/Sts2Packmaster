# Build both mods and install them into the game's mods folder.
#   powershell -ExecutionPolicy Bypass -File build-and-install.ps1 [-GameDir <path>] [-Dotnet <path to dotnet.exe>]
# Defaults come from the STS2_DIR / STS2_DOTNET environment variables, then the values below.
param(
    [string]$Configuration = "Release",
    [string]$GameDir = $(if ($env:STS2_DIR) { $env:STS2_DIR } else { "D:\SteamLibrary\steamapps\common\Slay the Spire 2" }),
    [string]$Dotnet = $(if ($env:STS2_DOTNET) { $env:STS2_DOTNET } else { "dotnet" })
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$modsDir = Join-Path $GameDir "mods"

foreach ($mod in @("PackmasterLib", "VanillaPacks")) {
    & $Dotnet build (Join-Path $root "$mod\$mod.csproj") -c $Configuration "-p:Sts2Dir=$GameDir"
    if ($LASTEXITCODE -ne 0) { throw "build failed: $mod" }
    $dest = Join-Path $modsDir $mod
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $root "$mod\bin\$Configuration\net9.0\$mod.dll") $dest -Force
    Copy-Item (Join-Path $root "$mod\$mod.json") $dest -Force
    Write-Host "Installed $mod -> $dest"
}
Write-Host "Done. Launch the game and enable both mods in the modding screen."
