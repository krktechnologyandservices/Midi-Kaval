using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseStageTransition> CaseStageTransitions => Set<CaseStageTransition>();
    public DbSet<CaseSearchPreset> CaseSearchPresets => Set<CaseSearchPreset>();
    public DbSet<CaseAssignment> CaseAssignments => Set<CaseAssignment>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<VisitNote> VisitNotes => Set<VisitNote>();
    public DbSet<CaseNote> CaseNotes => Set<CaseNote>();
    public DbSet<Intervention> Interventions => Set<Intervention>();
    public DbSet<CourtSitting> CourtSittings => Set<CourtSitting>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<TravelClaim> TravelClaims => Set<TravelClaim>();
    public DbSet<TravelClaimCaseLink> TravelClaimCaseLinks => Set<TravelClaimCaseLink>();
    public DbSet<SyncMutation> SyncMutations => Set<SyncMutation>();
    public DbSet<ReportExportJob> ReportExportJobs => Set<ReportExportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
