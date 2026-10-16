# Saves the "Shore Desk" Entra client secret to a protected file and checks it.
# Run in a normal (non-admin) PowerShell window:
#   powershell -ExecutionPolicy Bypass -File C:\Desk_app\scripts\Set-EntraSecret.ps1
# The secret is typed at a hidden prompt; it is never shown or written to the console.
# The app's client ID is read from src\Desk.Api\appsettings.json (AzureAd:ClientId).

$ErrorActionPreference = 'Stop'
$settings = Get-Content 'C:\Desk_app\src\Desk.Api\appsettings.json' -Raw | ConvertFrom-Json
$tenantId = $settings.AzureAd.TenantId
$clientId = $settings.AzureAd.ClientId
if (-not $clientId) { Write-Host 'The client ID is not in appsettings.json yet. Ask Claude to add it first.' -ForegroundColor Red; exit 1 }
$dir  = 'C:\DeskData\secrets'
$file = Join-Path $dir 'entra-client-secret.txt'

New-Item -ItemType Directory -Force -Path $dir | Out-Null

$secure = Read-Host -Prompt 'Paste the client secret VALUE (it will not be shown), then press Enter' -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $secret = ([Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)).Trim()
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

if ($secret.Length -lt 30) {
    Write-Host 'That is too short to be a client secret value. Did you copy the Secret ID instead of the Value? Nothing was saved.' -ForegroundColor Red
    exit 1
}

# Check the secret against Microsoft before saving it.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
try {
    Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" -Body @{
        client_id     = $clientId
        client_secret = $secret
        scope         = 'https://graph.microsoft.com/.default'
        grant_type    = 'client_credentials'
    } | Out-Null
} catch {
    Write-Host 'Microsoft did not accept that secret, so nothing was saved. Check you copied the Value column.' -ForegroundColor Red
    $secret = $null
    exit 1
}

[IO.File]::WriteAllText($file, $secret, (New-Object Text.ASCIIEncoding))

# Lock the folder down: this user, SYSTEM, Administrators (Install-Services adds read for the service).
$me = [Security.Principal.WindowsIdentity]::GetCurrent().Name
icacls $dir /inheritance:r /grant:r "${me}:(OI)(CI)F" 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' | Out-Null
if (Get-LocalUser svc-desk -ErrorAction SilentlyContinue) { icacls $dir /grant "$env:COMPUTERNAME\svc-desk:(OI)(CI)R" | Out-Null }

Write-Host "Secret saved to $file and Microsoft accepted it." -ForegroundColor Green
$secret = $null

# The app reads this secret when it starts, so restart the service that uses it (if installed).
if (Get-Service ShoreDeskWeb -ErrorAction SilentlyContinue) {
    try { Restart-Service ShoreDeskWeb -ErrorAction Stop; Write-Host 'Restarted ShoreDeskWeb so it uses the new value.' -ForegroundColor Green }
    catch { Write-Host 'Could not restart ShoreDeskWeb - ask Claude to restart it, or restart it in Services.' -ForegroundColor Yellow }
}
