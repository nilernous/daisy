using FluentValidation;

namespace Daisy.Application.Features.Checkout;

public sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(x => x.AddressId).GreaterThan(0);
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.VoucherCode).MaximumLength(50);
        RuleFor(x => x.CustomerNote).MaximumLength(500);
        RuleFor(x => x.ExpectedTotal).GreaterThanOrEqualTo(0);
    }
}

public sealed class ApplyVoucherRequestValidator : AbstractValidator<ApplyVoucherRequest>
{
    public ApplyVoucherRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}
