using Daisy.Mvc.Services;

namespace Daisy.Mvc.Infrastructure;

/// <summary>
/// Vietnamese messages for the API error codes (API_DESIGN §4.3). The API's own messages are
/// English; the web client switches on the code, never on the message.
/// </summary>
public static class ErrorMessages
{
    public static string For(ApiException ex) => ex.Code switch
    {
        "INVALID_CREDENTIALS" => "Email hoặc mật khẩu không đúng.",
        "ACCOUNT_LOCKED" => "Tài khoản của bạn đã bị khóa. Liên hệ cửa hàng để được hỗ trợ.",
        "EMAIL_ALREADY_EXISTS" => "Email này đã được đăng ký. Hãy đăng nhập hoặc dùng email khác.",
        "INVALID_RESET_TOKEN" => "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Hãy yêu cầu liên kết mới.",
        "INCORRECT_PASSWORD" => "Mật khẩu hiện tại không đúng.",
        "EMAIL_SERVICE_UNAVAILABLE" => "Chưa gửi được email. Vui lòng thử lại sau ít phút.",
        "INSUFFICIENT_STOCK" => ex.Detail<int?>("availableStock") is int left
            ? (left > 0 ? $"Chỉ có thể thêm tối đa {left} sản phẩm nữa." : "Sản phẩm đã hết hàng hoặc giỏ của bạn đã có đủ số lượng còn lại.")
            : "Một số sản phẩm không đủ hàng. Kiểm tra lại giỏ hàng.",
        "ITEM_UNAVAILABLE" => "Sản phẩm này hiện không còn bán.",
        "NO_ITEMS_SELECTED" => "Hãy chọn ít nhất một sản phẩm trong giỏ để thanh toán.",
        "VOUCHER_NOT_FOUND" => "Mã giảm giá không tồn tại.",
        "VOUCHER_NOT_ELIGIBLE" => ex.Detail<string>("reason") switch
        {
            "Inactive" => "Mã giảm giá đang tạm ngưng.",
            "NotStarted" => "Mã giảm giá chưa đến thời gian áp dụng.",
            "Expired" => "Mã giảm giá đã hết hạn.",
            "BelowMinimumOrder" => "Đơn hàng chưa đạt giá trị tối thiểu để dùng mã này.",
            "UsageLimitReached" => "Mã giảm giá đã hết lượt sử dụng.",
            _ => "Không thể áp dụng mã giảm giá này.",
        },
        "TOTAL_CHANGED" => ex.Detail<decimal?>("newTotal") is decimal total
            ? $"Tổng tiền vừa thay đổi thành {Display.Money(total)}. Kiểm tra lại rồi đặt hàng lần nữa."
            : "Tổng tiền vừa thay đổi. Kiểm tra lại rồi đặt hàng lần nữa.",
        "CONCURRENCY_CONFLICT" => "Tồn kho hoặc mã giảm giá vừa thay đổi. Vui lòng kiểm tra lại.",
        "ORDER_NOT_CANCELABLE" => "Đơn hàng này không thể hủy nữa.",
        "PAYMENT_EXPIRED" => "Đã hết thời gian thanh toán cho đơn hàng này.",
        "PAYMENT_NOT_AVAILABLE" => "Đơn hàng này không cần thanh toán chuyển khoản.",
        "REVIEW_NOT_ALLOWED" => "Bạn có thể đánh giá sau khi đơn hàng hoàn thành.",
        "ALREADY_REVIEWED" => "Bạn đã đánh giá sản phẩm này rồi.",
        "INVALID_STATUS_TRANSITION" => "Không thể chuyển đơn sang trạng thái này. Tải lại trang để xem trạng thái mới nhất.",
        "ORDER_NOT_PAID" => "Đơn chuyển khoản VietQR chưa được thanh toán nên chưa thể xác nhận.",
        "PAYMENT_ACTION_NOT_ALLOWED" => "Thao tác thanh toán này không áp dụng cho đơn hàng.",
        "TRANSACTION_CODE_USED" => "Mã giao dịch ngân hàng này đã được dùng cho một đơn khác.",
        "TRANSACTION_RECORDED" => "Giao dịch này đã được ngân hàng báo về. Hãy xử lý trong mục Đối soát chuyển khoản.",
        "TRANSACTION_ALREADY_RESOLVED" => "Giao dịch này đã được xử lý.",
        "ORDER_NOT_PAYABLE" => "Không gán được: đơn phải là đơn VietQR chưa thanh toán và tổng tiền không vượt quá số tiền đã chuyển.",
        "SLUG_ALREADY_EXISTS" => "Đường dẫn (slug) này đã được dùng.",
        "SKU_ALREADY_EXISTS" => "Mã SKU này đã tồn tại.",
        "VARIANT_OPTION_EXISTS" => "Sản phẩm đã có biến thể cùng size và màu.",
        "VOUCHER_CODE_EXISTS" => "Mã giảm giá này đã tồn tại.",
        "INVALID_CATEGORY_PARENT" => "Không thể đặt danh mục vào chính nó hoặc danh mục con của nó.",
        "INVALID_SALE" => "Giá khuyến mãi phải từ 0 đến giá gốc, và thời gian bắt đầu phải trước thời gian kết thúc.",
        "UNSUPPORTED_FILE" => "Chỉ nhận ảnh JPEG, PNG hoặc WebP trong dung lượng cho phép.",
        "CANNOT_MODIFY_SELF" => "Bạn không thể khóa hoặc đổi vai trò của chính mình.",
        "STOCK_CHANGED" => ex.Detail<int?>("currentStock") is int current
            ? $"Tồn kho vừa thay đổi thành {current}. Kiểm tra lại rồi cập nhật."
            : "Tồn kho vừa thay đổi. Kiểm tra lại rồi cập nhật.",
        "TOO_MANY_REQUESTS" => "Bạn thao tác quá nhanh. Vui lòng đợi một chút rồi thử lại.",
        "NOT_FOUND" or "" when ex.Status == System.Net.HttpStatusCode.NotFound => "Không tìm thấy dữ liệu bạn yêu cầu.",
        "FORBIDDEN" => "Bạn không có quyền thực hiện thao tác này.",
        "VALIDATION_FAILED" => "Một số thông tin chưa hợp lệ. Kiểm tra lại các ô được đánh dấu.",
        _ => "Đã có lỗi xảy ra. Vui lòng thử lại.",
    };
}
