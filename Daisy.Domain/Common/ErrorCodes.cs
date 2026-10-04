namespace Daisy.Domain.Common;

/// <summary>
/// Stable error codes returned in the API error body (API_DESIGN §4.3).
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string CartOwnerRequired = "CART_OWNER_REQUIRED";
    public const string InvalidResetToken = "INVALID_RESET_TOKEN";
    public const string IncorrectPassword = "INCORRECT_PASSWORD";
    public const string VoucherNotEligible = "VOUCHER_NOT_ELIGIBLE";
    public const string InvalidCategoryParent = "INVALID_CATEGORY_PARENT";
    public const string InvalidSale = "INVALID_SALE";
    public const string UnsupportedFile = "UNSUPPORTED_FILE";
    public const string CannotModifySelf = "CANNOT_MODIFY_SELF";

    public const string Unauthorized = "UNAUTHORIZED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
    public const string InvalidWebhookSignature = "INVALID_WEBHOOK_SIGNATURE";

    public const string Forbidden = "FORBIDDEN";
    public const string AccountLocked = "ACCOUNT_LOCKED";

    public const string NotFound = "NOT_FOUND";
    public const string VoucherNotFound = "VOUCHER_NOT_FOUND";

    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string SlugAlreadyExists = "SLUG_ALREADY_EXISTS";
    public const string SkuAlreadyExists = "SKU_ALREADY_EXISTS";
    public const string VariantOptionExists = "VARIANT_OPTION_EXISTS";
    public const string VoucherCodeExists = "VOUCHER_CODE_EXISTS";
    public const string ItemUnavailable = "ITEM_UNAVAILABLE";
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string NoItemsSelected = "NO_ITEMS_SELECTED";
    public const string TotalChanged = "TOTAL_CHANGED";
    public const string StockChanged = "STOCK_CHANGED";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string OrderNotCancelable = "ORDER_NOT_CANCELABLE";
    public const string OrderNotPaid = "ORDER_NOT_PAID";
    public const string PaymentNotAvailable = "PAYMENT_NOT_AVAILABLE";
    public const string PaymentExpired = "PAYMENT_EXPIRED";
    public const string PaymentActionNotAllowed = "PAYMENT_ACTION_NOT_ALLOWED";
    public const string TransactionCodeUsed = "TRANSACTION_CODE_USED";
    public const string TransactionRecorded = "TRANSACTION_RECORDED";
    public const string TransactionAlreadyResolved = "TRANSACTION_ALREADY_RESOLVED";
    public const string OrderNotPayable = "ORDER_NOT_PAYABLE";
    public const string ReviewNotAllowed = "REVIEW_NOT_ALLOWED";
    public const string AlreadyReviewed = "ALREADY_REVIEWED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";

    public const string TooManyRequests = "TOO_MANY_REQUESTS";
    public const string EmailServiceUnavailable = "EMAIL_SERVICE_UNAVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";
}
