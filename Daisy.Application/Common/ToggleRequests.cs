namespace Daisy.Application.Common;

public sealed record SetActiveRequest(bool IsActive);

public sealed record SetVisibleRequest(bool IsVisible);

public sealed record SetFeaturedRequest(bool IsFeatured);
