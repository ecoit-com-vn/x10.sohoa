// Microservices/EvnHanoi.ReportService/Core/DTOs/ReportUnitPublishDtos.cs
using System.Collections.Generic;

namespace EvnHanoi.ReportService.Core.DTOs
{
    /// <summary>
    /// Dữ liệu một báo cáo kèm cấu hình xuất bản của đơn vị hiện tại, phục vụ màn lưới danh sách.
    /// </summary>
    public class ReportUnitPublishDto
    {
        public long? Id { get; set; }
        public long ReportId { get; set; }
        public string ReportCode { get; set; } = string.Empty;
        public string ReportName { get; set; } = string.Empty;
        public bool IsPublish { get; set; }
        public List<long> RoleIds { get; set; } = new();
    }

    /// <summary>
    /// Dữ liệu lưu nháp / công bố cấu hình vai trò xem báo cáo của đơn vị.
    /// </summary>
    public class ReportUnitPublishSaveDto
    {
        public long ReportId { get; set; }
        public bool IsPublish { get; set; }
        public List<long> RoleIds { get; set; } = new();

        /// <summary>Chỉ dùng khi Admin hệ thống thao tác thay cho một đơn vị cụ thể.</summary>
        public long? UnitId { get; set; }
    }
}
