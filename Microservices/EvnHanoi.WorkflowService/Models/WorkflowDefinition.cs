using System;
using System.Collections.Generic;

namespace EvnHanoi.WorkflowService.Models
{
    public class WorkflowDefinition
    {
        public Guid Id { get; set; }

        /// <summary>Loại quy trình theo nghiệp vụ số hóa EVNHANOI</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Phân loại quy trình theo description của enum WorkflowType,
        /// ví dụ: "Quy trình số hóa hồ sơ", "Quy trình mượn/trả hồ sơ kỹ thuật".
        /// WorkflowEngine dùng để tự tìm definition phù hợp khi submit.
        /// </summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>Mô tả chi tiết quy trình</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Phiên bản quy trình, ví dụ: 1.0, 1.1, 2.0</summary>
        public string Version { get; set; } = "1.0";

        /// <summary>Ép buộc kích hoạt – vô hiệu hóa các quy trình cùng loại cũ hơn</summary>
        public bool ForceActivate { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = "System";
        public string? CreatedByUsername { get; set; }
        public string? CreatedByFullName { get; set; }
        public string UpdatedBy { get; set; } = "System";
        public string? UpdatedByUsername { get; set; }
        public string? UpdatedByFullName { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>Định nghĩa sơ đồ quy trình dạng BPMN 2.0 XML</summary>
        public string? BpmnXml { get; set; } = string.Empty;

        public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
    }
}
