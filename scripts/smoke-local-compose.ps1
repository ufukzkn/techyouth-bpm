param(
  [int]$TimeoutSeconds = 90,
  [int]$Port = 5291
)

$ErrorActionPreference = "Stop"
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$healthUrl = "http://localhost:$Port/health/ready"

while ((Get-Date) -lt $deadline) {
  try {
    $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 3
    if ($response.StatusCode -eq 200) {
      Write-Host "Local API is ready: $healthUrl"
      exit 0
    }
  } catch {
    Start-Sleep -Seconds 2
  }
}

docker compose ps
docker compose logs api --tail 120
throw "Local API did not become ready within $TimeoutSeconds seconds."
