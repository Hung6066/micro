namespace His.Hope.IdentityService.Application.OpenShip;

public static class OpenShipConsoleClaims
{
    /// <summary>
    /// Multi-valued claim (one value per IAM group key) consumed by the OpenShip
    /// console OIDC clients. Issued to access and identity tokens.
    /// </summary>
    public const string Groups = "groups";
}
