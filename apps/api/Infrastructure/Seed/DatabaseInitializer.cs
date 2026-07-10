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

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var blobStorage = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        await blobStorage.EnsureContainerAsync();

        // Bootstraps the first Director and Vendor accounts. Safe in any environment: each
        // is a no-op unless its own Seed:*:Email/Password config is explicitly set, and each
        // is idempotent (skips if that account already exists). Without these, a fresh
        // production database would have no way to create its first login-capable users.
        var seeder = scope.ServiceProvider.GetRequiredService<AdminUserSeeder>();
        await seeder.SeedAsync();

        var vendorSeeder = scope.ServiceProvider.GetRequiredService<VendorUserSeeder>();
        await vendorSeeder.SeedAsync();

        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        // Everything below creates local-only fixture data (a test field worker, a fake
        // POCSO-DEV-001 case) and must never run outside Development.
        var fieldWorkerSeeder = scope.ServiceProvider.GetRequiredService<FieldWorkerUserSeeder>();
        await fieldWorkerSeeder.SeedAsync();

        var pocsoCaseSeeder = scope.ServiceProvider.GetRequiredService<PocsoCaseSeeder>();
        await pocsoCaseSeeder.SeedAsync();
    }
}
