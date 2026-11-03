param([string]$EdoPath)
. (Join-Path $PSScriptRoot 'common.ps1')

$root = Find-EDOPro $EdoPath
Write-Host "EDOPro folder: $root"
Stop-App

$target = Join-Path $root "addons\$AddonName"
if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force; Write-Host 'Removed the app' }
Remove-Shortcuts $root

$configPath = Join-Path $root 'config\user_configs.json'
if (Test-Path -LiteralPath $configPath) {
    $config = Read-Config $configPath
    if (@($config.repos | Where-Object { Test-OurRepo $_ }).Count -gt 0) {
        $config.repos = @($config.repos | Where-Object { -not (Test-OurRepo $_) })
        Save-Config $configPath $config
        Write-Host 'Removed the Genesys banlist from config\user_configs.json'
    }
}
$repo = Join-Path $root 'repositories\genesys'
if (Test-Path -LiteralPath $repo) {
    if (Get-Process EDOPro -ErrorAction SilentlyContinue) {
        Write-Host 'Close EDOPro, then delete repositories\genesys to remove the Genesys banlist files.' -ForegroundColor Yellow
    } else {
        Remove-Item -LiteralPath $repo -Recurse -Force; Write-Host 'Removed the Genesys banlist files'
    }
}
Write-Host 'Genesys Deck Points is uninstalled.' -ForegroundColor Green
