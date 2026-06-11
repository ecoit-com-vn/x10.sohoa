using System;

namespace EvnHanoi.WorkflowService.Models
{
    public class WorkflowHistory
    {
        public Guid Id { get; set; }
        public Guid WorkflowInstanceId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // e.g. Submit, Approve, Reject, Return
        public string ActionByUserId { get; set; } = string.Empty;
        public string? Comment { get; set; }
        public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    }
}
