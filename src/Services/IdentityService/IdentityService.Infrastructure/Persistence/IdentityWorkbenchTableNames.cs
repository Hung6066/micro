namespace His.Hope.IdentityService.Infrastructure.Persistence;

/// <summary>Physical Identity Workbench table names. Keep migrations stable; new code references this catalog.</summary>
internal static class IdentityWorkbenchTableNames
{
    public const string Scopes = "iam_scopes";
    public const string Services = "iam_service_definitions";
    public const string PermissionSets = "iam_permission_sets";
    public const string Assignments = "iam_permission_set_assignments";
    public const string WorkloadRoles = "iam_workload_roles";
    public const string Boundaries = "iam_permission_boundaries";
    public const string ResourcePolicies = "iam_resource_policies";
    public const string Groups = "iam_groups";
    public const string GroupMemberships = "iam_group_memberships";
    public const string Permissions = "iam_permissions";
}
