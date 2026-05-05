namespace PropertyManagement.Domain.Common;

public static class Roles
{
    public const string FirmAdmin = "FirmAdmin";
    public const string Lawyer = "Lawyer";
    public const string Paralegal = "Paralegal";
    public const string ClientAdmin = "ClientAdmin";
    public const string ClientUser = "ClientUser";
    public const string Auditor = "Auditor";

    public static readonly string[] All =
    {
        FirmAdmin, Lawyer, Paralegal, ClientAdmin, ClientUser, Auditor
    };

    public static readonly string[] FirmStaff = { FirmAdmin, Lawyer, Paralegal };
    public static readonly string[] ClientStaff = { ClientAdmin, ClientUser };
}
