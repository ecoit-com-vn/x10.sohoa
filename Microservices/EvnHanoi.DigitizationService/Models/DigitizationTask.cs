using System;

namespace EvnHanoi.DigitizationService.Models
{
    public class DigitizationTask
    {
        public Guid Id { get; set; }
        public string DossierId { get; set; } = string.Empty;
        public Guid WorkflowStepId { get; set; }
        public string AssignedToUserId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Failed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
