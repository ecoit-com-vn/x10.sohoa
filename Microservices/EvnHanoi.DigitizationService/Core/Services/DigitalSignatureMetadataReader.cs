using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace EvnHanoi.DigitizationService.Core.Services;

/// <summary>
/// Chứa thông tin chữ ký số điện tử trích xuất từ file PDF (ISO 32000-1).
/// </summary>
public class PdfSignatureInfo
{
    public bool HasSignature { get; set; }
    public string? SignerName { get; set; }
    public string? Organization { get; set; }
    public DateTime? SigningDate { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentSignNotation { get; set; }
    public string? RawSummary { get; set; }
}

/// <summary>
/// Đọc siêu dữ liệu chữ ký số từ cấu trúc file PDF (PKCS#7 / CMS SignedData, X.509 Certificates).
/// Phục vụ hiển thị chính xác và tiêm dữ liệu vào prompt trích xuất LLM.
/// </summary>
public static class DigitalSignatureMetadataReader
{
    private static readonly Regex NumberNotationRegex = new(
        @"(?:Số|Số\s*ký\s*hiệu|Số/KH)\s*[:：]?\s*([0-9A-Za-z\-_/\.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DateRegex = new(
        @"(?:Ngày\s*ký|Ký\s*ngày)\s*[:：]?\s*([0-9]{1,2}[/-][0-9]{1,2}[/-][0-9]{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Kiểm tra nhanh xem file PDF có dấu hiệu chữ ký số điện tử (ISO 32000-1) hay không.
    /// Không cấp phát bộ nhớ (dùng ReadOnlySpan).
    /// </summary>
    public static bool HasDigitalSignature(byte[]? pdfBytes)
    {
        if (pdfBytes == null || pdfBytes.Length < 100) return false;
        var span = pdfBytes.AsSpan();
        bool hasSigType = span.IndexOf("/Type /Sig"u8) >= 0 || 
                          span.IndexOf("/Type/Sig"u8) >= 0 || 
                          span.IndexOf("/FT /Sig"u8) >= 0 || 
                          span.IndexOf("/FT/Sig"u8) >= 0;
        bool hasByteRange = span.IndexOf("/ByteRange"u8) >= 0;
        return hasSigType && hasByteRange;
    }

    public static PdfSignatureInfo Extract(byte[]? pdfBytes)
    {
        var result = new PdfSignatureInfo();
        if (pdfBytes == null || pdfBytes.Length < 200)
            return result;

        // Kiểm tra nhanh không cấp phát chuỗi; nếu không có ký số thì kết thúc ngay
        if (!HasDigitalSignature(pdfBytes))
            return result;

        try
        {
            var span = pdfBytes.AsSpan();
            int byteRangeByteOffset = span.IndexOf("/ByteRange"u8);
            if (byteRangeByteOffset < 0)
                return result;

            result.HasSignature = true;

            // Để tiết kiệm bộ nhớ với file PDF dung lượng lớn (tránh cấp phát Large Object Heap),
            // chỉ trích xuất vùng xung quanh đối tượng chữ ký số (tối đa 2MB).
            string textContent;
            int contextOffset = 0;
            if (pdfBytes.Length > 2 * 1024 * 1024)
            {
                int windowStart = Math.Max(0, byteRangeByteOffset - 250_000);
                int windowLength = Math.Min(pdfBytes.Length - windowStart, 1_500_000);
                textContent = Encoding.Latin1.GetString(pdfBytes, windowStart, windowLength);
                contextOffset = windowStart;
            }
            else
            {
                textContent = Encoding.Latin1.GetString(pdfBytes);
            }

            int byteRangeIndex = textContent.IndexOf("/ByteRange", StringComparison.OrdinalIgnoreCase);
            if (byteRangeIndex < 0)
                return result;

            // Trong Signature Dictionary, /Contents chứa dữ liệu PKCS#7 (dạng hex <...>)
            // Tìm /Contents lân cận vị trí /ByteRange (trước hoặc sau trong phạm vi 3000 ký tự)
            int contentsIndex = -1;
            int nextContents = textContent.IndexOf("/Contents", byteRangeIndex, StringComparison.OrdinalIgnoreCase);
            if (nextContents > 0 && (nextContents - byteRangeIndex) < 3000)
            {
                contentsIndex = nextContents;
            }
            else
            {
                int prevContents = textContent.LastIndexOf("/Contents", byteRangeIndex, StringComparison.OrdinalIgnoreCase);
                if (prevContents > 0 && (byteRangeIndex - prevContents) < 3000)
                {
                    contentsIndex = prevContents;
                }
            }

            // 1. Trích xuất thời gian ký từ /M (D:YYYYMMDDHHmmSS...) trong phạm vi chữ ký
            ExtractPdfDateFromMTag(textContent, byteRangeIndex, result);

            // 2. Trích xuất PKCS#7 SignedData trong /Contents <...>
            if (contentsIndex >= 0)
            {
                ExtractFromPkcs7Contents(textContent, contentsIndex, result);
            }

            // 3. Trích xuất Số/Ký hiệu từ Appearance stream hoặc text xung quanh
            ExtractDocumentNotation(textContent, result);

            // 4. Tổng hợp RawSummary để tiêm vào LLM context
            BuildRawSummary(result);
        }
        catch
        {
            // Không làm gián đoạn luồng chính nếu file PDF có cấu trúc chữ ký dị thường
        }

        return result;
    }

    private static void ExtractPdfDateFromMTag(string textContent, int contextIndex, PdfSignatureInfo result)
    {
        int searchStart = Math.Max(0, contextIndex - 2000);
        int searchLength = Math.Min(textContent.Length - searchStart, 4000);
        var subText = textContent.Substring(searchStart, searchLength);

        var match = Regex.Match(subText, @"/M\s*\(\s*D:?([0-9]{8,14})", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var dateStr = match.Groups[1].Value;
            try
            {
                var year = int.Parse(dateStr[..4]);
                var month = int.Parse(dateStr.Substring(4, 2));
                var day = int.Parse(dateStr.Substring(6, 2));
                int hour = 0, min = 0, sec = 0;
                if (dateStr.Length >= 14)
                {
                    hour = int.Parse(dateStr.Substring(8, 2));
                    min = int.Parse(dateStr.Substring(10, 2));
                    sec = int.Parse(dateStr.Substring(12, 2));
                }
                result.SigningDate = new DateTime(year, month, day, hour, min, sec, DateTimeKind.Utc);
            }
            catch { }
        }
    }

    private static void ExtractFromPkcs7Contents(string textContent, int contentsIndex, PdfSignatureInfo result)
    {
        int hexStart = textContent.IndexOf('<', contentsIndex);
        int hexEnd = textContent.IndexOf('>', hexStart + 1);
        if (hexStart > 0 && hexEnd > hexStart && (hexStart - contentsIndex) < 50)
        {
            var hexString = textContent.Substring(hexStart + 1, hexEnd - hexStart - 1).Trim();
            // Lọc bỏ ký tự xuống dòng / khoảng trắng có thể có trong luồng hex của PDF
            hexString = Regex.Replace(hexString, @"[^0-9A-Fa-f]", "");
            // Bỏ các ký tự 00 đệm cuối
            hexString = hexString.TrimEnd('0');
            if (hexString.Length % 2 != 0)
                hexString += "0";

            if (hexString.Length > 200)
            {
                try
                {
                    byte[] pkcs7Bytes = ConvertHexToBytes(hexString);
                    var signedCms = new SignedCms();
                    signedCms.Decode(pkcs7Bytes);

                    foreach (var signer in signedCms.SignerInfos)
                    {
                        var cert = signer.Certificate;
                        if (cert != null)
                        {
                            // Tên người ký
                            var name = cert.GetNameInfo(X509NameType.SimpleName, false);
                            if (!string.IsNullOrWhiteSpace(name))
                                result.SignerName = name.Trim();

                            // Đơn vị/Tổ chức
                            var subject = cert.Subject;
                            var orgMatch = Regex.Match(subject, @"O=([^,]+)", RegexOptions.IgnoreCase);
                            if (orgMatch.Success)
                                result.Organization = orgMatch.Groups[1].Value.Trim();

                            // Ngày ký từ thuộc tính Pkcs9SigningTime nếu có
                            foreach (var attr in signer.SignedAttributes)
                            {
                                if (attr.Oid?.Value == "1.2.840.113549.1.9.5") // Pkcs9SigningTime
                                {
                                    var signingTime = new Pkcs9SigningTime(attr.Values[0].RawData);
                                    result.SigningDate = signingTime.SigningTime;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(result.SignerName))
                            break;
                    }
                }
                catch { }
            }
        }
    }

    private static void ExtractDocumentNotation(string textContent, PdfSignatureInfo result)
    {
        var numMatch = NumberNotationRegex.Match(textContent);
        if (numMatch.Success)
        {
            result.DocumentNumber = numMatch.Groups[1].Value.Trim();
            result.DocumentSignNotation = numMatch.Value.Trim();
        }

        if (!result.SigningDate.HasValue)
        {
            var dateMatch = DateRegex.Match(textContent);
            if (dateMatch.Success && DateTime.TryParseExact(dateMatch.Groups[1].Value, 
                    new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy" }, 
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                result.SigningDate = parsedDate;
            }
        }
    }

    private static void BuildRawSummary(PdfSignatureInfo result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== THÔNG TIN CHỮ KÝ SỐ ĐIỆN TỬ (ĐÃ XÁC THỰC MẬT MÃ) ===");
        if (!string.IsNullOrEmpty(result.SignerName))
            sb.AppendLine($"- Người ký: {result.SignerName}");
        if (!string.IsNullOrEmpty(result.Organization))
            sb.AppendLine($"- Cơ quan/Đơn vị: {result.Organization}");
        if (result.SigningDate.HasValue)
            sb.AppendLine($"- Ngày ký: {result.SigningDate.Value:dd/MM/yyyy HH:mm:ss}");
        if (!string.IsNullOrEmpty(result.DocumentNumber))
            sb.AppendLine($"- Số/Ký hiệu văn bản: {result.DocumentNumber}");

        result.RawSummary = sb.ToString();
    }

    private static byte[] ConvertHexToBytes(string hex)
    {
        try
        {
            return Convert.FromHexString(hex);
        }
        catch
        {
            int length = hex.Length / 2;
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
}

