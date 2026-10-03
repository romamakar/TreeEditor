# Start both API and Web projects in separate PowerShell windows for development
# Usage: .\run-dev.ps1

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiDir = Join-Path $root "TreeEditor.Api"
$webDir = Join-Path $root "TreeEditor.Web"

Write-Host "Starting TreeEditor.Api in a new PowerShell window..."
$apiCmd = "Set-Location -Path '$apiDir'; dotnet run --no-launch-profile --urls 'https://localhost:5001'"
Start-Process -FilePath "pwsh" -ArgumentList @("-NoExit", "-Command", $apiCmd) -WindowStyle Normal

Start-Sleep -Milliseconds 500
Write-Host "Starting TreeEditor.Web in a new PowerShell window..."
$webCmd = "Set-Location -Path '$webDir'; dotnet run --no-launch-profile --urls 'https://localhost:5003' -- ApiBase='https://localhost:5001'"
Start-Process -FilePath "pwsh" -ArgumentList @("-NoExit", "-Command", $webCmd) -WindowStyle Normal

Write-Host "Launched both processes. Check the opened PowerShell windows for project URLs and logs."