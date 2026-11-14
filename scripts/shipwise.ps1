param(
    [ValidateSet('config','up','down','logs','build')][string]$Action = 'config',
    [ValidateSet('gateway','telemetry','infrastructure')][string[]]$Profile = @('gateway')
)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $arguments = @('compose', '-f', 'deploy/compose.yaml')
    foreach ($item in $Profile) { $arguments += @('--profile', $item) }
    $arguments += $Action
    if ($Action -eq 'up') { $arguments += @('-d', '--build') }
    & docker @arguments
    if ($LASTEXITCODE) { throw "Compose $Action failed." }
} finally { Pop-Location }
