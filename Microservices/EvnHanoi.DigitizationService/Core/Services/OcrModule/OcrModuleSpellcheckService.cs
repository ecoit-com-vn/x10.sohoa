using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EvnHanoi.DigitizationService.Helpers;
using EvnHanoi.DigitizationService.Models.OcrModule;
using EvnHanoi.DigitizationService.Repositories.OcrModule;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.DigitizationService.Core.Services.OcrModule;

public class SpellcheckRunResult
{
    public int TotalRegionsChecked { get; set; }
    public int SuggestionCount { get; set; }
}

/// <summary>
/// Yêu cầu 95 — kiểm tra chính tả bằng cách gọi lại đúng LLM server hiện có (AIModelServers:LlmServerUrl,
/// HttpClient "LlmClient") nhưng với 1 prompt riêng, gọn, hoàn toàn TÁCH BIỆT khỏi prompt trích xuất lớn
/// trong ExtractionWorker.cs — không đụng file đó.
/// </summary>
public interface IOcrModuleSpellcheckService
{
    Task<SpellcheckRunResult> RunAsync(string jobId);
}

public class OcrModuleSpellcheckService : IOcrModuleSpellcheckService
{
    private const int MaxRegionsPerCall = 150;

    private readonly IOcrModuleRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public OcrModuleSpellcheckService(
        IOcrModuleRepository repository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<SpellcheckRunResult> RunAsync(string jobId)
    {
        var allRegions = await _repository.GetAllRegionsAsync(jobId);
        var textRegions = allRegions
            .Where(r => r.RegionType == "Text" && !string.IsNullOrWhiteSpace(r.TextRaw))
            .Take(MaxRegionsPerCall)
            .ToList();

        if (textRegions.Count == 0)
        {
            return new SpellcheckRunResult { TotalRegionsChecked = 0, SuggestionCount = 0 };
        }

        var llmServerUrl = _configuration["AIModelServers:LlmServerUrl"] ?? "http://localhost:8080";
        var httpClient = _httpClientFactory.CreateClient("LlmClient");
        httpClient.BaseAddress = new Uri(llmServerUrl);
        httpClient.Timeout = TimeSpan.FromMinutes(2);

        const string systemPrompt = """
            Bạn là công cụ kiểm tra chính tả tiếng Việt cho văn bản kỹ thuật ngành điện đã qua OCR.
            Với mỗi đoạn văn bản được đánh số ID dưới đây, nếu phát hiện lỗi chính tả tiếng Việt rõ ràng
            (nhầm dấu thanh, nhầm ký tự hình dạng giống nhau, dính/tách từ sai do OCR), đề xuất bản đã sửa.
            TUYỆT ĐỐI KHÔNG sửa mã thiết bị, mã trạm, số hiệu văn bản, hoặc bất kỳ chuỗi định danh kỹ thuật
            xen kẽ chữ-số nào — giữ nguyên 100% các mã này.
            Nếu một đoạn không có lỗi chính tả, KHÔNG đưa đoạn đó vào kết quả (bỏ qua hoàn toàn).
            Trả về DUY NHẤT 1 JSON object dạng {"suggestions": [{"id": "...", "suggestion": "..."}]}.
            Không thêm giải thích, không thêm trường nào khác.
            """;

        var userPromptBuilder = new StringBuilder("[DANH SÁCH ĐOẠN VĂN BẢN]:\n");
        foreach (var region in textRegions)
        {
            userPromptBuilder.Append($"[{region.Id}] {OcrPageContentHelper.NormalizeUtf8Text(region.TextRaw)}\n");
        }

        var payload = new
        {
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPromptBuilder.ToString() },
            },
            temperature = 0.1,
            max_tokens = 4096,
            response_format = new { type = "json_object" },
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload, OcrPageContentHelper.Utf8JsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await httpClient.PostAsync("/v1/chat/completions", content);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();

        var envelope = JsonNode.Parse(responseBody);
        var rawContent = envelope?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        var cleaned = OcrPageContentHelper.StripMarkdownCodeFence(rawContent);

        var suggestions = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            try
            {
                var parsed = JsonNode.Parse(cleaned);
                var suggestionArray = parsed?["suggestions"]?.AsArray();
                if (suggestionArray != null)
                {
                    foreach (var item in suggestionArray)
                    {
                        var id = item?["id"]?.GetValue<string>();
                        var suggestion = item?["suggestion"]?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(suggestion))
                        {
                            suggestions[id] = suggestion;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Bỏ qua nếu LLM trả về JSON không hợp lệ — không có gợi ý nào được lưu, không lỗi hệ thống.
            }
        }

        await _repository.UpdateRegionSpellcheckSuggestionsAsync(suggestions);

        return new SpellcheckRunResult
        {
            TotalRegionsChecked = textRegions.Count,
            SuggestionCount = suggestions.Count,
        };
    }
}
