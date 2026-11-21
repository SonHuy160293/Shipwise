#requires -Version 7.0
param(
    [ValidateSet('init','check','up','down','status','logs','config','build')][string]$Action = 'status',
    [ValidateSet('local','server')][string]$Environment = 'local',
    [ValidateSet('infrastructure','telemetry')][string[]]$Profile,
    [switch]$Reset
)
$ErrorActionPreference = 'Stop'

function Invoke-Docker([string[]]$Arguments) {
    & docker @Arguments
    if ($LASTEXITCODE) { throw "Docker command failed (exit $LASTEXITCODE)." }
}

function Read-Settings([string]$Path) {
    $settings = @{}
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if (!$line.Trim() -or $line.StartsWith('#')) { continue }
        if ($line -cnotmatch '^(SHIPWISE_[A-Z0-9_]+)=([^$#''"]*)$' -or $Matches[2] -ne $Matches[2].Trim()) {
            throw 'Invalid environment file. Use literal unquoted SHIPWISE_KEY=value entries.'
        }
        if ($settings.ContainsKey($Matches[1])) { throw "Duplicate setting $($Matches[1])." }
        $settings.Add($Matches[1], $Matches[2])
    }
    return $settings
}

function Set-SecretPermissions([string]$Path, [int]$OwnerId) {
    if (!$IsLinux) { return }
    $owner = "$($OwnerId):$OwnerId"
    $metadata = & stat -c '%u:%g:%a' -- $Path
    if ($LASTEXITCODE) { throw "Cannot stat secret file $Path." }
    if ($metadata -eq "$($owner):400") { return }
    $fileUser, $fileGroup, $mode = $metadata.Split(':')
    $currentUser = & id -u
    if ($mode -ne '400') {
        if ($currentUser -in '0',$fileUser) { & chmod 0400 -- $Path }
        else { & sudo chmod 0400 -- $Path }
        if ($LASTEXITCODE) { throw "Cannot set mode 0400 on $Path." }
    }
    if ("$($fileUser):$fileGroup" -ne $owner) {
        if ($currentUser -eq '0') { & chown $owner -- $Path }
        else { & sudo chown $owner -- $Path }
        if ($LASTEXITCODE) { throw "Cannot set owner $owner on $Path." }
    }
}

function Initialize-Secrets {
    [IO.Directory]::CreateDirectory($secretDir) | Out-Null
    foreach ($name in $secretNames) {
        $path = Join-Path $secretDir $name
        $existing = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        if ($existing -and ($existing.LinkType -or $existing.PSIsContainer)) { throw "Expected a regular secret file: $name." }
        $ownerId = if ($name -eq 'postgres-admin-password') { 999 } else { 1654 }
        if (![IO.File]::Exists($path)) {
            $temp = Join-Path $secretDir ([IO.Path]::GetRandomFileName())
            try {
                if ($IsLinux) {
                    # mktemp creates a private file before any secret bytes are written.
                    $temp = & mktemp --tmpdir=$secretDir '.init-XXXXXXXXXX'
                    if ($LASTEXITCODE) { throw 'Cannot create temporary secret file.' }
                }
                $bytes = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
                $value = if ($name.EndsWith('-password')) { [Convert]::ToHexString($bytes) } else { [Convert]::ToBase64String($bytes) }
                [IO.File]::WriteAllText($temp, $value)
                try { [IO.File]::Move($temp, $path, $false) }
                catch [IO.IOException] { if (![IO.File]::Exists($path)) { throw } }
            } finally {
                if ($temp -and (Test-Path -LiteralPath $temp -PathType Leaf)) { Remove-Item -LiteralPath $temp }
            }
        }
        Set-SecretPermissions $path $ownerId
    }
}

function Test-Stack {
    foreach ($key in $required) {
        if (!$settings[$key] -or $settings[$key].Contains('REPLACE_WITH_')) { throw "Set $key in $configFile." }
    }
    $expectedEnvironment = if ($Environment -eq 'server') { 'Production' } else { 'Development' }
    if ($settings.SHIPWISE_ENVIRONMENT -cne $expectedEnvironment) { throw "Set SHIPWISE_ENVIRONMENT to $expectedEnvironment." }
    if ($Environment -eq 'server') {
        $ip = $null
        if (![Net.IPAddress]::TryParse($settings.SHIPWISE_INGRESS_PROXY_IP, [ref]$ip)) {
            throw 'SHIPWISE_INGRESS_PROXY_IP must be an IP address.'
        }
    }
    foreach ($key in 'SHIPWISE_DOTNET_SDK_IMAGE','SHIPWISE_DOTNET_RUNTIME_IMAGE','SHIPWISE_POSTGRES_IMAGE') {
        if ($settings[$key] -cnotmatch '^[^@\s]+@sha256:[a-f0-9]{64}$') { throw "Pin $key by digest." }
    }
    foreach ($target in 'gateway','identity-api','identity-migration') {
        $key = 'SHIPWISE_' + $target.Replace('-','_').ToUpperInvariant() + '_IMAGE'
        $image = $settings[$key]
        if ($Environment -eq 'server') {
            if ($image -cnotmatch '^.+(@sha256:[a-f0-9]{64}|:[a-f0-9]{7,40})$' -or $image -match ':latest(@|$)') {
                throw "Use a registry commit tag or digest for $key."
            }
        } elseif ($image -cne "shipwise-$($target):local") { throw "Use shipwise-$($target):local for $key." }
    }
    foreach ($name in $secretNames) {
        $file = Get-Item -LiteralPath (Join-Path $secretDir $name) -ErrorAction SilentlyContinue
        if (!$file -or $file.PSIsContainer -or $file.LinkType -or $file.Length -eq 0) { throw "Missing, linked or empty secret file: $name." }
        if ($IsLinux) {
            $uid = if ($name -eq 'postgres-admin-password') { 999 } else { 1654 }
            if ((& stat -c '%u:%a' -- $file.FullName) -ne "$($uid):400") { throw "Expected owner $uid and mode 0400 on $name." }
        }
    }
    Invoke-Docker ($compose + @('config','--quiet'))
    $ports = ,@('gateway','SHIPWISE_GATEWAY_PORT','8080/tcp')
    if ($Environment -eq 'local') {
        $ports += ,@('postgres','SHIPWISE_POSTGRES_PORT','5432/tcp')
        $ports += ,@('identity-api','SHIPWISE_IDENTITY_API_PORT','8080/tcp')
    }
    $used = @{}
    foreach ($portSpec in $ports) {
        $service, $key, $containerPort = $portSpec
        [int]$port = 0
        if (![int]::TryParse($settings[$key], [ref]$port) -or $port -lt 1 -or $port -gt 65535 -or $used.ContainsKey($port)) {
            throw "Invalid or duplicate host port in $key."
        }
        $used[$port] = $true
        $container = Invoke-Docker ($compose + @('ps','-q',$service))
        if ($container -and (Invoke-Docker @('port',$container,$containerPort)) -contains "127.0.0.1:$port") { continue }
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $port)
        try { $listener.Server.ExclusiveAddressUse = $true; $listener.Start() }
        catch { throw "Port $port ($key) is unavailable." }
        finally { $listener.Stop() }
    }
}

Push-Location (Split-Path $PSScriptRoot -Parent)
$previous = @{}
try {
    if ($Reset -and $Action -ne 'down') { throw '-Reset applies only to down.' }
    if ($Profile) {
        if ($Action -notin 'config','build','up','down','status','logs' -or $Reset) { throw 'Unsupported profile action.' }
        $argsList = @('compose','-f','deploy/compose.yaml')
        foreach ($item in $Profile) { $argsList += @('--profile',$item) }
        $argsList += if ($Action -eq 'status') { 'ps' } else { $Action }
        if ($Action -eq 'up') { $argsList += '-d' }
        Invoke-Docker $argsList
        return
    }

    $configFile = [IO.Path]::GetFullPath("deploy/env/$Environment.env", (Get-Location).Path)
    if ($Action -eq 'init' -and ![IO.File]::Exists($configFile)) {
        try { [IO.File]::Copy("$configFile.example", $configFile) }
        catch [IO.IOException] { if (![IO.File]::Exists($configFile)) { throw } }
    }
    if (![IO.File]::Exists($configFile)) { throw "Missing $configFile. Run init first." }
    $settings = Read-Settings $configFile
    $required = @((Read-Settings "$configFile.example").Keys)
    foreach ($key in 'SHIPWISE_STACK','SHIPWISE_SECRETS_DIR') {
        if (!$settings[$key]) { throw "Set $key in $configFile." }
    }
    if ($settings.SHIPWISE_STACK -cnotmatch '^[a-z0-9][a-z0-9_-]*$' -or $settings.SHIPWISE_STACK -eq 'shipwise') {
        throw 'Choose a distinct Compose project name for SHIPWISE_STACK.'
    }
    $secretDir = [IO.Path]::GetFullPath($settings.SHIPWISE_SECRETS_DIR, (Resolve-Path deploy/compose).Path)
    $secretNames = @('postgres-admin-password','identity-migrator-password','identity-api-password','identity-jwt-key','identity-integration-key')
    if ($Action -eq 'init') {
        if ($Environment -eq 'local') { Initialize-Secrets }
        Write-Output "Initialized $Environment configuration."
        return
    }

    # Compose gives shell variables precedence. Use the same settings that check validates.
    foreach ($key in $required) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key)
        [Environment]::SetEnvironmentVariable($key, $settings[$key])
    }
    $compose = @('compose','--env-file',$configFile,'-p',$settings.SHIPWISE_STACK,'-f','deploy/compose/compose.yaml')
    if ($Environment -eq 'local') { $compose += @('-f','deploy/compose/compose.local.yaml') }
    switch ($Action) {
        'check' { Test-Stack; Write-Output 'Stack configuration passed.' }
        'up' {
            Test-Stack
            $up = $compose + @('up','-d','--wait','--wait-timeout','120')
            if ($Environment -eq 'local') { $up += '--build' }
            Invoke-Docker $up
        }
        'down' {
            $down = $compose + 'down'
            if ($Reset) {
                if ((Read-Host "Type $($settings.SHIPWISE_STACK) to delete its volumes") -cne $settings.SHIPWISE_STACK) {
                    throw 'Reset cancelled.'
                }
                $down += '--volumes'
            }
            Invoke-Docker $down
        }
        'status' { Invoke-Docker ($compose + @('ps','--all')) }
        'logs' { Invoke-Docker ($compose + 'logs') }
        'config' { Invoke-Docker ($compose + 'config') }
        'build' {
            if ($Environment -ne 'local') { throw 'Server deployments use published images.' }
            Invoke-Docker ($compose + 'build')
        }
    }
} finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key]) }
    Pop-Location
}
