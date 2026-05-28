using System;

namespace EvnHanoi.WorkflowService.Models
{
    public class BorrowRecord
    {
        public Guid Id { get; set; }
        public string DossierId { get; set; } = string.Empty;
        public string RequesterId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public DateTime? BorrowedDate { get; set; }
        public DateTime? ReturnedDate { get; set; }
        public BorrowState State { get; set; }
    }
}
