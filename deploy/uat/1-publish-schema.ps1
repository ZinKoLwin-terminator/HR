<#
.SYNOPSIS
  Step 1: create the HR database on the UAT RDS SQL Server and publish the HR DBPJ schema.

.DESCRIPTION
  Run it yourself, from a PC whose IP is allowed on the RDS security group (port 1433).
  The server and login come from uat.local.env next to this script (git-ignored; copy uat.local.env.example),
  from -Server / -User, or are asked for.
    1. Installs SqlPackage (dotnet global tool) if it is missing.
    2. Builds the DB project (target SQL Server 2019) unless -DacpacPath is given.
    3. Writes the deployment script for review (HR-deploy-<time>.sql). Nothing changes yet.
    4. Publishes only after you type "yes". The database is created if it does not exist.
  The password is asked for and never written to disk.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\1-publish-schema.ps1
#>
param(
    [string]$Server = "",
    [string]$Database = "HR",
    [string]$User = "",
    [string]$ProjectDir = (Join-Path $PSScriptRoot "..\..\HR DBPJ"),
    [string]$DacpacPath = ""
)
$ErrorActionPreference = "Stop"

# --- environment values (never committed) -------------------------------------------------------
$settings = @{}
$localEnv = Join-Path $PSScriptRoot "uat.local.env"
if (Test-Path $localEnv) {
    foreach ($line in Get-Content $localEnv) {
        if ($line -match '^\s*([A-Za-z_]+)\s*=\s*(.*?)\s*$') { $settings[$Matches[1]] = $Matches[2] }
    }
}
if (-not $Server) {
    $endpoint = if ($settings["RDS_ENDPOINT"]) { $settings["RDS_ENDPOINT"] } else { Read-Host "RDS endpoint (host name)" }
    $Server = if ($endpoint -match ',\d+$') { $endpoint } else { "$endpoint,1433" }
}
if (-not $User) {
    $User = if ($settings["RDS_ADMIN_LOGIN"]) { $settings["RDS_ADMIN_LOGIN"] } else { Read-Host "RDS master login" }
}

# --- 1. SqlPackage -----------------------------------------------------------------------------
$sqlpackage = (Get-Command sqlpackage -ErrorAction SilentlyContinue).Source
if (-not $sqlpackage) {
    $candidate = Join-Path $env:USERPROFILE ".dotnet\tools\sqlpackage.exe"
    if (-not (Test-Path $candidate)) {
        Write-Host "Installing SqlPackage (dotnet tool install -g microsoft.sqlpackage)..." -ForegroundColor Cyan
        dotnet tool install -g microsoft.sqlpackage
        if ($LASTEXITCODE -ne 0) { throw "SqlPackage install failed." }
    }
    $sqlpackage = $candidate
}
Write-Host "SqlPackage: $sqlpackage"

# --- 2. dacpac ---------------------------------------------------------------------------------
if (-not $DacpacPath) {
    Write-Host "Building the DB project (SQL Server 2019 target)..." -ForegroundColor Cyan
    dotnet build (Join-Path $ProjectDir "HR DBPJ.sqlproj") -c Release -nologo
    if ($LASTEXITCODE -ne 0) { throw "DB project build failed." }
    $DacpacPath = Join-Path $ProjectDir "bin\Release\HR DBPJ.dacpac"
}
if (-not (Test-Path $DacpacPath)) { throw "dacpac not found: $DacpacPath" }
Write-Host "dacpac: $DacpacPath"

# --- connection ---------------------------------------------------------------------------------
$secure = Read-Host "Password for $User on $Server" -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
# TrustServerCertificate: only for this run from a developer PC. The API itself verifies the RDS certificate.
$conn = "Server=$Server;Database=$Database;User Id=$User;Password=$plain;Encrypt=True;TrustServerCertificate=True;Connect Timeout=30"

$common = @(
    "/SourceFile:$DacpacPath",
    "/TargetConnectionString:$conn",
    "/p:BlockOnPossibleDataLoss=True",
    "/p:DropObjectsNotInSource=False",
    "/p:IncludeTransactionalScripts=True"
)

try {
    # --- 3. review script ------------------------------------------------------------------------
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $reviewFile = Join-Path $PSScriptRoot "HR-deploy-$stamp.sql"
    Write-Host "Generating the deployment script for review..." -ForegroundColor Cyan
    & $sqlpackage /Action:Script @common "/OutputPath:$reviewFile"
    if ($LASTEXITCODE -ne 0) { throw "Script generation failed." }
    Write-Host ""
    Write-Host "Review this file before publishing: $reviewFile" -ForegroundColor Yellow
    Write-Host "Target: $Server / database [$Database]" -ForegroundColor Yellow

    # --- 4. publish ------------------------------------------------------------------------------
    $answer = Read-Host "Type yes to publish to [$Database] on $Server"
    if ($answer -ne "yes") { Write-Host "Not published."; exit 0 }

    & $sqlpackage /Action:Publish @common
    if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
    Write-Host "Schema published to [$Database]. Next: run 2-create-api-login.sql in SSMS." -ForegroundColor Green
}
finally {
    $plain = $null; $conn = $null; $common = $null
}
