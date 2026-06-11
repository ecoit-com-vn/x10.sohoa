using System;
using System.Text.Json.Serialization;

namespace EvnHanoi.WorkflowService.Models
{
    public class WorkflowTask
    {
        public Guid Id { get; set; }
        public Guid WorkflowInstanceId { get; set; }
        public Guid StepId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public string AssignedRole { get; set; } = string.Empty;
        public string? AssigneeUserId { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Completed, Rejected
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        [JsonIgnore]
        public WorkflowInstance? WorkflowInstance { get; set; }
        public WorkflowStep? Step { get; set; }
    }
}
