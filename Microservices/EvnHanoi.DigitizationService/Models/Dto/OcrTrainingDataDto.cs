using System;
using Microsoft.AspNetCore.Http;

namespace EvnHanoi.DigitizationService.Models.Dto
{
    // ─── REQUEST DTOs ────────────────────────────────────────────────────────────

    /// <summary>Request upload file dữ liệu huấn luyện</summary>
    public class UploadTrainingDataRequest
    {
        public IFormFile File { get; set; } = null!;
        public string DocumentType { get; set; } = "Other";
        public string? LabelText { get; set; }
        public string? Notes { get; set; }
        public string UploadedBy { get; set; } = "System";
    }

    /// <summary>Request cập nhật nhãn và trạng thái cho bản ghi dữ liệu huấn luyện</summary>
    public class UpdateTrainingDataRequest
    {
        public string? LabelText { get; set; }
        public string DocumentType { get; set; } = "Other";
        public string TrainingStatus { get; set; } = "Pending";
        public decimal? QualityScore { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Request xác nhận (verify) bản ghi dữ liệu</summary>
    public class VerifyTrainingDataRequest
    {
        public bool IsVerified { get; set; }
        public string VerifiedBy { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    /// <summary>Yêu cầu 91 (mở rộng) — gán nhãn theo từng trường thay vì cả tài liệu.</summary>
    public class UpdateFieldLabelsRequest
    {
        public string FieldLabelsJson { get; set; } = "[]";
    }

    /// <summary>Yêu cầu 91 (mở rộng) — liên kết bản ghi với 1 job huấn luyện lại.</summary>
    public class LinkRetrainingJobRequest
    {
        public string? DatasetVersion { get; set; }
        public string? Notes { get; set; }
        public string TriggeredBy { get; set; } = "System";
    }

    // ─── RESPONSE DTOs ────────────────────────────────────────────────────────────

    /// <summary>Thông tin tóm tắt của một bản ghi dữ liệu huấn luyện (dùng trong danh sách)</summary>
    public class OcrTrainingDataSummaryDto
    {
        public long Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string TrainingStatus { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public decimal? QualityScore { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    /// <summary>Chi tiết đầy đủ của một bản ghi dữ liệu huấn luyện</summary>
    public class OcrTrainingDataDetailDto : OcrTrainingDataSummaryDto
    {
        public string FilePath { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string? LabelText { get; set; }
        public string? Notes { get; set; }
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Kết quả phân trang danh sách dữ liệu huấn luyện</summary>
    public class PagedResult<T>
    {
        public System.Collections.Generic.List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }
}
