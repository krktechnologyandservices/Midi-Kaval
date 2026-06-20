using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=midikaval;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        return new AppDbContext(optionsBuilder.Options);
    }
}
