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
                entity.HasKey(e => e.Id);
                entity.Property(e => e.State).HasConversion<string>();
            });

            modelBuilder.Entity<WorkflowDefinition>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasMany(w => w.Steps)
                      .WithOne(s => s.WorkflowDefinition)
                      .HasForeignKey(s => s.WorkflowDefinitionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WorkflowStep>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }
    }
}
