namespace SarahBeauty.Business.Customers;

public sealed record Customer
{
    public string Id { get; init; } = "";
    public int Revision { get; init; }
    public string FullName { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string PreferredContact { get; init; } = "Undecided";
    public string BillingName { get; init; } = "";
    public string BillingAddress { get; init; } = "";
    public string MakeupPreferences { get; init; } = "";
    public string Sensitivities { get; init; } = "";
    public string PrivateNotes { get; init; } = "";
    public string PhotoPermission { get; init; } = "Not asked";
    public string PermissionScope { get; init; } = "";
    public string PermissionNotes { get; init; } = "";
    public bool Archived { get; init; }
    public string ContactWarning => Phone.Length == 0 && Email.Length == 0 ? "Contact information missing" : "";
    public override string ToString() => $"{FullName}{(Archived ? " • Archived" : "")} {ContactWarning}".Trim();
}

public sealed record PermissionHistory(string Status, string Scope, string Notes, string AtUtc);
