# One-time setup: installs Shore Desk as two Windows Services running under the
# low-privilege account svc-desk. Run ONCE in an ADMINISTRATOR PowerShell:
#   powershell -ExecutionPolicy Bypass -File C:\Desk_app\scripts\Install-Services.ps1
#
# What it does (each step prints OK or stops with an error):
#  1. Creates the local account svc-desk (Users group only) with a long random password
#     nobody ever sees, and gives it the "log on as a service" right.
#  2. Sets folder permissions: the service can read secrets and write only its data folders.
#  3. Creates a local HTTPS certificate (desk.shorehotelapps.com) for the web service.
#  4. Registers event-log sources so the services can log errors.
#  5. Gives svc-desk read/write on the Desk database (nothing else in SQL Server), and lets
#     SQL Server write nightly backups to C:\DeskData\backup.
#  6. Installs both services (automatic start, restart on failure, after SQL Server).
#  7. Lets the operator account (Claude's shell) start/stop them and publish updates.
#  8. Starts both services and checks the web service answers.
# Safe to run again: existing items are left in place. Does not touch the contract app.

$ErrorActionPreference = 'Stop'
$svcUser  = 'svc-desk'
$svcFull  = "$env:COMPUTERNAME\$svcUser"
$operator = "$env:COMPUTERNAME\Fadi Khalil"
$app      = 'C:\DeskApp'
$data     = 'C:\DeskData'
$sqlSvc   = 'MSSQL$SQLEXPRESS'
$sqlInst  = 'localhost\SQLEXPRESS'

function Step($n, $text) { Write-Host ""; Write-Host "[$n] $text" -ForegroundColor Cyan }
function Ok($text) { Write-Host "    OK - $text" -ForegroundColor Green }

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Please run this in an ADMINISTRATOR PowerShell window (right-click PowerShell > Run as administrator).'
}
foreach ($exe in "$app\api\Desk.Api.exe", "$app\scheduler\Desk.Scheduler.exe") {
    if (-not (Test-Path $exe)) { throw "Missing $exe - ask Claude to run scripts\Publish.ps1 first." }
}
if (-not (Test-Path "$data\secrets\entra-client-secret.txt")) {
    throw 'The Entra sign-in secret is not saved yet - run scripts\Set-EntraSecret.ps1 first.'
}

# ---------------------------------------------------------------------------
Step 1 'Service account, password and "log on as a service" right'

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
public static class LsaRights {
    [StructLayout(LayoutKind.Sequential)] struct LSA_UNICODE_STRING { public ushort Length, MaximumLength; public IntPtr Buffer; }
    [StructLayout(LayoutKind.Sequential)] struct LSA_OBJECT_ATTRIBUTES { public int Length; public IntPtr RootDirectory, ObjectName; public int Attributes; public IntPtr SecurityDescriptor, SecurityQualityOfService; }
    [DllImport("advapi32.dll")] static extern uint LsaOpenPolicy(IntPtr system, ref LSA_OBJECT_ATTRIBUTES attr, int access, out IntPtr handle);
    [DllImport("advapi32.dll")] static extern uint LsaAddAccountRights(IntPtr handle, byte[] sid, LSA_UNICODE_STRING[] rights, int count);
    [DllImport("advapi32.dll")] static extern uint LsaClose(IntPtr handle);
    [DllImport("advapi32.dll")] static extern int LsaNtStatusToWinError(uint status);
    public static void Grant(string account, string right) {
        var sid = (SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));
        var bytes = new byte[sid.BinaryLength]; sid.GetBinaryForm(bytes, 0);
        var attr = new LSA_OBJECT_ATTRIBUTES(); IntPtr h;
        uint r = LsaOpenPolicy(IntPtr.Zero, ref attr, 0x00000800 | 0x00000010, out h);
        if (r != 0) throw new System.ComponentModel.Win32Exception(LsaNtStatusToWinError(r));
        var s = new LSA_UNICODE_STRING { Buffer = Marshal.StringToHGlobalUni(right), Length = (ushort)(right.Length * 2), MaximumLength = (ushort)(right.Length * 2 + 2) };
        try { r = LsaAddAccountRights(h, bytes, new[] { s }, 1); if (r != 0) throw new System.ComponentModel.Win32Exception(LsaNtStatusToWinError(r)); }
        finally { Marshal.FreeHGlobal(s.Buffer); LsaClose(h); }
    }
}
'@

$chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#%^*-_=+'.ToCharArray()
$bytes = New-Object byte[] 40
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$plain = -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })
$securePw = ConvertTo-SecureString $plain -AsPlainText -Force
$plain = $null
if (Get-LocalUser $svcUser -ErrorAction SilentlyContinue) {
    Set-LocalUser -Name $svcUser -Password $securePw -PasswordNeverExpires $true -UserMayChangePassword $false
} else {
    New-LocalUser -Name $svcUser -Password $securePw -PasswordNeverExpires -UserMayNotChangePassword `
        -Description 'Runs the Shore Desk services' | Out-Null
    Add-LocalGroupMember -Group 'Users' -Member $svcUser
}
$cred = New-Object Management.Automation.PSCredential(".\$svcUser", $securePw)
[LsaRights]::Grant($svcFull, 'SeServiceLogonRight')
Ok "$svcUser ready (new random password, not shown or stored) with log-on-as-service"

# ---------------------------------------------------------------------------
Step 2 'Folder permissions'

foreach ($d in "$app", "$app\api", "$app\scheduler", "$data\files", "$data\outbox", "$data\keys", "$data\status", "$data\backup", "$data\secrets") {
    New-Item -ItemType Directory -Force -Path $d | Out-Null
}
# Program folder: operator can update it, service can only read and run it.
icacls $app /grant "${operator}:(OI)(CI)M" "${svcFull}:(OI)(CI)RX" | Out-Null
# Data folders the services write to.
foreach ($d in "$data\files", "$data\outbox", "$data\keys", "$data\status") {
    icacls $d /grant "${svcFull}:(OI)(CI)M" | Out-Null
}
# Secrets: read only.
icacls "$data\secrets" /grant "${svcFull}:(OI)(CI)R" | Out-Null
# Top folder: allow the service to see into it (this folder only).
icacls $data /grant "${svcFull}:(RX)" | Out-Null
Ok 'program folder read-only for the service; files/outbox/keys/status writable; secrets read-only'

# ---------------------------------------------------------------------------
Step 3 'HTTPS certificate for the web service'

$subject = 'CN=desk.shorehotelapps.com'
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date).AddDays(30) } | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -DnsName 'desk.shorehotelapps.com', 'localhost' `
        -CertStoreLocation Cert:\LocalMachine\My -NotAfter (Get-Date).AddYears(5) `
        -KeyExportPolicy NonExportable -FriendlyName 'Shore Desk (web service)'
}
$rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
# Newer (CNG) keys and older (CSP) keys report their file name differently.
$keyName = if ($rsa -is [Security.Cryptography.RSACng]) { $rsa.Key.UniqueName } else { $rsa.CspKeyContainerInfo.UniqueKeyContainerName }
$keyPath = @("$env:ProgramData\Microsoft\Crypto\Keys\$keyName", "$env:ProgramData\Microsoft\Crypto\RSA\MachineKeys\$keyName") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $keyPath) { throw "Could not find the certificate's private key file ($keyName)." }
icacls $keyPath /grant "${svcFull}:R" | Out-Null
Ok "certificate $($cert.Thumbprint) valid until $($cert.NotAfter.ToString('yyyy-MM-dd')); service can use its key"

# ---------------------------------------------------------------------------
Step 4 'Event log sources'

foreach ($src in 'Desk.Api', 'Desk.Scheduler') {
    if (-not [Diagnostics.EventLog]::SourceExists($src)) { New-EventLog -LogName Application -Source $src }
}
Ok 'services can write errors to the Windows Application log'

# ---------------------------------------------------------------------------
Step 5 'Database access'

$sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$svcFull') CREATE LOGIN [$svcFull] FROM WINDOWS;
USE Desk;
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$svcFull') CREATE USER [$svcFull] FOR LOGIN [$svcFull];
ALTER ROLE db_datareader ADD MEMBER [$svcFull];
ALTER ROLE db_datawriter ADD MEMBER [$svcFull];
"@
$out = sqlcmd -S $sqlInst -E -C -b -Q $sql 2>&1
if ($LASTEXITCODE -ne 0) { throw "SQL step failed: $($out -join ' ')" }
icacls "$data\backup" /grant "NT SERVICE\${sqlSvc}:(OI)(CI)M" | Out-Null
Ok "$svcUser can read and write the Desk database only; SQL Server can write backups to $data\backup"

# ---------------------------------------------------------------------------
Step 6 'Install the services'

$defs = @(
    @{ Name = 'ShoreDeskWeb'; Exe = "$app\api\Desk.Api.exe"; Display = 'Shore Desk - Web';
       Desc = 'Shore Desk web app and API (https://localhost:5444).' },
    @{ Name = 'ShoreDeskScheduler'; Exe = "$app\scheduler\Desk.Scheduler.exe"; Display = 'Shore Desk - Scheduler';
       Desc = 'Shore Desk emails and reminders (every 5 minutes).' }
)
foreach ($d in $defs) {
    $existing = Get-Service $d.Name -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.Status -ne 'Stopped') { Stop-Service $d.Name -Force }
        sc.exe config $d.Name obj= ".\$svcUser" password= $cred.GetNetworkCredential().Password | Out-Null
    } else {
        New-Service -Name $d.Name -BinaryPathName "`"$($d.Exe)`"" -DisplayName $d.Display `
            -Description $d.Desc -StartupType Automatic -Credential $cred | Out-Null
    }
    sc.exe config $d.Name start= delayed-auto depend= $sqlSvc | Out-Null
    sc.exe failure $d.Name reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
}
$cred = $null; $securePw = $null
Ok 'ShoreDeskWeb and ShoreDeskScheduler installed (start after SQL Server, restart on failure)'

# ---------------------------------------------------------------------------
Step 7 'Let the operator account start/stop the services (no admin needed for updates)'

$opSid = (New-Object Security.Principal.NTAccount($operator)).Translate([Security.Principal.SecurityIdentifier]).Value
foreach ($d in $defs) {
    $sddl = (sc.exe sdshow $d.Name | Where-Object { $_ -match '^D:' }) -join ''
    $ace = "(A;;RPWPDTLOCRRC;;;$opSid)"
    if ($sddl -notlike "*$opSid*") {
        $new = if ($sddl -match 'S:') { $sddl -replace 'S:', "${ace}S:" } else { $sddl + $ace }
        $out = sc.exe sdset $d.Name $new
        if ($LASTEXITCODE -ne 0) { throw "Could not set permissions on $($d.Name): $out" }
    }
}
Ok "$operator can start, stop and check both services"

# ---------------------------------------------------------------------------
Step 8 'Start and check'

# A test copy (if running) holds port 5444; stop it so the service can use it.
Get-Process Desk.Api -ErrorAction SilentlyContinue | Where-Object { $_.Path -notlike "$app\*" } | Stop-Process -Force
foreach ($d in $defs) { Start-Service $d.Name }
Start-Sleep -Seconds 15
Get-Service $defs.Name | Format-Table Name, Status, StartType -AutoSize | Out-String | Write-Host
$health = & curl.exe -sk -o NUL -w '%{http_code}' https://localhost:5444/health
if ($health -eq '200') { Ok 'web service answers at https://localhost:5444' }
else { Write-Host "    Web service did not answer yet (got '$health'). Tell Claude." -ForegroundColor Yellow }

Write-Host ''
Write-Host 'Done. Send Claude the lines above.' -ForegroundColor Green
