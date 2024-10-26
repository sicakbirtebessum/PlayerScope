using Microsoft.EntityFrameworkCore;

namespace PlayerScope.Database;

internal sealed class RetainerTrackContext : DbContext
{
    public DbSet<Retainer> Retainers { get; set; }
    public DbSet<Player> Players { get; set; }
    public RetainerTrackContext(DbContextOptions<RetainerTrackContext> options)
        : base(options)
    {
    }
}
