using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Customers;

internal static class CustomerValidation
{
    internal static Customer Normalize(Customer customer)
    {
        var value = customer with
        {
            FullName = customer.FullName.Trim(), Phone = customer.Phone.Trim(), Email = customer.Email.Trim(),
            BillingName = customer.BillingName.Trim(), BillingAddress = customer.BillingAddress.Trim(),
            MakeupPreferences = customer.MakeupPreferences.Trim(), Sensitivities = customer.Sensitivities.Trim(),
            PrivateNotes = customer.PrivateNotes.Trim(), PermissionScope = customer.PermissionScope.Trim(),
            PermissionNotes = customer.PermissionNotes.Trim()
        };
        Validate(value);
        return value;
    }

    private static void Validate(Customer value)
    {
        if (value.FullName.Length is 0 or > 200) throw new ArgumentException("Enter a name of 1 to 200 characters.");
        ContactRules.Validate(value.Phone,value.Email);
        if (!new[] { "Undecided", "Phone", "Text", "Email" }.Contains(value.PreferredContact))
            throw new ArgumentException("Select a supported contact preference.");
        if (!new[] { "Not asked", "Granted", "Declined", "Withdrawn" }.Contains(value.PhotoPermission))
            throw new ArgumentException("Select a supported photo permission status.");
        if (value.Email.Length > 254 || new[] { value.Phone, value.BillingName, value.BillingAddress,
            value.MakeupPreferences, value.Sensitivities, value.PrivateNotes, value.PermissionNotes,
            value.PermissionScope }.Any(s => s.Length > 10000))
            throw new ArgumentException("Shorten the supplied details before saving.");
    }
}
