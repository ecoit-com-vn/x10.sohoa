using System;

namespace EvnHanoi.DigitizationService.Models
{
    /// <summary>
    /// Đại diện cho một bản ghi dữ liệu huấn luyện AI-OCR.
    /// Lưu thông tin file ảnh/PDF cùng nhãn văn bản (ground truth) đã được chú thích.
    /// </summary>
    public class OcrTrainingData
    {
        public long Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        
        /// <summary>Loại tài liệu: SoDoLuoi, SoDoTram, BanVeKyThuat, KetQuaKiemTra, Other</summary>
        public string DocumentType { get; set; } = string.Empty;
        
        /// <summary>Văn bản chú thích / ground truth do chuyên gia gán nhãn</summary>
        public string? LabelText { get; set; }
        
        /// <summary>Điểm chất lượng ảnh từ 0-100 (do phần mềm đánh giá)</summary>
        public decimal? QualityScore { get; set; }
        
        /// <summary>Trạng thái huấn luyện: Pending, Labeled, Verified, Rejected</summary>
        public string TrainingStatus { get; set; } = "Pending";
        
        /// <summary>Đã được chuyên gia xác nhận chính xác hay chưa</summary>
        public bool IsVerified { get; set; } = false;
        
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? Notes { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Gán nhãn theo từng trường (JSON: [{fieldName, value}]) — mở rộng thay vì chỉ LabelText cả tài liệu.</summary>
        public string? FieldLabelsJson { get; set; }

        /// <summary>Liên kết tới job huấn luyện lại đã dùng bản ghi này.</summary>
        public string? RetrainJobId { get; set; }

        /// <summary>Nhãn phiên bản dataset export gần nhất chứa bản ghi này.</summary>
        public string? DatasetVersion { get; set; }
    }
}
