using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace MidiKaval.Api.IntegrationTests.Admin;

public class InvitationsSchemaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }

    private async Task<AppDbContext> CreateMigratedDbContextAsync()
    {
        var db = CreateDbContext();
        await db.Database.MigrateAsync();
        return db;
    }

    private static readonly string _testHash = "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3";

    private async Task<(Organisation Org, User User)> SeedOrgAndUserAsync(AppDbContext db)
    {
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            Email = $"inviter-{Guid.NewGuid():N}@example.com",
            Role = UserRoles.Director,
            PasswordHash = _testHash,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (org, user);
    }

    [Fact]
    public async Task InvitationsTable_Exists_WithAllExpectedColumns()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var columns = await db.Database
            .SqlQueryRaw<string>(
                "SELECT column_name FROM information_schema.columns WHERE table_name = 'invitations' ORDER BY column_name")
            .ToListAsync();

        Assert.Contains("id", columns);
        Assert.Contains("organisation_id", columns);
        Assert.Contains("invited_by_user_id", columns);
        Assert.Contains("target_email", columns);
        Assert.Contains("role", columns);
        Assert.Contains("token_hash", columns);
        Assert.Contains("expires_at_utc", columns);
        Assert.Contains("status", columns);
        Assert.Contains("created_at_utc", columns);
        Assert.Contains("confirmed_at_utc", columns);
    }

    [Fact]
    public async Task InvitationsTable_ColumnTypes_AreCorrect()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var columnTypes = await db.Database
            .SqlQueryRaw<(string column_name, string data_type)>(
                """
                SELECT column_name, data_type
                FROM information_schema.columns
                WHERE table_name = 'invitations'
                ORDER BY column_name
                """)
            .ToListAsync();

        Assert.Contains(("id", "uuid"), columnTypes.Select(c => (c.column_name, c.data_type)));
        Assert.Contains(("organisation_id", "uuid"), columnTypes.Select(c => (c.column_name, c.data_type)));
        Assert.Contains(("invited_by_user_id", "uuid"), columnTypes.Select(c => (c.column_name, c.data_type)));
        Assert.Contains(("target_email", "character varying"), columnTypes.Select(c => (c.column_name, c.data_type)));
        Assert.Contains(("role", "character varying"), columnTypes.Select(c => (c.column_name, c.data_type)));
        Assert.Contains(("token_hash", "text"), columnTypes.Select(c => (c.column_name, c.data_type)));
        Assert.Contains(("expires_at_utc", "timestamp with time zone"), columnTypes.Select(c => (c.column_name, c.data_type)));
        Assert.Contains(("status", "character varying"), columnTypes.Select(c => (c.column_name, c.data_type)));
        Assert.Contains(("created_at_utc", "timestamp with time zone"), columnTypes.Select(c => (c.column_name, c.data_type)));
        Assert.Contains(("confirmed_at_utc", "timestamp with time zone"), columnTypes.Select(c => (c.column_name, c.data_type)));
    }

    [Fact]
    public async Task InvitationsTable_ColumnLengths_AreCorrect()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var maxLengths = await db.Database
            .SqlQueryRaw<(string column_name, int? character_maximum_length)>(
                """
                SELECT column_name, character_maximum_length
                FROM information_schema.columns
                WHERE table_name = 'invitations'
                  AND character_maximum_length IS NOT NULL
                ORDER BY column_name
                """)
            .ToListAsync();

        Assert.Contains(("target_email", 320), maxLengths.Select(c => (c.column_name, c.character_maximum_length)));
        Assert.Contains(("role", 32), maxLengths.Select(c => (c.column_name, c.character_maximum_length)));
        Assert.Contains(("status", 16), maxLengths.Select(c => (c.column_name, c.character_maximum_length)));
    }

    [Fact]
    public async Task ConfirmedAtUtc_IsNullable_DefaultsToNull()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var nullableColumns = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT column_name
                FROM information_schema.columns
                WHERE table_name = 'invitations'
                  AND is_nullable = 'YES'
                ORDER BY column_name
                """)
            .ToListAsync();

        Assert.Contains("confirmed_at_utc", nullableColumns);
    }

    [Fact]
    public async Task CreatedAtUtc_IsNotNullable_HasNoDefault()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var notNullColumns = await db.Database
            .SqlQueryRaw<(string column_name, string? column_default)>(
                """
                SELECT column_name, column_default
                FROM information_schema.columns
                WHERE table_name = 'invitations'
                  AND is_nullable = 'NO'
                ORDER BY column_name
                """)
            .ToListAsync();

        Assert.Contains("created_at_utc", notNullColumns.Select(c => c.column_name));

        var createdDefault = notNullColumns
            .FirstOrDefault(c => c.column_name == "created_at_utc")
            .column_default;

        Assert.Null(createdDefault);
    }

    [Fact]
    public async Task StatusColumn_HasDefaultValue_Pending()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "newuser@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();

        var saved = await db.Invitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(InvitationStatus.Pending, saved.Status);
    }

    [Fact]
    public async Task StatusColumn_AcceptsExpiredValue()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "expired@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
            CreatedAtUtc = DateTime.UtcNow,
            Status = InvitationStatus.Expired,
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();

        var saved = await db.Invitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(InvitationStatus.Expired, saved.Status);
    }

    [Fact]
    public async Task StatusColumn_IsNotNull()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var nonNullStatusColumns = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT column_name
                FROM information_schema.columns
                WHERE table_name = 'invitations'
                  AND column_name = 'status'
                  AND is_nullable = 'NO'
                """)
            .ToListAsync();

        Assert.Contains("status", nonNullStatusColumns);
    }

    [Fact]
    public async Task FkColumns_OrganisationIdAndInvitedByUserId_AreNotNull()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var nonNullFkColumns = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT column_name
                FROM information_schema.columns
                WHERE table_name = 'invitations'
                  AND column_name IN ('organisation_id', 'invited_by_user_id')
                  AND is_nullable = 'NO'
                ORDER BY column_name
                """)
            .ToListAsync();

        Assert.Contains("organisation_id", nonNullFkColumns);
        Assert.Contains("invited_by_user_id", nonNullFkColumns);
    }

    [Fact]
    public async Task ForeignKey_OrganisationId_RejectsInvalidOrg()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = Guid.NewGuid(),
            InvitedByUserId = user.Id,
            TargetEmail = "newuser@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ForeignKey_InvitedByUserId_RejectsInvalidUser()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, _) = await SeedOrgAndUserAsync(db);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = Guid.NewGuid(),
            TargetEmail = "newuser@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ForeignKey_OnDeleteRestrict_PreventsOrgDeletion_WhenInvitationsExist()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var orgWithInvitation = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Org With Invitations",
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(orgWithInvitation);

        var orgForUser = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Org For User",
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(orgForUser);

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgForUser.Id,
            Email = $"inviter-{Guid.NewGuid():N}@example.com",
            Role = UserRoles.Director,
            PasswordHash = _testHash,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgWithInvitation.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "newuser@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();

        db.Organisations.Remove(orgWithInvitation);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ForeignKey_OnDeleteRestrict_PreventsUserDeletion_WhenUserSentInvitations()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "newuser@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();

        db.Users.Remove(user);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task NavigationProperties_OrganisationAndUser_AreQueryable()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "navtest@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();

        var saved = await db.Invitations
            .Include(i => i.Organisation)
            .Include(i => i.InvitedByUser)
            .SingleAsync(i => i.Id == invitation.Id);

        Assert.NotNull(saved.Organisation);
        Assert.Equal(org.Id, saved.Organisation.Id);
        Assert.Equal(org.Name, saved.Organisation.Name);
        Assert.NotNull(saved.InvitedByUser);
        Assert.Equal(user.Id, saved.InvitedByUser.Id);
        Assert.Equal(user.Email, saved.InvitedByUser.Email);
    }

    [Fact]
    public async Task UniqueFilteredIndex_RejectsDuplicatePendingEmail_PerOrg()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation1 = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "duplicate@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = $"{_testHash}-1",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation1);
        await db.SaveChangesAsync();

        var invitation2 = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "duplicate@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = $"{_testHash}-2",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation2);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task UniqueFilteredIndex_AllowsDuplicateEmail_AcrossDifferentOrgs()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var org1 = new Organisation { Id = Guid.NewGuid(), Name = "Org 1", CreatedAtUtc = DateTime.UtcNow };
        var org2 = new Organisation { Id = Guid.NewGuid(), Name = "Org 2", CreatedAtUtc = DateTime.UtcNow };
        db.Organisations.AddRange(org1, org2);

        var user1 = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = org1.Id,
            Email = "inviter1@example.com",
            Role = UserRoles.Director,
            PasswordHash = _testHash,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = org2.Id,
            Email = "inviter2@example.com",
            Role = UserRoles.Director,
            PasswordHash = _testHash,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        var invitation1 = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org1.Id,
            InvitedByUserId = user1.Id,
            TargetEmail = "sameperson@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = $"{_testHash}-1",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        var invitation2 = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org2.Id,
            InvitedByUserId = user2.Id,
            TargetEmail = "sameperson@example.com",
            Role = UserRoles.CaseWorker,
            TokenHash = $"{_testHash}-2",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.AddRange(invitation1, invitation2);

        var ex = await Record.ExceptionAsync(() => db.SaveChangesAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task UniqueFilteredIndex_AllowsDuplicateConfirmed_ForSameEmail_AndOrg()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation1 = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "repeat@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = $"{_testHash}-1",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
            Status = InvitationStatus.Confirmed,
            ConfirmedAtUtc = DateTime.UtcNow,
        };
        var invitation2 = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "repeat@example.com",
            Role = UserRoles.CaseWorker,
            TokenHash = $"{_testHash}-2",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
            Status = InvitationStatus.Confirmed,
            ConfirmedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.AddRange(invitation1, invitation2);

        var ex = await Record.ExceptionAsync(() => db.SaveChangesAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task ConfirmedAtUtc_InsertWithoutSetting_DefaultsToNull()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "defaultnull@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();

        var saved = await db.Invitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Null(saved.ConfirmedAtUtc);
    }

    [Fact]
    public async Task CreatedAtUtc_NoDbDefault_ClrDefaultPersists()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "nodbdefault@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
        };

        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();

        var saved = await db.Invitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(DateTime.MinValue, saved.CreatedAtUtc);
    }

    [Fact]
    public async Task Migration_AddsHasPendingRecovery_ToOrganisations()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var orgColumns = await db.Database
            .SqlQueryRaw<(string column_name, string data_type, string? column_default)>(
                """
                SELECT column_name, data_type, column_default
                FROM information_schema.columns
                WHERE table_name = 'organisations'
                  AND column_name = 'has_pending_recovery'
                """)
            .ToListAsync();

        Assert.Single(orgColumns);
        Assert.Equal("boolean", orgColumns[0].data_type);
        Assert.Equal("false", orgColumns[0].column_default?.Trim('(', ')', '\''));
    }

    [Fact]
    public async Task Migration_AppliedMigrationIsExpectedOne()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var migrations = await db.Database
            .SqlQueryRaw<string>(
                "SELECT migration_id FROM public.\"__EFMigrationsHistory\" ORDER BY migration_id")
            .ToListAsync();

        var invitationsMigration = migrations
            .FirstOrDefault(m => m.StartsWith("20260624152225_AddInvitations"));

        Assert.NotNull(invitationsMigration);
        Assert.StartsWith("20260624152225_AddInvitations", invitationsMigration);
    }

    [Fact]
    public async Task InvitationsTable_HasPrimaryKey()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var primaryKeyColumns = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                  ON tc.constraint_name = kcu.constraint_name
                WHERE tc.table_name = 'invitations'
                  AND tc.constraint_type = 'PRIMARY KEY'
                """)
            .ToListAsync();

        Assert.Contains("id", primaryKeyColumns);
    }

    [Fact]
    public async Task DbSet_Invitations_CanInsertAndQuery()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "dbstest@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();

        var saved = await db.Invitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.NotNull(saved);
        Assert.Equal(invitation.Id, saved.Id);
    }

    [Fact]
    public async Task StatusColumn_AcceptsInvalidValue_NoCheckConstraint()
    {
        await using var db = await CreateMigratedDbContextAsync();
        var (org, user) = await SeedOrgAndUserAsync(db);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            InvitedByUserId = user.Id,
            TargetEmail = "invalid-status@example.com",
            Role = UserRoles.SocialWorker,
            TokenHash = _testHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
            Status = "invalid_value",
        };
        db.Invitations.Add(invitation);

        var ex = await Record.ExceptionAsync(() => db.SaveChangesAsync());
        Assert.Null(ex);
    }
}
