# Builds the web screens and starts Shore Desk on https://localhost:5444 for testing.
# Close the window to stop it. Needs the Entra sign-in secret (scripts\Set-EntraSecret.ps1).
#   powershell -ExecutionPolicy Bypass -File C:\Desk_app\scripts\Start-App.ps1

$ErrorActionPreference = 'Stop'
$root = 'C:\Desk_app'

Write-Host 'Building web screens...'
Push-Location "$root\src\Desk.Web"
if (-not (Test-Path node_modules)) { npm install --no-audit --no-fund }
npm run build
Pop-Location

Write-Host 'Starting Shore Desk at https://localhost:5444 (close this window to stop).'
Push-Location "$root\src\Desk.Api"
dotnet run --launch-profile https
Pop-Location
