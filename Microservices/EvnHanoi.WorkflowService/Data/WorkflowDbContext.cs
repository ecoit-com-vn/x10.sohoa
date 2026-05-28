using Microsoft.EntityFrameworkCore;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Data
{
    public class WorkflowDbContext : DbContext
    {
        public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

        public DbSet<BorrowRecord> BorrowRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<BorrowRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.State).HasConversion<string>();
            });
        }
    }
}
