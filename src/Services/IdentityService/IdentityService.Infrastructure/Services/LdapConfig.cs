namespace His.Hope.IdentityService.Infrastructure.Services;

public class LdapConfig
{
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; }
    public string BindDn { get; set; } = string.Empty;
    public string BindPassword { get; set; } = string.Empty;
    public string SearchBase { get; set; } = string.Empty;
    public string SearchFilter { get; set; } = "(objectClass=user)";
    public string[] Attributes { get; set; } = { "sAMAccountName", "mail", "givenName", "sn", "displayName", "userPrincipalName", "memberOf", "userAccountControl" };
    public int SyncIntervalMinutes { get; set; } = 15;
    public bool Enabled { get; set; }

    public string UserNameAttribute { get; set; } = "sAMAccountName";
    public string EmailAttribute { get; set; } = "mail";
    public string FirstNameAttribute { get; set; } = "givenName";
    public string LastNameAttribute { get; set; } = "sn";
    public string DisplayNameAttribute { get; set; } = "displayName";
    public string MemberOfAttribute { get; set; } = "memberOf";
    public string UserAccountControlAttribute { get; set; } = "userAccountControl";

    public Dictionary<string, string> GroupRoleMapping { get; set; } = new()
    {
        ["CN=Doctors"] = "Provider",
        ["CN=Nurses"] = "Nurse",
        ["CN=LabStaff"] = "LabTechnician",
        ["CN=PharmacyStaff"] = "Pharmacist",
        ["CN=AdminStaff"] = "Admin"
    };
}
