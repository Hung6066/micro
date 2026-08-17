[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$CoverageRoot,
    [double]$LineThreshold = 90,
    [double]$BranchThreshold = 80
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $CoverageRoot -PathType Container)) {
    throw "Coverage directory not found: $CoverageRoot"
}

$reports = @(Get-ChildItem -LiteralPath $CoverageRoot -Recurse -Filter 'coverage.cobertura.xml' -File)
if ($reports.Count -eq 0) {
    throw "No Cobertura reports found below $CoverageRoot"
}

# A test project can report the same source assembly more than once. Merge by
# source file, class and line so a duplicated report cannot inflate coverage.
$lines = @{}
$branches = @{}
foreach ($report in $reports) {
    [xml]$document = Get-Content -LiteralPath $report.FullName -Raw
    foreach ($package in @($document.coverage.packages.package)) {
        # Measure production Identity assemblies only. Test helpers (for
        # example IdentityService.Testing) are not product code and must not
        # dilute or inflate the service coverage gate.
        if ([string]$package.name -notmatch '^IdentityService\.(Api|Application|Domain|Infrastructure)$') { continue }
        foreach ($class in @($package.classes.class)) {
            # Coverlet reports async state machines and compiler-generated
            # closures as separate classes (for example /<Method>d__12 or
            # /<>c). Their sequence points are projections of the containing
            # source method, not independently maintainable product code.
            if ([string]$class.name -match '/<|/<>c') { continue }
            $source = ([string]$class.filename).Replace('\', '/')
            # Coverlet can emit the same source once as src/Services/... and
            # once as Services/... depending on the test project's working
            # directory. Normalize the stable repository suffix before merging.
            $identitySourceIndex = $source.IndexOf('Services/IdentityService/', [StringComparison]::OrdinalIgnoreCase)
            if ($identitySourceIndex -ge 0) {
                $source = $source.Substring($identitySourceIndex)
            }
            # Generated EF migration/designer code is not maintainable service
            # logic and would make the gate measure scaffolding instead of tests.
            if ($source -match '[\\/]Migrations[\\/]' -or
                $source -match '[\\/]obj[\\/]' -or
                $source -match '[\\/]Program\.cs$' -or
                $source -match '[\\/]LocalizationSeedData\.cs$' -or
                $source -match '\.Designer\.cs$' -or
                $source -match '(?:\.g|Grpc)\.cs$' -or
                # Composition files register middleware, DI and endpoint
                # delegates; they contain application wiring rather than
                # independently testable business decisions. Endpoint
                # handler files remain in the measured surface.
                $source -match '[\\/]Composition[\\/]IdentityService(?:Endpoint|Registration|Pipeline)Extensions\.cs$') {
                continue
            }
            if ($null -eq $class.lines -or $class.lines.PSObject.Properties.Name -notcontains 'line') {
                continue
            }
            foreach ($line in @($class.lines.line)) {
                $key = "$source|$($class.name)|$($line.number)"
                $hit = [int]$line.hits
                if (-not $lines.ContainsKey($key) -or $hit -gt $lines[$key]) {
                    $lines[$key] = $hit
                }

                if ([string]$line.branch -eq 'True') {
                    $branchKey = "$source|$($class.name)|$($line.number)|branch"
                    $condition = [string]$line.'condition-coverage'
                    if ($condition -match '(\d+)%(?: \((\d+)\/(\d+)\))?') {
                        $covered = if ($Matches[2]) { [int]$Matches[2] } else { [math]::Round(([int]$Matches[1] / 100), 0) }
                        $total = if ($Matches[3]) { [int]$Matches[3] } else { 1 }
                        if (-not $branches.ContainsKey($branchKey)) {
                            $branches[$branchKey] = @($covered, $total)
                        } else {
                            # Reports from separate test layers can describe
                            # the same branch with different instrumentation.
                            # Keep the strongest observed result instead of
                            # allowing report order to overwrite coverage.
                            $existing = $branches[$branchKey]
                            $mergedTotal = [math]::Max([int]$existing[1], [int]$total)
                            $mergedCovered = [math]::Max([int]$existing[0], [int]$covered)
                            $branches[$branchKey] = @($mergedCovered, $mergedTotal)
                        }
                    }
                }
            }
        }
    }
}

$validLines = $lines.Count
$coveredLines = @($lines.GetEnumerator() | Where-Object Value -gt 0).Count
$lineRate = if ($validLines -eq 0) { 0 } else { 100 * $coveredLines / $validLines }
$validBranches = ($branches.Values | ForEach-Object { $_[1] } | Measure-Object -Sum).Sum
$coveredBranches = ($branches.Values | ForEach-Object { $_[0] } | Measure-Object -Sum).Sum
$branchRate = if (-not $validBranches) { 0 } else { 100 * $coveredBranches / $validBranches }

Write-Output ("IDENTITY_COVERAGE lines={0}/{1} ({2:N2}%) branches={3}/{4} ({5:N2}%) reports={6}" -f `
    $coveredLines, $validLines, $lineRate, $coveredBranches, $validBranches, $branchRate, $reports.Count)

if ($lineRate -lt $LineThreshold -or $branchRate -lt $BranchThreshold) {
    throw ("Identity coverage gate failed: line {0:N2}% (required {1:N2}%), branch {2:N2}% (required {3:N2}%)." -f `
        $lineRate, $LineThreshold, $branchRate, $BranchThreshold)
}

Write-Output 'IDENTITY_COVERAGE_GATE_PASS'
