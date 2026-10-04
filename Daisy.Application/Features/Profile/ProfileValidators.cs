using Daisy.Application.Common;
using FluentValidation;

namespace Daisy.Application.Features.Profile;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator(TimeProvider time)
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).VietnamesePhone();
        RuleFor(x => x.Gender)
            .Must(g => g is null || ValidationRules.Genders.Contains(g))
            .WithMessage($"Gender must be one of: {string.Join(", ", ValidationRules.Genders)}.");
        RuleFor(x => x.DateOfBirth)
            .Must(d => d is null || d < time.GetVietnamToday())
            .WithMessage("Date of birth must be in the past.");
    }
}

public sealed class AddressRequestValidator : AbstractValidator<AddressRequest>
{
    public AddressRequestValidator()
    {
        RuleFor(x => x.ReceiverName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ReceiverPhone).NotEmpty().VietnamesePhone();
        RuleFor(x => x.Province).NotEmpty().MaximumLength(100);
        RuleFor(x => x.District).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Ward).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StreetAddress).NotEmpty().MaximumLength(255);
    }
}
