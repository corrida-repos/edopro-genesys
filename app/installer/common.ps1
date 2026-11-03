$ErrorActionPreference = 'Stop'
$RepoUrl = 'https://github.com/corrida-repos/edopro-genesys'
$AddonName = 'genesys-deck-points'
$ShortcutName = 'Genesys Deck Points.lnk'

function Find-EDOPro([string]$Given) {
    if ($Given) {
        if (Test-Path -LiteralPath (Join-Path $Given 'EDOPro.exe')) { return (Resolve-Path -LiteralPath $Given).Path }
        throw "EDOPro.exe was not found in $Given"
    }
    # Extracted inside the EDOPro folder, or somewhere next to it
    $dir = $PSScriptRoot
    for ($i = 0; $i -lt 4 -and $dir; $i++) {
        if (Test-Path -LiteralPath (Join-Path $dir 'EDOPro.exe')) { return $dir }
        $dir = Split-Path -Parent $dir
    }
    # Running EDOPro
    $running = Get-Process EDOPro -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($running -and $running.Path) { return (Split-Path -Parent $running.Path) }
    # Usual install places
    $candidates = @('C:\ProjectIgnis', "$env:USERPROFILE\ProjectIgnis", "$env:USERPROFILE\Desktop\ProjectIgnis",
        "$env:USERPROFILE\Documents\ProjectIgnis", "$env:USERPROFILE\Downloads\ProjectIgnis", "$env:ProgramFiles\ProjectIgnis",
        "${env:ProgramFiles(x86)}\ProjectIgnis", 'D:\ProjectIgnis', 'E:\ProjectIgnis')
    foreach ($c in $candidates) { if ($c -and (Test-Path -LiteralPath (Join-Path $c 'EDOPro.exe'))) { return $c } }
    # Ask
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Choose your EDOPro folder (the one that contains EDOPro.exe)'
    while ($dialog.ShowDialog() -eq 'OK') {
        if (Test-Path -LiteralPath (Join-Path $dialog.SelectedPath 'EDOPro.exe')) { return $dialog.SelectedPath }
        [System.Windows.Forms.MessageBox]::Show('That folder has no EDOPro.exe. Pick the folder EDOPro is installed in.', 'Genesys Deck Points') | Out-Null
    }
    throw 'No EDOPro folder chosen.'
}

function Stop-App {
    Get-Process GenesysDeckPoints -ErrorAction SilentlyContinue | ForEach-Object {
        $_.CloseMainWindow() | Out-Null
        if (-not $_.WaitForExit(5000)) { Stop-Process -Id $_.Id -Force }
    }
}

function Read-Config([string]$Path) {
    if (Test-Path -LiteralPath $Path) { return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json) }
    return [pscustomobject]@{ repos = @(); urls = @(); servers = @() }
}

function Save-Config([string]$Path, $Config) {
    if (Test-Path -LiteralPath $Path) { Copy-Item -LiteralPath $Path -Destination "$Path.bak" -Force }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $json = $Config | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function Test-OurRepo($Repo) { return ($Repo.url -replace '\.git$', '').TrimEnd('/') -ieq $RepoUrl }

function Remove-Shortcuts([string]$Root) {
    foreach ($s in @((Join-Path $Root $ShortcutName), (Join-Path ([Environment]::GetFolderPath('Desktop')) $ShortcutName))) {
        if (Test-Path -LiteralPath $s) { Remove-Item -LiteralPath $s -Force }
    }
}
