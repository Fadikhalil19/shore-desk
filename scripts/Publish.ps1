# Builds Shore Desk and copies it to C:\DeskApp, where the Windows Services run from.
# Needs no admin: Install-Services.ps1 lets this account stop/start the two services
# and write to C:\DeskApp.
#   powershell -ExecutionPolicy Bypass -File C:\Desk_app\scripts\Publish.ps1
#
# Steps: tests -> web screens -> database update -> build -> stop services -> copy -> start.
# If any step fails, nothing after it runs and the running app is left as it was.

$ErrorActionPreference = 'Stop'
$root     = 'C:\Desk_app'
$target   = 'C:\DeskApp'
$services = 'ShoreDeskWeb', 'ShoreDeskScheduler'

Write-Host 'Running tests...'
dotnet test "$root\Desk.slnx" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Tests failed - nothing was published.' }

Write-Host 'Building web screens...'
Push-Location "$root\src\Desk.Web"
if (-not (Test-Path node_modules)) { npm install --no-audit --no-fund }
npm run build
if ($LASTEXITCODE -ne 0) { Pop-Location; throw 'Web build failed.' }
Pop-Location

# Database changes only ever add things; this account is SQL sysadmin, the service account is not.
Write-Host 'Updating the database...'
Push-Location $root
dotnet tool restore | Out-Null
dotnet ef database update --project src\Desk.Core --startup-project src\Desk.Api
if ($LASTEXITCODE -ne 0) { Pop-Location; throw 'Database update failed - nothing was published.' }
Pop-Location

$staging = Join-Path $env:TEMP 'desk-publish'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
dotnet publish "$root\src\Desk.Api" -c Release -o "$staging\api" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'API publish failed.' }
dotnet publish "$root\src\Desk.Scheduler" -c Release -o "$staging\scheduler" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Scheduler publish failed.' }

# Stop, copy, start. If a service is not installed yet, just copy.
$installed = $services | Where-Object { Get-Service $_ -ErrorAction SilentlyContinue }
foreach ($s in $installed) { Stop-Service $s -ErrorAction Stop; Write-Host "Stopped $s" }

if (-not (Test-Path $target)) {
    # First publish: don't inherit C:\ (where every signed-in account may write).
    # Install-Services.ps1 later adds read access for the service account.
    New-Item -ItemType Directory $target | Out-Null
    $me = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    icacls $target /inheritance:r /grant:r "${me}:(OI)(CI)F" 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not lock down $target." }
}
foreach ($part in 'api', 'scheduler') {
    New-Item -ItemType Directory -Force "$target\$part" | Out-Null
    robocopy "$staging\$part" "$target\$part" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copy to $target\$part failed (robocopy $LASTEXITCODE)." }
}

foreach ($s in $installed) { Start-Service $s -ErrorAction Stop; Write-Host "Started $s" }
Remove-Item $staging -Recurse -Force

if ($installed -contains 'ShoreDeskWeb') {
    Start-Sleep -Seconds 8
    $code = & curl.exe -sk -o NUL -w '%{http_code}' https://localhost:5444/health
    if ($code -ne '200') { Write-Host "Warning: web service did not answer yet (got '$code')." -ForegroundColor Yellow }
}
Write-Host "Published to $target." -ForegroundColor Green
