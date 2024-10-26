#if EF
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RetainerTrack.Database;

internal sealed class PalClientContextFactory : IDesignTimeDbContextFactory<RetainerTrackContext>
{
    public RetainerTrackContext CreateDbContext(string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<RetainerTrackContext>().UseSqlite(
                $"Data Source={Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "pluginConfigs", "RetainerTrack", RetainerTrackPlugin.DatabaseFileName)}");
        return new RetainerTrackContext(optionsBuilder.Options);
    }
}
#endif
