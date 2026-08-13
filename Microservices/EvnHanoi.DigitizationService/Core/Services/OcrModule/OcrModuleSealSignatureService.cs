using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EvnHanoi.DigitizationService.Core.ImageProcessing;
using EvnHanoi.DigitizationService.Helpers;
using EvnHanoi.DigitizationService.Models.OcrModule;
using EvnHanoi.DigitizationService.Repositories.OcrModule;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.DigitizationService.Services.OcrModule;
using EvnHanoi.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace EvnHanoi.DigitizationService.Core.Services.OcrModule;

public class SealSignatureRunResult
{
    public int SealCount { get; set; }
    public int SignatureCount { get; set; }
}

/// <summary>
/// Yêu cầu 94 — điều phối việc dựng lại ảnh từng trang từ PDF gốc đã lưu trên MinIO (dùng lại
/// PDFtoImage, ở OcrModuleImageDpi.DisplayDpi — CHÚ Ý: đây là DPI khác với OcrWorker.cs dùng lúc OCR
/// văn bản, xem OcrModuleImageDpi để biết vì sao 2 giá trị này không được lệch nhau), sinh ứng viên
/// con dấu VÀ chữ ký bằng
/// SealSignatureDetector — cả 2 loại đều quét trực tiếp pixel ảnh trang, KHÔNG dựa vào box của vùng
/// văn bản OCR — rồi XÁC THỰC từng ứng viên bằng cách cắt ảnh và gửi cho LLM server hiện có
/// (AIModelServers:LlmServerUrl, model multimodal, cùng HttpClient "LlmClient" đã dùng cho spellcheck).
/// Chỉ ứng viên được LLM xác nhận mới được ghi nhận là Seal/Signature thật — ghi thành region MỚI cho
/// cả 2 loại (không còn "gắn nhãn lại" 1 vùng text OCR có sẵn như trước, vì box OCR không khớp hình
/// dạng/kích thước thật của nét chữ ký).
/// </summary>
public interface IOcrModuleSealSignatureService
{
    /// <summary>pageNumber = null → xử lý toàn bộ Job; có giá trị → chỉ dựng lại ảnh + quét đúng trang đó.</summary>
    Task<SealSignatureRunResult> RunAsync(OcrModuleJob job, int? pageNumber = null);
}

public class OcrModuleSealSignatureService : IOcrModuleSealSignatureService
{
    private const int MaxCandidatesPerVlmCall = 12;

    private readonly IMinioStorageService _minioService;
    private readonly IOcrModuleRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public OcrModuleSealSignatureService(
        IMinioStorageService minioService,
        IOcrModuleRepository repository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _minioService = minioService;
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<SealSignatureRunResult> RunAsync(OcrModuleJob job, int? pageNumber = null)
    {
        using var fileStream = await _minioService.DownloadFileAsync(job.SourceBucket, job.SourceFilePath);
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        var pdfBytes = ms.ToArray();

        var pageCount = PDFtoImage.Conversion.GetPageCount(pdfBytes);
        var newRegions = new List<OcrModuleRegion>();

        for (var i = 0; i < pageCount; i++)
        {
            var currentPageNumber = i + 1;
            // Chỉ định 1 trang cụ thể → bỏ qua render/quét các trang còn lại (tiết kiệm chi phí đáng kể
            // với tài liệu nhiều trang, thay vì luôn xử lý lại toàn bộ Job như trước).
            if (pageNumber.HasValue && pageNumber.Value != currentPageNumber) continue;

            // Chạy lại đúng 1 trang này → xóa mềm các vùng Seal/Signature đã nhận diện trước đó của trang
            // đó, tránh nhân đôi dữ liệu khi người dùng bấm chạy lại nhiều lần.
            await _repository.DeleteSealAndSignatureRegionsAsync(job.Id, currentPageNumber);

            using var imgStream = new MemoryStream();
            var renderOptions = new PDFtoImage.RenderOptions { Dpi = OcrModuleImageDpi.DisplayDpi, WithAnnotations = true };
            PDFtoImage.Conversion.SaveJpeg(imgStream, pdfBytes, password: null, page: i, options: renderOptions);
            var pageImageBytes = imgStream.ToArray();

            if (pageImageBytes.Length == 0) continue;

            var sealCandidates = SealSignatureDetector.DetectSealCandidates(pageImageBytes, currentPageNumber);
            var signatureCandidates = SealSignatureDetector.DetectSignatureCandidates(pageImageBytes, currentPageNumber);

            if (sealCandidates.Count == 0 && signatureCandidates.Count == 0) continue;

            using var image = Image.Load<Rgba32>(pageImageBytes);
            var verdicts = await VerifyCandidatesAsync(image, sealCandidates, signatureCandidates);

            foreach (var (candidate, verdict) in verdicts.SealVerdicts)
            {
                newRegions.Add(ToRegion(job.Id, candidate, verdict, "Seal", "(Con dấu — xác thực bằng LLM vision)"));
            }

            foreach (var (candidate, verdict) in verdicts.SignatureVerdicts)
            {
                newRegions.Add(ToRegion(job.Id, candidate, verdict, "Signature", "(Chữ ký — xác thực bằng LLM vision)"));
            }
        }

        if (newRegions.Count > 0)
        {
            await _repository.InsertRegionsAsync(newRegions);
        }

        return new SealSignatureRunResult
        {
            SealCount = newRegions.Count(r => r.RegionType == "Seal"),
            SignatureCount = newRegions.Count(r => r.RegionType == "Signature"),
        };
    }

    private static OcrModuleRegion ToRegion(string jobId, InkRegionCandidate candidate, double score, string regionType, string textRaw) => new()
    {
        Id = UuidHelper.NewUuid(),
        JobId = jobId,
        PageNumber = candidate.PageNumber,
        BoxX0 = candidate.BoxX0,
        BoxY0 = candidate.BoxY0,
        BoxX1 = candidate.BoxX1,
        BoxY1 = candidate.BoxY1,
        TextRaw = textRaw,
        RegionType = regionType,
        SealSignatureScore = score,
        Status = "Detected",
    };

    private sealed record VlmVerdicts(
        List<(InkRegionCandidate Candidate, double Score)> SealVerdicts,
        List<(InkRegionCandidate Candidate, double Score)> SignatureVerdicts);

    /// <summary>
    /// Cắt ảnh từng ứng viên (con dấu + chữ ký) của 1 trang, gộp vào 1 request multimodal duy nhất gửi
    /// LLM server để xác thực — hạn chế số lần gọi model (rất chậm với ảnh) xuống 1 lần/trang.
    /// </summary>
    private async Task<VlmVerdicts> VerifyCandidatesAsync(
        Image<Rgba32> pageImage, List<InkRegionCandidate> sealCandidates, List<InkRegionCandidate> signatureCandidates)
    {
        // Chia đều ngân sách slot cho 2 loại thay vì ưu tiên con dấu trước — nếu không, khi 1 trang có
        // nhiều ứng viên con dấu (kể cả dương tính giả), ứng viên chữ ký thật có thể bị chiếm hết chỗ,
        // không bao giờ được gửi cho LLM xác thực.
        var halfBudget = MaxCandidatesPerVlmCall / 2;
        var sealSlots = sealCandidates.Take(halfBudget).ToList();
        var signatureBudget = MaxCandidatesPerVlmCall - sealSlots.Count;
        var signatureSlots = signatureCandidates.Take(signatureBudget).ToList();
        var sealBudget = MaxCandidatesPerVlmCall - signatureSlots.Count;
        if (sealSlots.Count < sealBudget)
        {
            sealSlots = sealCandidates.Take(sealBudget).ToList();
        }

        var empty = new VlmVerdicts([], []);
        if (sealSlots.Count == 0 && signatureSlots.Count == 0) return empty;

        var contentParts = new List<object>
        {
            new { type = "text", text = "[DANH SÁCH ỨNG VIÊN]:" },
        };

        var index = 1;
        foreach (var seal in sealSlots)
        {
            var crop = ImageCropHelper.CropToBase64Jpeg(pageImage, seal.BoxX0, seal.BoxY0, seal.BoxX1, seal.BoxY1);
            contentParts.Add(new { type = "text", text = $"Ứng viên #{index} (nghi vấn: con dấu):" });
            contentParts.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{crop}" } });
            index++;
        }

        foreach (var signature in signatureSlots)
        {
            var crop = ImageCropHelper.CropToBase64Jpeg(pageImage, signature.BoxX0, signature.BoxY0, signature.BoxX1, signature.BoxY1);
            contentParts.Add(new { type = "text", text = $"Ứng viên #{index} (nghi vấn: chữ ký):" });
            contentParts.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{crop}" } });
            index++;
        }

        contentParts.Add(new
        {
            type = "text",
            text = "Trả về DUY NHẤT 1 JSON object theo đúng định dạng đã yêu cầu, không thêm giải thích.",
        });

        const string systemPrompt = """
            Bạn là công cụ xác thực vùng con dấu/chữ ký trên tài liệu kỹ thuật ngành điện đã scan.
            Với mỗi ảnh ứng viên được đánh số thứ tự (đã cắt riêng từ trang gốc bằng xử lý ảnh, có thể lệch
            loại hoặc không phải con dấu/chữ ký gì cả so với nghi vấn ban đầu), phân loại đúng 1 trong 3 loại:
            - "Seal": con dấu mộc thật — hình tròn/vuông, có chữ/hoa văn khắc dấu. Mực có thể màu đỏ, màu
              xanh, HOẶC chỉ còn đen/xám đậm nếu tài liệu là bản photo đen trắng (không còn giữ màu mực gốc)
              — vẫn tính là "Seal" nếu hình dạng/hoa văn khắc dấu rõ ràng, không phụ thuộc màu mực.
            - "Signature": chữ ký tay thật — nét viết tay liên tục thể hiện 1 cái tên/chữ ký cá nhân, không
              phải chữ in OCR đọc rõ được, không phải hình vẽ/gạch chân/dấu tích khác.
            - "None": không phải con dấu cũng không phải chữ ký (chữ in, logo, tiêu đề, đoạn văn bản, bảng,
              đường kẻ, nhiễu...).
            Trả về DUY NHẤT 1 JSON object dạng
            {"results": [{"index": <số thứ tự ứng viên>, "type": "Seal"|"Signature"|"None", "confidence": <0.0-1.0>}]}.
            Không thêm giải thích, không thêm trường nào khác, không bỏ sót ứng viên nào.
            """;

        var payload = new
        {
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = contentParts },
            },
            temperature = 0, // phân loại đúng/sai, không phải sinh văn bản — cần nhất quán giữa các lần chạy, không cần đa dạng
            max_tokens = 4096,
            response_format = new { type = "json_object" },
        };

        var llmServerUrl = _configuration["AIModelServers:LlmServerUrl"] ?? "http://localhost:8080";
        var httpClient = _httpClientFactory.CreateClient("LlmClient");
        httpClient.BaseAddress = new Uri(llmServerUrl);

        var requestContent = new StringContent(
            JsonSerializer.Serialize(payload, OcrPageContentHelper.Utf8JsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await httpClient.PostAsync("/v1/chat/completions", requestContent);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();

        var envelope = JsonNode.Parse(responseBody);
        var rawContent = envelope?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        var cleaned = OcrPageContentHelper.StripMarkdownCodeFence(rawContent);

        var verdictByIndex = new Dictionary<int, (string Type, double Confidence)>();
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            try
            {
                var parsed = JsonNode.Parse(cleaned);
                var resultsArray = parsed?["results"]?.AsArray();
                if (resultsArray != null)
                {
                    foreach (var item in resultsArray)
                    {
                        var idx = item?["index"]?.GetValue<int>();
                        var type = item?["type"]?.GetValue<string>();
                        var confidence = item?["confidence"]?.GetValue<double>() ?? 0.7;
                        if (idx.HasValue && !string.IsNullOrWhiteSpace(type))
                        {
                            verdictByIndex[idx.Value] = (type, Math.Clamp(confidence, 0.0, 1.0));
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Bỏ qua nếu LLM trả về JSON không hợp lệ — không xác thực được ứng viên nào ở lần chạy này,
                // không phải lỗi hệ thống (giống hành xử của OcrModuleSpellcheckService).
            }
        }

        var sealVerdicts = new List<(InkRegionCandidate Candidate, double Score)>();
        var signatureVerdicts = new List<(InkRegionCandidate Candidate, double Score)>();

        index = 1;
        foreach (var seal in sealSlots)
        {
            if (verdictByIndex.TryGetValue(index, out var verdict) && verdict.Type == "Seal")
            {
                sealVerdicts.Add((seal, verdict.Confidence));
            }

            index++;
        }

        foreach (var signature in signatureSlots)
        {
            if (verdictByIndex.TryGetValue(index, out var verdict) && verdict.Type == "Signature")
            {
                signatureVerdicts.Add((signature, verdict.Confidence));
            }

            index++;
        }

        return new VlmVerdicts(sealVerdicts, signatureVerdicts);
    }
}
