# Nullscent Setup Script
# Run this script to prepare the game for first launch

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "   Nullscent Setup Script" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Create Songs directory
if (!(Test-Path "Songs")) {
    New-Item -ItemType Directory -Path "Songs" | Out-Null
    Write-Host "[✓] Created Songs/ directory" -ForegroundColor Green
} else {
    Write-Host "[✓] Songs/ directory already exists" -ForegroundColor Yellow
}

# Create Skins directory
if (!(Test-Path "Skins")) {
    New-Item -ItemType Directory -Path "Skins" | Out-Null
    Write-Host "[✓] Created Skins/ directory" -ForegroundColor Green
} else {
    Write-Host "[✓] Skins/ directory already exists" -ForegroundColor Yellow
}

# Create default settings.json if it doesn't exist
$settingsPath = "settings.json"
if (!(Test-Path $settingsPath)) {
    $defaultSettings = @{
        ScrollSpeed = 20
        GlobalAudioOffset = 0.0
        MasterVolume = 0.8
        MusicVolume = 0.7
        HitsoundVolume = 0.5
        HitPosition = 410
        DownScroll = $false
        ShowHitLighting = $true
        ShowKeyOverlay = $true
        ShowFPS = $false
        BackgroundDim = 0.8
        CurrentSkinPath = ""
        WindowWidth = 1280
        WindowHeight = 720
        Fullscreen = $false
        VSync = $true
    }

    $defaultSettings | ConvertTo-Json -Depth 10 | Set-Content -Path $settingsPath -Encoding UTF8
    Write-Host "[✓] Created default settings.json" -ForegroundColor Green
} else {
    Write-Host "[✓] settings.json already exists" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Setup complete!" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Place your osu!mania beatmaps in the Songs/ folder" -ForegroundColor White
Write-Host "2. (Optional) Place skins in the Skins/ folder" -ForegroundColor White
Write-Host "3. Run the game with: dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "Read INSTRUCTIONS.md for detailed setup guide" -ForegroundColor Cyan
Write-Host ""
