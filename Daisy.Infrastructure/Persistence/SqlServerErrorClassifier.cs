using Daisy.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Infrastructure.Persistence;

public sealed class SqlServerErrorClassifier : IDatabaseErrorClassifier
{
    // 2601: duplicate key in a unique index; 2627: violation of a UNIQUE / PRIMARY KEY constraint.
    public bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
