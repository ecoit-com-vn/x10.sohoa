using System;
using System.Collections.Generic;

namespace EvnHanoi.WorkflowService.Models
{
    public class WorkflowInstance
    {
        public Guid Id { get; set; }
        public Guid WorkflowDefinitionId { get; set; }
        public string TargetEntityId { get; set; } = string.Empty;
        /// <summary>ID loại quy trình liên kết với WORKFLOW_TYPES</summary>
        public int WorkflowTypeId { get; set; }
        public string Status { get; set; } = "Running"; // Running, Completed, Terminated
        public int CurrentStepOrder { get; set; }
        public string? CurrentNodeId { get; set; }
        public string? CurrentNodeName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public WorkflowDefinition? WorkflowDefinition { get; set; }
        public ICollection<WorkflowTask> Tasks { get; set; } = new List<WorkflowTask>();
    }
}
