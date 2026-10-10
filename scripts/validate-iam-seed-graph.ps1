[CmdletBinding()]
param(
    [string]$ContainerName = 'his-hope-postgres',
    [string]$Database = 'identitydb',
    [string]$Username = 'postgres'
)

$ErrorActionPreference = 'Stop'

$sql = @'
WITH counts AS (
    SELECT 'scopes' AS key, count(*)::int AS value FROM iam_scopes
    UNION ALL SELECT 'services', count(*)::int FROM iam_service_definitions
    UNION ALL SELECT 'permission_sets', count(*)::int FROM iam_permission_sets
    UNION ALL SELECT 'assignments', count(*)::int FROM iam_permission_set_assignments
    UNION ALL SELECT 'groups', count(*)::int FROM iam_groups
    UNION ALL SELECT 'group_memberships', count(*)::int FROM iam_group_memberships
    UNION ALL SELECT 'workload_roles', count(*)::int FROM iam_workload_roles
    UNION ALL SELECT 'boundaries', count(*)::int FROM iam_permission_boundaries
    UNION ALL SELECT 'resource_policies', count(*)::int FROM iam_resource_policies
    UNION ALL SELECT 'policies', count(*)::int FROM authorization_policy_definitions
    UNION ALL SELECT 'access_requests', count(*)::int FROM access_requests
    UNION ALL SELECT 'access_reviews', count(*)::int FROM access_reviews
    UNION ALL SELECT 'break_glass', count(*)::int FROM break_glass_requests
    UNION ALL SELECT 'posture_policies', count(*)::int FROM device_posture_policies
    UNION ALL SELECT 'posture_assessments', count(*)::int FROM device_posture_assessments
    UNION ALL SELECT 'posture_assessment_table', count(*)::int FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'device_posture_assessments'
    UNION ALL SELECT 'provision_bindings', count(*)::int FROM directory_provisioning_bindings
    UNION ALL SELECT 'provision_outbox', count(*)::int FROM directory_provisioning_outbox
    UNION ALL SELECT 'provision_outbox_table', count(*)::int FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'directory_provisioning_outbox'
    UNION ALL SELECT 'certificates', count(*)::int FROM user_client_certificates
    UNION ALL SELECT 'seed_audit', count(*)::int FROM audit_logs WHERE source = 'seed-demo'
), relationships AS (
    SELECT 'group_assignment' AS key, count(*)::int AS value
    FROM iam_permission_set_assignments a JOIN iam_groups g ON g.id = a.principal_id
    WHERE a.principal_type = 'group'
    UNION ALL SELECT 'workload_assignments', count(*)::int
    FROM iam_permission_set_assignments a JOIN iam_workload_roles r ON r.id = a.principal_id
    WHERE a.principal_type = 'workload'
    UNION ALL SELECT 'workload_boundaries', count(*)::int
    FROM iam_permission_boundaries b JOIN iam_workload_roles r ON r.id = b.principal_id
    WHERE b.principal_type = 'workload'
    UNION ALL SELECT 'workload_resource_policies', count(*)::int
    FROM iam_resource_policies p JOIN iam_workload_roles r ON p.service_key = split_part(r.audience, '-service', 1)
       OR (r.audience = 'appointment-service' AND p.service_key = 'appointments')
)
SELECT line FROM (
    SELECT key || '=' || value AS line FROM counts
    UNION ALL
    SELECT key || '=' || value AS line FROM relationships
) ordered_lines ORDER BY line;
'@

$output = & docker exec -i $ContainerName psql -U $Username -d $Database -At -c $sql 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "IAM seed graph query failed: $($output -join ' ')"
}

$values = @{}
foreach ($line in $output) {
    if ($line -match '^(?<key>[a-z_]+)=(?<value>\d+)$') {
        $values[$Matches.key] = [int]$Matches.value
    }
}

# Assessments and provisioning outbox rows are runtime-generated, so a clean
# production tenant may legitimately have zero rows. Require their tables,
# while keeping seeded control-plane relationships fail-closed.
$minimums = @{
    scopes = 4; services = 12; permission_sets = 3; assignments = 8; groups = 1
    group_memberships = 1; workload_roles = 5; boundaries = 5; resource_policies = 5
    policies = 1; access_requests = 1; access_reviews = 1; break_glass = 1
    posture_policies = 1; posture_assessment_table = 1; provision_bindings = 1
    provision_outbox_table = 1; certificates = 1; seed_audit = 1
    group_assignment = 1; workload_assignments = 5; workload_boundaries = 5
    workload_resource_policies = 5
}

$failures = foreach ($entry in $minimums.GetEnumerator()) {
    if (-not $values.ContainsKey($entry.Key) -or $values[$entry.Key] -lt $entry.Value) {
        $actual = 'missing'
        if ($values.ContainsKey($entry.Key)) {
            $actual = $values[$entry.Key]
        }
        "{0}={1} (expected >= {2})" -f $entry.Key, $actual, $entry.Value
    }
}

if ($failures) {
    Write-Error ('IAM_SEED_GRAPH_FAIL ' + ($failures -join '; '))
    exit 1
}

Write-Output ('IAM_SEED_GRAPH_PASS ' + (($values.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' '))
