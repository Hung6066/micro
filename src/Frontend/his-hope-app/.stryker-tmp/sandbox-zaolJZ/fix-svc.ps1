$baseDir = "D:\AI\micro\src\Frontend\his-hope-app"
$svcFiles = @(
    "src\app\core\services\appointment.service.spec.ts",
    "src\app\core\services\billing.service.spec.ts",
    "src\app\core\services\clinical.service.spec.ts",
    "src\app\core\services\lab.service.spec.ts",
    "src\app\core\services\patient.service.spec.ts",
    "src\app\core\services\pharmacy.service.spec.ts"
)
foreach ($file in $svcFiles) {
    $path = Join-Path $baseDir $file
    $content = Get-Content $path -Raw
    $content = $content -replace "import \{ TestBed, fakeAsync, tick \} from .@angular/core/testing.", "import { TestBed } from '@angular/core/testing';"
    $content = $content -replace "import \{ TestBed, fakeAsync \} from .@angular/core/testing.", "import { TestBed } from '@angular/core/testing';"
    $content = $content -replace "it\('([^']+)', fakeAsync\(\(\) => \{", "it('`$1', () => {"
    $content = $content -replace "}  \)", "})"
    $lines = $content -split "`n"
    $newLines = @()
    foreach ($line in $lines) {
        if ($line -match '^(\s*)\}\)\);$') {
            $line = $matches[1] + '});'
        }
        $newLines += $line
    }
    $content = $newLines -join "`n"
    Set-Content -Path $path -Value $content
    Write-Host "Fixed: $file"
}
