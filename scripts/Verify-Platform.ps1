param([switch]$RequireInfrastructure)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if ($RequireInfrastructure -and ([string]::IsNullOrWhiteSpace($env:SHIPWISE_TEST_POSTGRES) -or
        [string]::IsNullOrWhiteSpace($env:SHIPWISE_TEST_RABBITMQ))) {
        throw 'Set SHIPWISE_TEST_POSTGRES and SHIPWISE_TEST_RABBITMQ for infrastructure verification.'
    }
    & "$PSScriptRoot/Test-Architecture.ps1"
    dotnet restore Shipwise.slnx
    if ($LASTEXITCODE) { throw 'Restore failed.' }
    dotnet build Shipwise.slnx -c Release --no-restore
    if ($LASTEXITCODE) { throw 'Build failed.' }
    dotnet test Shipwise.slnx -c Release --no-build --logger 'trx;LogFilePrefix=platform' --results-directory artifacts/tests
    if ($LASTEXITCODE) { throw 'Tests failed.' }
} finally { Pop-Location }
