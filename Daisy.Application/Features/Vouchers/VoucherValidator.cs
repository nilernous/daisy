using Daisy.Application.Common;
using Daisy.Domain.Enums;
using FluentValidation;

namespace Daisy.Application.Features.Vouchers;

/// <summary>Voucher definition rules (BR-25).</summary>
public sealed class VoucherRequestValidator : AbstractValidator<VoucherRequest>
{
    public VoucherRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Must(c => ValidationRules.VoucherCodeRegex().IsMatch(c.Trim().ToUpperInvariant()))
            .WithMessage("Code must be 3-50 uppercase letters or digits.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DiscountType).IsInEnum();
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(100)
            .When(x => x.DiscountType == DiscountType.Percent)
            .WithMessage("A percent discount cannot be more than 100.");
        RuleFor(x => x.MaxDiscountAmount).GreaterThanOrEqualTo(0).When(x => x.MaxDiscountAmount is not null);
        RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UsageLimit).GreaterThanOrEqualTo(1).When(x => x.UsageLimit is not null);
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt).WithMessage("End time must be after start time.");
    }
}
