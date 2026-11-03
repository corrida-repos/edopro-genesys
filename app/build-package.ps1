# Builds dist\GenesysDeckPoints.zip: the app, its installer and sqlite3.dll from an EDOPro install.
param([string]$EdoPath = 'C:\ProjectIgnis')
$ErrorActionPreference = 'Stop'
$app = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$stage = Join-Path $app 'dist\GenesysDeckPoints'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path "$stage\addon\src", "$stage\installer" | Out-Null

$common = @('/nologo', '/platform:x86', '/optimize+', '/reference:System.Web.Extensions.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', "/win32manifest:$app\src\app.manifest")
& $compiler @common /target:winexe "/out:$stage\addon\GenesysDeckPoints.exe" "$app\src\GenesysDeckPoints.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& $compiler @common /target:exe "/out:$stage\addon\GenesysDeckPoints.Console.exe" "$app\src\GenesysDeckPoints.cs"
if ($LASTEXITCODE -ne 0) { throw 'Console build failed.' }

$sqlite = Get-ChildItem -Path $EdoPath -Filter sqlite3.dll -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch 'genesys' } | Select-Object -First 1
if (-not $sqlite) { throw "sqlite3.dll not found under $EdoPath (EDOPro's WindBot folder has one)." }
Copy-Item $sqlite.FullName "$stage\addon\"
Copy-Item "$app\src\*" "$stage\addon\src\"
Copy-Item "$app\APP-README.md" "$stage\addon\README.md"
Copy-Item "$app\installer\*" "$stage\installer\"
Copy-Item "$app\Install.bat", "$app\Uninstall.bat", "$app\README.txt" $stage

$zip = Join-Path $app 'dist\GenesysDeckPoints.zip'
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, 'Optimal', $true)
Write-Host "Built $zip"
