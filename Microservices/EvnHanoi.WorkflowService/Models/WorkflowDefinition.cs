using System;
using System.Collections.Generic;

namespace EvnHanoi.WorkflowService.Models
{
    public class WorkflowDefinition
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
    }
}
