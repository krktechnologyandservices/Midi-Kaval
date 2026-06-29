using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Audit;

namespace MidiKaval.Api.UnitTests.Infrastructure.Audit;

public class AuditServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task RecordAsync_WithTargetUserSnapshot_PersistsJsonCorrectly()
    {
        // Arrange
        using var db = CreateContext();
        var service = new AuditService(db);
        var snapshot = new TargetUserSnapshotDto("user@example.com", "John Doe", "Director");

        // Act
        await service.RecordAsync(
            "test_event",
            Guid.NewGuid(),
            actorUserId: Guid.NewGuid(),
            targetUserSnapshot: snapshot);

        // Assert
        var saved = await db.AuditEvents.SingleAsync();
        Assert.NotNull(saved.TargetUserSnapshot);

        var deserialized = JsonSerializer.Deserialize<TargetUserSnapshotDto>(saved.TargetUserSnapshot);
        Assert.NotNull(deserialized);
        Assert.Equal("user@example.com", deserialized.Email);
        Assert.Equal("John Doe", deserialized.Name);
        Assert.Equal("Director", deserialized.Role);
    }

    [Fact]
    public async Task RecordAsync_WithActorIpAddress_PersistsIpString()
    {
        // Arrange
        using var db = CreateContext();
        var service = new AuditService(db);

        // Act
        await service.RecordAsync(
            "test_event",
            Guid.NewGuid(),
            actorUserId: Guid.NewGuid(),
            actorIpAddress: "192.168.1.1");

        // Assert
        var saved = await db.AuditEvents.SingleAsync();
        Assert.Equal("192.168.1.1", saved.ActorIpAddress);
    }

    [Fact]
    public async Task RecordAsync_WithNullFields_StoresNullForNewColumns()
    {
        // Arrange
        using var db = CreateContext();
        var service = new AuditService(db);

        // Act — simulate existing behaviour where snapshot/IP are not provided
        await service.RecordAsync(
            "test_event",
            Guid.NewGuid(),
            actorUserId: Guid.NewGuid(),
            metadata: new Dictionary<string, object?> { ["key"] = "value" });

        // Assert
        var saved = await db.AuditEvents.SingleAsync();
        Assert.Null(saved.TargetUserSnapshot);
        Assert.Null(saved.ActorIpAddress);
        Assert.NotNull(saved.MetadataJson);
        Assert.Equal("test_event", saved.EventType);
    }

    [Fact]
    public async Task RecordAsync_WithSnapshotSupportsIPv6()
    {
        // Arrange
        using var db = CreateContext();
        var service = new AuditService(db);
        var ipv6Address = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";
        var snapshot = new TargetUserSnapshotDto("admin@org.com", "Admin User", "Coordinator");

        // Act
        await service.RecordAsync(
            "test_event",
            Guid.NewGuid(),
            actorUserId: Guid.NewGuid(),
            targetUserSnapshot: snapshot,
            actorIpAddress: ipv6Address);

        // Assert
        var saved = await db.AuditEvents.SingleAsync();
        Assert.Equal(ipv6Address, saved.ActorIpAddress);
        Assert.NotNull(saved.TargetUserSnapshot);
    }
}
