param(
    [string]$Url = "http://localhost:5291",
    [string]$EnvFile = ".env.neon.local",
    [int]$SessionDurationMinutes = 120,
    [int]$RememberMeDurationMinutes = 43200,
    [int]$RefreshTokenDurationMinutes = 43200,
    [int]$PasswordResetMinutes = 30,
    [string]$FrontendBaseUrl = "http://localhost:3000",
    [int]$MaxFailedLoginAttempts = 5,
    [int]$LockoutMinutes = 10,
    [switch]$SkipMockData,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiRoot = Join-Path $repoRoot "apps/api"
$envPath = Join-Path $repoRoot $EnvFile

if (-not (Test-Path -LiteralPath $envPath)) {
    Write-Error "Neon environment file was not found: $EnvFile"
}

Get-Content -LiteralPath $envPath | ForEach-Object {
    $line = $_.Trim()
    if (-not $line -or $line.StartsWith("#")) {
        return
    }

    $separatorIndex = $line.IndexOf("=")
    if ($separatorIndex -lt 1) {
        return
    }

    $name = $line.Substring(0, $separatorIndex).Trim()
    $value = $line.Substring($separatorIndex + 1).Trim().Trim('"')
    [Environment]::SetEnvironmentVariable($name, $value, "Process")
}

if (-not $env:ConnectionStrings__DefaultConnection) {
    Write-Error "ConnectionStrings__DefaultConnection must be set in $EnvFile"
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Database__Provider = "PostgreSql"
$env:Auth__SessionDurationMinutes = "$SessionDurationMinutes"
$env:Auth__RememberMeDurationMinutes = "$RememberMeDurationMinutes"
$env:Auth__RefreshTokenDurationMinutes = "$RefreshTokenDurationMinutes"
$env:Auth__PasswordResetMinutes = "$PasswordResetMinutes"
$env:Frontend__BaseUrl = "$FrontendBaseUrl"
$env:Auth__MaxFailedLoginAttempts = "$MaxFailedLoginAttempts"
$env:Auth__LockoutMinutes = "$LockoutMinutes"
$env:Seed__MockData = if ($SkipMockData) { "false" } else { "true" }
$Host.UI.RawUI.WindowTitle = "TechYouth BPM API - Neon"

Set-Location $apiRoot
Write-Host "Starting API with Neon PostgreSQL at $Url"
Write-Host "Environment file: $EnvFile"
Write-Host "EF Core migrations are applied on API startup, then demo seed data is added."
Write-Host "Use a disposable Neon branch/database when testing migration resets."
Write-Host "Mock workflow data: $($env:Seed__MockData)"
Write-Host "Session duration: $SessionDurationMinutes minute(s)"
Write-Host "Remember-me duration: $RememberMeDurationMinutes minute(s)"
Write-Host "Refresh-token duration: $RefreshTokenDurationMinutes minute(s)"
Write-Host "Password reset token duration: $PasswordResetMinutes minute(s)"
Write-Host "Frontend base URL: $FrontendBaseUrl"
Write-Host "Failed login lockout: $MaxFailedLoginAttempts attempt(s), $LockoutMinutes minute(s)"
Write-Host "Stop with Ctrl+C in this terminal."
if ($NoBuild) {
    dotnet run --no-build --project src/TechYouthBpm.Api --urls $Url
} else {
    dotnet run --project src/TechYouthBpm.Api --urls $Url
}
