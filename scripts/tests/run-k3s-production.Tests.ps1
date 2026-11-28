$ErrorActionPreference = 'Stop'
$text = Get-Content (Join-Path $PSScriptRoot '../run-k3s-production.ps1') -Raw
$releaseValidator = Get-Content (Join-Path $PSScriptRoot '../validate-k3s-release.ps1') -Raw
if ($text -match '--extra-vars.*password') { throw 'Secrets must not be command-line extra vars.' }
if ($text -notmatch 'summary\.json') { throw 'Runner must write summary.json.' }
if ($text -notmatch 'ValidationOnly') { throw 'Runner must support validation-only mode.' }
if ($text -notmatch 'phaseOrder') { throw 'Runner must validate ordered phase ranges.' }
if ($text -notmatch '--tags') { throw 'Runner must pass selected phase tags to Ansible.' }
if ($releaseValidator -notmatch 'previousNativeErrorAction') { throw 'Release validator must preserve native stderr handling state.' }
if ($releaseValidator -notmatch "ErrorActionPreference = 'Continue'") { throw 'Release validator must tolerate non-fatal kubectl render warnings.' }
if ($releaseValidator -notmatch 'LASTEXITCODE') { throw 'Release validator must still fail when kustomize exits non-zero.' }
if ($releaseValidator -notmatch 'image-tag-policy') { throw 'Release validator must reject mutable production image tags.' }
Write-Output 'Production runner contract PASS'
