param(
    [string]$Url = "http://localhost:5291",
    [int]$SessionDurationMinutes = 120,
    [int]$RememberMeDurationMinutes = 43200,
    [int]$RefreshTokenDurationMinutes = 43200,
    [int]$PasswordResetMinutes = 30,
    [string]$FrontendBaseUrl = "http://localhost:3000",
    [int]$MaxFailedLoginAttempts = 5,
    [int]$LockoutMinutes = 10,
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
$env:Auth__RefreshTokenDurationMinutes = "$RefreshTokenDurationMinutes"
$env:Auth__PasswordResetMinutes = "$PasswordResetMinutes"
$env:Frontend__BaseUrl = "$FrontendBaseUrl"
$env:Auth__MaxFailedLoginAttempts = "$MaxFailedLoginAttempts"
$env:Auth__LockoutMinutes = "$LockoutMinutes"
$env:Seed__MockData = if ($SkipMockData) { "false" } else { "true" }
$Host.UI.RawUI.WindowTitle = "TechYouth BPM API"

Set-Location $apiRoot
Write-Host "Starting API with SQLite at $Url"
Write-Host "EF Core migrations are applied on API startup, then demo seed data is added."
Write-Host "If this is an old pre-migration SQLite DB, run with -ResetDb -Force once."
Write-Host "Mock workflow data: $($env:Seed__MockData)"
Write-Host "Session duration: $SessionDurationMinutes minute(s)"
Write-Host "Remember-me duration: $RememberMeDurationMinutes minute(s)"
Write-Host "Refresh-token duration: $RefreshTokenDurationMinutes minute(s)"
Write-Host "Password reset token duration: $PasswordResetMinutes minute(s)"
Write-Host "Frontend base URL: $FrontendBaseUrl"
Write-Host "Failed login lockout: $MaxFailedLoginAttempts attempt(s), $LockoutMinutes minute(s)"
Write-Host "Stop with Ctrl+C in this terminal."
dotnet run --project src/TechYouthBpm.Api --urls $Url
