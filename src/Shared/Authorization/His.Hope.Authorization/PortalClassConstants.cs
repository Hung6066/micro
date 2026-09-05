using His.Hope.SharedKernel.Protocol;

namespace His.Hope.Authorization;

public static class PortalClassConstants
{
    public const string Claim = HisHopeProtocolConstants.Claims.PortalClass;
    public const string Operator = HisHopeProtocolConstants.PortalClasses.Operator;
    public const string CustomerOperator = "customer_operator";
    public const string EndUser = "end_user";
    public const string PrivilegedOperator = "privileged_operator";
}
