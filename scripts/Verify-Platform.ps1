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
    # CI reads top-level TRX files. Give this run a unique prefix so previous or parallel runs cannot affect its verdict.
    $prefix = "platform-$([guid]::NewGuid().ToString('N'))"
    dotnet test Shipwise.slnx -c Release --no-build --logger "trx;LogFilePrefix=$prefix" --results-directory artifacts/tests
    if ($LASTEXITCODE) { throw 'Tests failed.' }
    if ($RequireInfrastructure) {
        $files = @(Get-ChildItem -LiteralPath artifacts/tests -Filter "$prefix*.trx" -File)
        if (!$files.Count) { throw 'Required infrastructure verification produced no TRX files.' }
        $skipped = foreach ($file in $files) {
            [xml]$trx = Get-Content -LiteralPath $file.FullName -Raw
            $trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq 'NotExecuted' | ForEach-Object testName
        }
        if ($skipped) { throw "Required infrastructure verification skipped: $($skipped -join ', ')." }
    }
} finally { Pop-Location }
