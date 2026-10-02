using System.ComponentModel.DataAnnotations;

namespace Distributor.Api.Contracts;

public sealed class PhoneNumberAttribute : ValidationAttribute
{
    public PhoneNumberAttribute() => ErrorMessage = "Enter a valid phone number (7–15 digits, with an optional country code).";

    public override bool IsValid(object? value)
    {
        if (value is null or "") return true;
        if (value is not string phone || phone != phone.Trim() || phone.Length > 25) return false;
        if (phone[0] == '+' && phone.Length == 1) return false;
        for (var i = 0; i < phone.Length; i++)
            if (!char.IsAsciiDigit(phone[i]) && phone[i] is not (' ' or '-' or '(' or ')') && !(i == 0 && phone[i] == '+')) return false;
        var digits = phone.Count(char.IsAsciiDigit);
        return digits is >= 7 and <= 15;
    }
}

public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute() => ErrorMessage = "Select a valid record.";
    public override bool IsValid(object? value) => value is Guid id && id != Guid.Empty;
}

public sealed class OptionalEmailAttribute : ValidationAttribute
{
    public OptionalEmailAttribute() => ErrorMessage = "Enter a valid email address.";
    public override bool IsValid(object? value) => value is null or "" || new EmailAddressAttribute().IsValid(value);
}

public sealed class BusinessDateAttribute : ValidationAttribute
{
    public BusinessDateAttribute() => ErrorMessage = "Enter a valid date.";
    public override bool IsValid(object? value) => value is null || value is DateOnly date && date.Year is >= 1900 and <= 2100;
}
