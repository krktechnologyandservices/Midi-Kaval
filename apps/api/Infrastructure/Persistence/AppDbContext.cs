using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Entities.Legends;

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
    public DbSet<OffenceType> OffenceTypes => Set<OffenceType>();
    public DbSet<Classification> Classifications => Set<Classification>();
    public DbSet<InterventionCategory> InterventionCategories => Set<InterventionCategory>();
    public DbSet<EducationLevel> EducationLevels => Set<EducationLevel>();
    public DbSet<Occupation> Occupations => Set<Occupation>();
    public DbSet<VisitOutcome> VisitOutcomes => Set<VisitOutcome>();
    public DbSet<CourtOutcome> CourtOutcomes => Set<CourtOutcome>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<PoliceStation> PoliceStations => Set<PoliceStation>();
    public DbSet<CaseStage2Data> CaseStage2Data => Set<CaseStage2Data>();
    public DbSet<CaseStage3Support> CaseStage3Supports => Set<CaseStage3Support>();
    public DbSet<CaseStage4Placement> CaseStage4Placements => Set<CaseStage4Placement>();
    public DbSet<CaseStage5Reintegration> CaseStage5Reintegrations => Set<CaseStage5Reintegration>();
    public DbSet<CaseStage6TerminationExclusion> CaseStage6TerminationExclusions => Set<CaseStage6TerminationExclusion>();
    public DbSet<CaseRelatedCase> CaseRelatedCases => Set<CaseRelatedCase>();
    public DbSet<ProjectBudget> ProjectBudgets => Set<ProjectBudget>();
    public DbSet<BudgetLineItem> BudgetLineItems => Set<BudgetLineItem>();
    public DbSet<BudgetUtilization> BudgetUtilizations => Set<BudgetUtilization>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
