param([string]$EdoPath, [switch]$NoDesktopShortcut)
. (Join-Path $PSScriptRoot 'common.ps1')

$root = Find-EDOPro $EdoPath
Write-Host "EDOPro folder: $root"
Stop-App

# 1. The app
$source = Join-Path (Split-Path -Parent $PSScriptRoot) 'addon'
$target = Join-Path $root "addons\$AddonName"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
Get-ChildItem -LiteralPath $target -Recurse -File | Unblock-File
Write-Host "Installed the app in $target"

# 2. The Genesys banlist repository, which EDOPro downloads and updates on each launch
$configPath = Join-Path $root 'config\user_configs.json'
$config = Read-Config $configPath
if (-not ($config.PSObject.Properties.Name -contains 'repos')) { $config | Add-Member repos @() }
if (@($config.repos | Where-Object { Test-OurRepo $_ }).Count -eq 0) {
    $config.repos = @($config.repos) + [pscustomobject]@{
        url = $RepoUrl; repo_name = 'Genesys banlist'; repo_path = './repositories/genesys'
        lflist_path = 'lflists'; should_update = $true; should_read = $true
    }
    Save-Config $configPath $config
    Write-Host 'Added the Genesys banlist to config\user_configs.json (old copy saved as .bak)'
} else {
    Write-Host 'The Genesys banlist is already in config\user_configs.json'
}

# 3. Shortcuts that open the app together with EDOPro
$shell = New-Object -ComObject WScript.Shell
$places = @($root); if (-not $NoDesktopShortcut) { $places += [Environment]::GetFolderPath('Desktop') }
foreach ($place in $places) {
    $link = $shell.CreateShortcut((Join-Path $place $ShortcutName))
    $link.TargetPath = Join-Path $target 'GenesysDeckPoints.exe'
    $link.Arguments = '--start-edopro'
    $link.WorkingDirectory = $target
    $link.IconLocation = (Join-Path $root 'EDOPro.exe') + ',0'
    $link.Description = 'EDOPro with the Genesys deck points window'
    $link.Save()
}
Write-Host "Created '$($ShortcutName -replace '\.lnk$')' shortcuts"

Write-Host ''
if (Get-Process EDOPro -ErrorAction SilentlyContinue) {
    Write-Host 'EDOPro is open: close it and start it again so it downloads the Genesys banlist.' -ForegroundColor Yellow
} else {
    Write-Host 'Done. Open "Genesys Deck Points" to start EDOPro and the points window.' -ForegroundColor Green
}
Write-Host 'In the deck editor, pick "Genesys" in the Ban List dropdown. The points window updates each time you save a deck.'
