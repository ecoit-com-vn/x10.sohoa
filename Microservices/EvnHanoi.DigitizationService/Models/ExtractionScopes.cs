using System;
using System.Collections.Generic;

namespace EvnHanoi.DigitizationService.Models
{
    /// <summary>
    /// Phạm vi trang cần bóc tách. Bước OCR LUÔN chạy trên toàn bộ trang (để PDF 2 lớp đầy đủ và
    /// tìm kiếm toàn văn không bị hụt) — phạm vi này chỉ giới hạn số trang được gửi lên LLM ở bước
    /// bóc tách, vì đó là bước tốn thời gian nhất (đo được ~30-60 giây mỗi trang) và với phần lớn
    /// biểu mẫu ngành điện thì dữ liệu cần lấy chỉ nằm ở trang đầu và/hoặc trang cuối.
    /// </summary>
    public static class ExtractionScopes
    {
        public const string FirstPage = "FirstPage";
        public const string LastPage = "LastPage";
        public const string FirstAndLastPage = "FirstAndLastPage";
        public const string AllPages = "AllPages";

        /// <summary>
        /// Mặc định khi message KHÔNG mang giá trị nào: bóc tách mọi trang — giữ nguyên hành vi
        /// trước đây cho các luồng chưa gửi trường này (bóc tách lại, luồng thiết bị, message cũ
        /// còn trong queue). Riêng UI upload trực tiếp chủ động gửi FirstAndLastPage.
        /// </summary>
        public const string Default = AllPages;

        public static bool IsValid(string? scope) =>
            scope is FirstPage or LastPage or FirstAndLastPage or AllPages;

        /// <summary>
        /// Trả về danh sách SỐ TRANG (bắt đầu từ 1) cần bóc tách, đã sắp xếp tăng dần và không trùng.
        /// Giá trị lạ/rỗng được coi như <see cref="Default"/>. Tài liệu 1 trang thì mọi phạm vi đều
        /// cho ra đúng trang đó (FirstAndLastPage không nhân đôi).
        /// </summary>
        public static IReadOnlyList<int> ResolvePageNumbers(string? scope, int totalPages)
        {
            if (totalPages <= 0) return Array.Empty<int>();

            var normalized = IsValid(scope) ? scope! : Default;
            var pages = new SortedSet<int>();

            switch (normalized)
            {
                case FirstPage:
                    pages.Add(1);
                    break;
                case LastPage:
                    pages.Add(totalPages);
                    break;
                case FirstAndLastPage:
                    pages.Add(1);
                    pages.Add(totalPages);
                    break;
                default:
                    for (int p = 1; p <= totalPages; p++) pages.Add(p);
                    break;
            }

            return new List<int>(pages);
        }

        /// <summary>Nhãn tiếng Việt để ghi log cho dễ đọc khi truy vết job.</summary>
        public static string Describe(string? scope) => (IsValid(scope) ? scope : Default) switch
        {
            FirstPage => "chỉ trang đầu",
            LastPage => "chỉ trang cuối",
            FirstAndLastPage => "trang đầu + trang cuối",
            _ => "tất cả các trang",
        };
    }
}
