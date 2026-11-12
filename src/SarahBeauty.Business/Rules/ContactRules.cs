using System.Net.Mail;

namespace SarahBeauty.Business.Rules;

internal static class ContactRules
{
    internal static void Validate(string phone,string email)
    {
        if(phone.Length>0&&(phone.Count(char.IsDigit) is <7 or >15||phone.Any(c=>!char.IsDigit(c)&&!" +()- .".Contains(c))))throw new ArgumentException("Enter a phone number with 7 to 15 digits, or leave it blank.");
        if(email.Length>254||email.Length>0&&(!MailAddress.TryCreate(email,out var address)||address.Address!=email||!address.Host.Contains('.')))throw new ArgumentException("Enter an email address such as name@example.com, or leave it blank.");
    }
}
