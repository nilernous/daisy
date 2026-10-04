namespace Daisy.Domain.Entities;

public class UserAddress
{
    public int AddressId { get; set; }

    public int UserId { get; set; }

    public string ReceiverName { get; set; } = string.Empty;

    public string ReceiverPhone { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public string Ward { get; set; } = string.Empty;

    public string StreetAddress { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }

    /// <summary>The address as copied into an order snapshot (BR-28).</summary>
    public string FullAddress => $"{StreetAddress}, {Ward}, {District}, {Province}";
}
