$ErrorActionPreference = 'Stop'
$text = Get-Content (Join-Path $PSScriptRoot '../run-k3s-production.ps1') -Raw
if ($text -match '--extra-vars.*password') { throw 'Secrets must not be command-line extra vars.' }
if ($text -notmatch 'summary\.json') { throw 'Runner must write summary.json.' }
if ($text -notmatch 'ValidationOnly') { throw 'Runner must support validation-only mode.' }
if ($text -notmatch 'phaseOrder') { throw 'Runner must validate ordered phase ranges.' }
if ($text -notmatch '--tags') { throw 'Runner must pass selected phase tags to Ansible.' }
Write-Output 'Production runner contract PASS'
