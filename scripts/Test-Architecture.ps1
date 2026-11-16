$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$projects = @(Get-ChildItem "$root/src", "$root/tests" -Recurse -Filter *.csproj |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
$graph = @{}
foreach ($project in $projects) {
    $graph[$project.FullName] = @()
    [xml]$xml = Get-Content -LiteralPath $project.FullName -Raw
    foreach ($reference in $xml.SelectNodes('//ProjectReference')) {
        $target = [IO.Path]::GetFullPath((Join-Path $project.DirectoryName $reference.Include))
        if (-not $target.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $target)) { throw "Missing or external reference: $target" }
        $isTest = $project.FullName -match '[\\/]tests[\\/]'
        $isBlock = $target -match '[\\/]BuildingBlocks[\\/]'
        $allowed = $isTest -or $isBlock
        if (-not $isTest -and $project.FullName -match '[\\/]BuildingBlocks[\\/]') {
            $allowed = $isBlock
        } elseif (-not $isTest -and $project.FullName -match '[\\/]Contracts[\\/]') {
            $allowed = $isBlock
        } elseif (-not $isTest -and $project.BaseName -match '^(.*)\.(Domain|Application|Infrastructure|API|Worker|MigrationRunner)$') {
            $module = $Matches[1]
            $layer = $Matches[2]
            $layers = switch ($layer) {
                Domain { @() }
                Application { @('Domain') }
                Infrastructure { @('Application', 'Domain') }
                API { @('Application', 'Infrastructure') }
                Worker { @('Application', 'Infrastructure') }
                MigrationRunner { @('Infrastructure') }
            }
            $dependency = [IO.Path]::GetFileNameWithoutExtension($target)
            $isContract = $target -match '[\\/]Contracts[\\/]'
            $allowed = $isBlock -or ($layer -notin @('Domain', 'MigrationRunner') -and $isContract) -or
                ($dependency -in @($layers | ForEach-Object { "$module.$_" }))
            if ($layer -eq 'Application' -and $dependency -eq 'Shipwise.AspNetCore') { $allowed = $false }
        }
        if (-not $allowed) { throw "Forbidden dependency: $($project.Name) -> $target" }
        if ($target -match 'Shipwise\.(Notification|Projects)') { throw "Excluded module reference: $target" }
        $graph[$project.FullName] += $target
    }
}
function Visit($node, $ancestors) {
    if ($node -in $ancestors) { throw "Dependency cycle: $node" }
    foreach ($child in $graph[$node]) { Visit $child (@($ancestors) + $node) }
}
foreach ($node in @($graph.Keys)) { Visit $node @() }
Write-Output "Architecture checks passed for $($projects.Count) source and test projects."
