using Microsoft.EntityFrameworkCore;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Data
{
    public class WorkflowDbContext : DbContext
    {
        public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

        public DbSet<BorrowRecord> BorrowRecords { get; set; }
        public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
        public DbSet<WorkflowStep> WorkflowSteps { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BorrowRecord>(entity =>
            {
                entity.ToTable("BORROWRECORDS");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("ID");
                entity.Property(e => e.DossierId).HasColumnName("DOSSIERID");
                entity.Property(e => e.RequesterId).HasColumnName("REQUESTERID");
                entity.Property(e => e.Reason).HasColumnName("REASON");
                entity.Property(e => e.RequestDate).HasColumnName("REQUESTDATE");
                entity.Property(e => e.ApprovedDate).HasColumnName("APPROVEDDATE");
                entity.Property(e => e.BorrowedDate).HasColumnName("BORROWEDDATE");
                entity.Property(e => e.ReturnedDate).HasColumnName("RETURNEDDATE");
                entity.Property(e => e.State).HasColumnName("STATE").HasConversion<string>();
            });

            modelBuilder.Entity<WorkflowDefinition>(entity =>
            {
                entity.ToTable("WORKFLOWDEFINITIONS");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("ID");
                entity.Property(e => e.Name).HasColumnName("NAME");
                entity.Property(e => e.Description).HasColumnName("DESCRIPTION");
                entity.Property(e => e.Version).HasColumnName("VERSION");
                entity.Property(e => e.ForceActivate).HasColumnName("FORCEACTIVATE");
                entity.Property(e => e.CreatedAt).HasColumnName("CREATEDAT");
                entity.Property(e => e.UpdatedAt).HasColumnName("UPDATEDAT");
                entity.Property(e => e.IsActive).HasColumnName("ISACTIVE");
                entity.HasMany(w => w.Steps)
                      .WithOne(s => s.WorkflowDefinition)
                      .HasForeignKey(s => s.WorkflowDefinitionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WorkflowStep>(entity =>
            {
                entity.ToTable("WORKFLOWSTEPS");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("ID");
                entity.Property(e => e.WorkflowDefinitionId).HasColumnName("WORKFLOWDEFINITIONID");
                entity.Property(e => e.StepName).HasColumnName("STEPNAME");
                entity.Property(e => e.Order).HasColumnName("Order");
                entity.Property(e => e.RequiredRole).HasColumnName("REQUIREDROLE");
                entity.Property(e => e.ActionType).HasColumnName("ACTIONTYPE");
            });
        }
    }
}
