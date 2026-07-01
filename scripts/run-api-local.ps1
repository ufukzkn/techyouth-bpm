param(
    [string]$Url = "http://localhost:5291",
    [int]$SessionDurationMinutes = 120,
    [int]$RememberMeDurationMinutes = 43200,
    [switch]$ResetDb,
    [switch]$Force,
    [switch]$SkipMockData
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiRoot = Join-Path $repoRoot "apps/api"
$apiProjectRoot = Join-Path $apiRoot "src/TechYouthBpm.Api"
$dbFiles = @(
    "techyouth-bpm.db",
    "techyouth-bpm.db-shm",
    "techyouth-bpm.db-wal"
)

if ($ResetDb) {
    if (-not $Force) {
        $answer = Read-Host "This removes the local SQLite demo database. Continue? Type YES"
        if ($answer -ne "YES") {
            Write-Host "Reset cancelled."
            exit 0
        }
    }

    foreach ($file in $dbFiles) {
        $path = Join-Path $apiProjectRoot $file
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Local SQLite demo database was reset."
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Database__Provider = "Sqlite"
$env:Auth__SessionDurationMinutes = "$SessionDurationMinutes"
$env:Auth__RememberMeDurationMinutes = "$RememberMeDurationMinutes"
$env:Seed__MockData = if ($SkipMockData) { "false" } else { "true" }
$Host.UI.RawUI.WindowTitle = "TechYouth BPM API"

Set-Location $apiRoot
Write-Host "Starting API with SQLite at $Url"
Write-Host "The database is created and seeded on API startup if it does not exist."
Write-Host "Mock workflow data: $($env:Seed__MockData)"
Write-Host "Session duration: $SessionDurationMinutes minute(s)"
Write-Host "Remember-me duration: $RememberMeDurationMinutes minute(s)"
dotnet run --project src/TechYouthBpm.Api --urls $Url
