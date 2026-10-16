# One-time: creates the Windows scheduled task "Shore Desk Backup" that runs
# scripts\Backup-App.ps1 every night at 2:30 am (after the contract app's 2:00 am backup),
# even when nobody is signed in.
# Run in a normal PowerShell window as Fadi Khalil:
#   powershell -ExecutionPolicy Bypass -File C:\Desk_app\scripts\Register-BackupTask.ps1
#
# It asks for YOUR Windows password (the one for this server's "Fadi Khalil" account).
# Windows stores it so the task can run at night with your saved access to the
# backup share (\\10.1.11.243). If you change that password later, run this again.

$ErrorActionPreference = 'Stop'
$user = "$env:COMPUTERNAME\Fadi Khalil"
$cred = Get-Credential -UserName $user -Message 'Your Windows password for this server (for the nightly backup task)'

$action  = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument '-NoProfile -ExecutionPolicy Bypass -File "C:\Desk_app\scripts\Backup-App.ps1"'
$trigger = New-ScheduledTaskTrigger -Daily -At 2:30am
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
    -RestartCount 2 -RestartInterval (New-TimeSpan -Minutes 30)

Register-ScheduledTask -TaskName 'Shore Desk Backup' -Action $action -Trigger $trigger -Settings $settings `
    -User $user -Password $cred.GetNetworkCredential().Password `
    -Description 'Nightly backup of the Shore Desk database and files to \\10.1.11.243\d$\Backup\shoreapps\desk' -Force | Out-Null
$cred = $null

Write-Host 'Task created. Running it once now as a test...' -ForegroundColor Cyan
Start-ScheduledTask -TaskName 'Shore Desk Backup'
Write-Host 'Started. Tell Claude "task created" and Claude will check the result.' -ForegroundColor Green
