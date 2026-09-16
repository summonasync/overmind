using Microsoft.EntityFrameworkCore;

namespace Overmind.Infrastructure.Persistence;

public static class UniqueViolation
{
    public static bool IsOn(DbUpdateException ex, string constraint) =>
        ex.InnerException?.Message.Contains(
            $"UNIQUE constraint failed: {constraint}", StringComparison.OrdinalIgnoreCase) == true;
}
