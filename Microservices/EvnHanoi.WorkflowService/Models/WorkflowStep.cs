using System;
using System.Text.Json.Serialization;

namespace EvnHanoi.WorkflowService.Models
{
    public class WorkflowStep
    {
        public Guid Id { get; set; }
        public Guid WorkflowDefinitionId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public int Order { get; set; }
        public string RequiredRole { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty; // e.g., "Scan", "DataEntry", "Approve"

        [JsonIgnore]
        public WorkflowDefinition? WorkflowDefinition { get; set; }
    }
}
