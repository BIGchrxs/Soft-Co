namespace SoftCo.Data;

/// <summary>
/// The four roles this first slice needs. Deliberately fewer than the nine in the developer brief:
/// the tracker module only has to distinguish who may see money, who may change it, and who may
/// only look. The remaining roles arrive with the modules that need them.
/// </summary>
public static class Roles
{
    /// <summary>Full access, including user administration.</summary>
    public const string Admin = "Admin";

    /// <summary>Records payments, sees every value, exports.</summary>
    public const string Finance = "Finance";

    /// <summary>Creates and progresses orders; sees invoice values but cannot record payments.</summary>
    public const string Procurement = "Procurement";

    /// <summary>Read-only, and never sees payment or invoice values.</summary>
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Finance, Procurement, Viewer];

    /// <summary>Roles permitted to see money columns anywhere in the UI.</summary>
    public const string CanSeeValues = Admin + "," + Finance + "," + Procurement;

    /// <summary>Roles permitted to record or amend payments.</summary>
    public const string CanRecordPayments = Admin + "," + Finance;

    /// <summary>Roles permitted to create or edit orders, suppliers and projects.</summary>
    public const string CanEditOrders = Admin + "," + Procurement + "," + Finance;
}
