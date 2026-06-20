using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.Storage;

namespace MidiKaval.Api.Infrastructure.Seed;

public static class DatabaseInitializer
{
    public static async Task ApplyMigrationsAndSeedAsync(this WebApplication app)
    {
        if (app.Environment.IsTesting())
        {
            return;
        }

        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<AdminUserSeeder>();
        await seeder.SeedAsync();

        var fieldWorkerSeeder = scope.ServiceProvider.GetRequiredService<FieldWorkerUserSeeder>();
        await fieldWorkerSeeder.SeedAsync();

        var pocsoCaseSeeder = scope.ServiceProvider.GetRequiredService<PocsoCaseSeeder>();
        await pocsoCaseSeeder.SeedAsync();

        var blobStorage = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        await blobStorage.EnsureContainerAsync();
    }
}
