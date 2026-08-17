Describe 'Unified runtime adapter matrix' {
    It 'contains the canonical contract and every adapter entry point' {
        $root = (Resolve-Path "$PSScriptRoot\..\..").Path
        @(
            'config\runtime-contract.schema.json',
            'scripts\config\validate-runtime-contract.ps1',
            'docker\config\compose.runtime.env.ps1',
            'scripts\config\validate-compose-stack.ps1',
            'deploy\vm\systemd\his-hope-service@.service',
            'deploy\vm\windows\Install-HisHopeService.ps1',
            'scripts\config\validate-vm-runtime.ps1',
            'k8s\base\runtime-contract-configmap.yaml',
            'k8s\overlays\prod\runtime-secret-provider-class.yaml',
            'scripts\config\validate-kustomize-runtime.ps1',
            'scripts\config\compare-runtime-contracts.ps1',
            'scripts\config\smoke-runtime-stack.ps1',
            'scripts\config\rollback-runtime.ps1'
        ) | ForEach-Object { Test-Path (Join-Path $root $_) | Should -BeTrue -Because $_ }
    }
}
