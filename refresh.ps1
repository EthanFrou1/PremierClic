<#
Refresh local development services for PremierClic.
Usage:
  .\refresh.ps1
  .\refresh.ps1 -NoVolumes
#>
param(
    [switch]$NoVolumes
)

$downArgs = @("down")
if ($NoVolumes) {
    $downArgs += "-v"
}

Write-Host "Stopping Docker Compose services..."
docker compose @downArgs

Write-Host "Rebuilding and restarting services..."
docker compose up --build
