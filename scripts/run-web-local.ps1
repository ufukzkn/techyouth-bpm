param(
    [string]$HostName = "localhost",
    [int]$Port = 3000
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$webRoot = Join-Path $repoRoot "apps/web"

$Host.UI.RawUI.WindowTitle = "TechYouth BPM Web"

Set-Location $webRoot
Write-Host "Starting web app at http://$HostName`:$Port"
npm run dev -- --hostname $HostName --port $Port
