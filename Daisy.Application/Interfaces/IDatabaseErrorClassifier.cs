using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Interfaces;

/// <summary>Recognizes provider-specific database errors without referencing the provider.</summary>
public interface IDatabaseErrorClassifier
{
    bool IsUniqueConstraintViolation(DbUpdateException exception);
}
