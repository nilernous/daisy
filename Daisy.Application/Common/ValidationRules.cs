using System.Text.RegularExpressions;
using FluentValidation;

namespace Daisy.Application.Common;

/// <summary>Shared validation rules and allowed values.</summary>
public static partial class ValidationRules
{
    /// <summary>Proposed values for Users.Gender (API_DESIGN §5.1).</summary>
    public static readonly string[] Genders = ["Male", "Female", "Other"];

    /// <summary>Proposed values for Products.GenderTarget (API_DESIGN §5.1).</summary>
    public static readonly string[] GenderTargets = ["Men", "Women", "Unisex", "Kids"];

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public static partial Regex SlugRegex();

    [GeneratedRegex(@"^(\+84|0)\d{9,10}$")]
    public static partial Regex PhoneRegex();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    public static partial Regex ColorHexRegex();

    [GeneratedRegex("^[A-Z0-9]{3,50}$")]
    public static partial Regex VoucherCodeRegex();

    public static IRuleBuilderOptions<T, string> Slug<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule.NotEmpty()
            .MaximumLength(maxLength)
            .Matches(SlugRegex())
            .WithMessage("Slug may contain only lowercase letters, digits and single hyphens.");

    public static IRuleBuilderOptions<T, string?> VietnamesePhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(20)
            .Must(p => string.IsNullOrWhiteSpace(p) || PhoneRegex().IsMatch(p.Replace(" ", string.Empty)))
            .WithMessage("Phone must be a Vietnamese phone number, e.g. 0901234567.");

    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Must(p => p.Length is >= 8 and <= 100 && p.Any(char.IsLetter) && p.Any(char.IsDigit))
            .WithMessage("Password must be 8-100 characters and contain at least one letter and one digit.");

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
