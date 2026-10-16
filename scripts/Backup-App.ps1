# Nightly backup of Shore Desk to the office backup share.
# Run by the Windows scheduled task "Shore Desk Backup" (scripts\Register-BackupTask.ps1).
# Can also be run by hand:
#   powershell -ExecutionPolicy Bypass -File C:\Desk_app\scripts\Backup-App.ps1
#
# What it does:
#  1. SQL backup of the Desk database to C:\DeskData\backup (SQL Server can't write to a
#     network share as its own service account, so it stages locally first).
#  2. Copies the .bak and the files folder (photos, attachments) to <share>\<yyyy-MM-dd_HHmm>\.
#     Database first, then files: files are only ever added, so every file the
#     database points to is in the copy.
#  3. Checks the copy (backup checksum, file count) and writes a manifest.
#  4. Deletes backup folders older than $KeepDays on the share.
#  5. On any failure: writes FAILED to the log (shown on Settings > App health) and, once the
#     desk@ mailer exists, emails Fadi.
# Secrets are NOT copied (keep them in a password manager). Separate from the contract
# app's backup job, so neither can break the other.

param(
    [string]$Share    = '\\10.1.11.243\d$\Backup\shoreapps\desk',
    [int]   $KeepDays = 14
)

$ErrorActionPreference = 'Stop'
$data     = 'C:\DeskData'
$staging  = "$data\backup"
$logFile  = "$data\status\backup-log.txt"
$sqlInst  = 'localhost\SQLEXPRESS'
$alertTo  = 'fadikhalil@shorehotel.com'
$problems = New-Object System.Collections.Generic.List[string]

function Log($text) {
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $text
    Write-Host $line
    Add-Content -Path $logFile -Value $line -Encoding UTF8
}

# Needs the Shore Desk mailer app (set up with the desk@ mailbox, phase 5). Until then: log only.
function Send-FailureMail($subject, $text) {
    $secretFile = "$data\secrets\mailer-client-secret.txt"
    $settingsFile = 'C:\DeskApp\scheduler\appsettings.json'
    if (-not (Test-Path $secretFile) -or -not (Test-Path $settingsFile)) { Log 'no mailer set up yet - failure shown on App health only'; return }
    try {
        $s = (Get-Content $settingsFile -Raw | ConvertFrom-Json).Scheduler
        if (-not $s.MailerClientId) { Log 'no mailer client ID yet - failure shown on App health only'; return }
        $secret = (Get-Content $secretFile -Raw).Trim()
        $tok = Invoke-RestMethod -Method Post `
            -Uri "https://login.microsoftonline.com/$($s.TenantId)/oauth2/v2.0/token" `
            -Body @{ client_id = $s.MailerClientId; client_secret = $secret
                     scope = 'https://graph.microsoft.com/.default'; grant_type = 'client_credentials' }
        $body = @{ message = @{ subject = $subject
                                body = @{ contentType = 'Text'; content = $text }
                                toRecipients = @(@{ emailAddress = @{ address = $alertTo } }) }
                   saveToSentItems = $true } | ConvertTo-Json -Depth 6
        Invoke-RestMethod -Method Post -Uri "https://graph.microsoft.com/v1.0/users/$($s.FromMailbox)/sendMail" `
            -Headers @{ Authorization = "Bearer $($tok.access_token)" } -ContentType 'application/json' -Body $body
        Log 'failure email sent'
    } catch { Log "could not send failure email: $($_.Exception.Message)" }
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
New-Item -ItemType Directory -Force $staging, "$data\status", "$data\files" | Out-Null
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
$target = Join-Path $Share $stamp
$bak = Join-Path $staging 'Desk.bak'
Log "backup started -> $target"

try {
    # 1. Database
    if (Test-Path $bak) { Remove-Item $bak -Force }
    $out = sqlcmd -S $sqlInst -E -C -b -Q "BACKUP DATABASE Desk TO DISK='$bak' WITH INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK='$bak' WITH CHECKSUM" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL backup failed: $($out -join ' ')" }
    $counts = sqlcmd -S $sqlInst -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT (SELECT COUNT(*) FROM Desk.dbo.Users), (SELECT COUNT(*) FROM Desk.dbo.ActivityLog)" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "row count failed: $($counts -join ' ')" }
    $c = ($counts | Where-Object { $_ -match '\d' } | Select-Object -First 1) -split '\s+'
    Log ("database backed up and verified: {0:N1} MB, {1} people, {2} activity rows" -f ((Get-Item $bak).Length / 1MB), $c[0], $c[1])

    # 2. Copy to the share
    if (-not (Test-Path $Share)) { New-Item -ItemType Directory -Force $Share | Out-Null }
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item $bak (Join-Path $target 'Desk.bak')
    # robocopy handles long paths itself (and rejects \\?\ prefixes).
    robocopy "$data\files" "$target\files" /E /R:2 /W:5 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "file copy failed (robocopy code $LASTEXITCODE)" }

    # 3. Check the copy
    $srcFiles = @(Get-ChildItem "$data\files" -Recurse -File)
    $dstFiles = @(Get-ChildItem "$target\files" -Recurse -File -ErrorAction SilentlyContinue)
    $srcMb = ($srcFiles | Measure-Object Length -Sum).Sum / 1MB
    if ($dstFiles.Count -ne $srcFiles.Count) { throw "file count mismatch: $($srcFiles.Count) on server, $($dstFiles.Count) in backup" }
    if ((Get-Item (Join-Path $target 'Desk.bak')).Length -ne (Get-Item $bak).Length) { throw 'database copy size mismatch' }
    @(
        "Shore Desk backup $stamp from $env:COMPUTERNAME"
        "People: $($c[0])   Activity rows: $($c[1])"
        "Files: $($srcFiles.Count) ($('{0:N1}' -f $srcMb) MB)"
        'Restore: RESTORE DATABASE Desk FROM DISK=... WITH REPLACE, then copy files\ back to C:\DeskData\files.'
    ) | Set-Content (Join-Path $target 'manifest.txt') -Encoding UTF8
    Log ("copied: Desk.bak + {0} files ({1:N1} MB)" -f $srcFiles.Count, $srcMb)
} catch {
    $problems.Add($_.Exception.Message)
    Log "FAILED: $($_.Exception.Message)"
} finally {
    # Never leave a full copy of the database lying in the staging folder, even after a failure.
    if (Test-Path $bak) { Remove-Item $bak -Force -ErrorAction SilentlyContinue }
}

# 4. Retention (only folders this script made: names like 2026-10-17_0230)
try {
    if ($problems.Count -eq 0) {
        $cutoff = (Get-Date).AddDays(-$KeepDays)
        Get-ChildItem $Share -Directory | Where-Object {
            $_.Name -match '^\d{4}-\d{2}-\d{2}_\d{4}$' -and
            [datetime]::ParseExact($_.Name, 'yyyy-MM-dd_HHmm', $null) -lt $cutoff
        } | ForEach-Object {
            Remove-Item "\\?\UNC\$($_.FullName.TrimStart('\'))" -Recurse -Force
            Log "removed old backup $($_.Name)"
        }
    }
} catch {
    $problems.Add("retention: $($_.Exception.Message)")
    Log "retention FAILED: $($_.Exception.Message)"
}

if ($problems.Count -gt 0) {
    Send-FailureMail "[Shore Desk] Nightly backup FAILED on $env:COMPUTERNAME" `
        ("The Shore Desk backup did not complete.`r`n`r`n" + ($problems -join "`r`n") + "`r`n`r`nLog: $logFile")
    exit 1
}
Set-Content "$data\status\backup-last-ok.txt" (Get-Date -Format 'o') -Encoding UTF8
Log 'backup finished OK'
exit 0
