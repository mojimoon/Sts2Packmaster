# Build both mods and install into the game's mods directory.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dotnet = "D:\env\dotnet-sdk-9\dotnet.exe"
$gameDir = "D:\SteamLibrary\steamapps\common\Slay the Spire 2"
$modsDir = Join-Path $gameDir "mods"

foreach ($mod in @("PackmasterLib", "VanillaPacks")) {
    & $dotnet build (Join-Path $root "$mod\$mod.csproj") -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "build failed: $mod" }
    $dest = Join-Path $modsDir $mod
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $root "$mod\bin\$Configuration\net9.0\$mod.dll") $dest -Force
    Copy-Item (Join-Path $root "$mod\$mod.json") $dest -Force
    Write-Host "Installed $mod -> $dest"
}
Write-Host "Done. Launch the game and enable both mods in the modding screen."
