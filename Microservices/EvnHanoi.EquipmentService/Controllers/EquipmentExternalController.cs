using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/equipment")]
public class EquipmentExternalController : ControllerBase
{
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IExternalApiKeyValidator _externalApiKeyValidator;
    private readonly IExternalApiCallLogRepository _externalApiCallLogRepository;
    private readonly ISystemParamRepository _systemParamRepository;
    private readonly ILogger<EquipmentExternalController> _logger;
    private const string keyName = "PMIS-BBXX";
    private const string keyNameLLTB = "PMIS-LLTB";
    private const string keyNameCBM = "PMIS-CBM";
    private const string keyNameTSVH = "PMIS-TSVH";
    private const string keyNameTSKT = "PMIS-TSKT";
    public EquipmentExternalController(
        IEquipmentRepository equipmentRepository,
        IDocumentRepository documentRepository,
        IExternalApiKeyValidator externalApiKeyValidator,
        IExternalApiCallLogRepository externalApiCallLogRepository,
        ISystemParamRepository systemParamRepository,
        ILogger<EquipmentExternalController> logger)
    {
        _equipmentRepository = equipmentRepository;
        _documentRepository = documentRepository;
        _externalApiKeyValidator = externalApiKeyValidator;
        _externalApiCallLogRepository = externalApiCallLogRepository;
        _systemParamRepository = systemParamRepository;
        _logger = logger;
    }

    [HttpGet("external/factory-acceptance")]
    public async Task<IActionResult> GetFactoryAcceptanceEquipmentList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 1000,
        [FromQuery] string? keyword = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 1000);

        var filter = new DossierDocumentFilterDto
        {
            Page = page,
            PageSize = pageSize,
            Keyword = keyword
        };
        var (items, totalCount) = await _documentRepository.GetPublishedFactoryAcceptanceDocumentsAsync(filter);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("pmis-getlist")]
    public async Task<IActionResult> GetPmisListEquipment(
     //   [FromHeader(Name = "X-Pmis-Key-Name")] string? keyName,
        [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
        [FromQuery] PmisEquipmentListRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        long? apiKeyId = null;
        int statusCode = StatusCodes.Status200OK;
        string? errorMessage = null;
        string? responseSummary = null;

        try
        {
            if (string.IsNullOrWhiteSpace(keyName) || string.IsNullOrWhiteSpace(privateKey))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            apiKeyId = await _externalApiKeyValidator.ValidateAsync(keyName.Trim(), ComputeSha256(privateKey));
            if (apiKeyId is null)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            if (request.Loai is not null and not 1 and not 2)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Loại chỉ nhận giá trị 1 (trạm biến áp) hoặc 2 (đường dây).";
                return BadRequest(new { message = errorMessage });
            }

            if (request.Skip < 0 || request.Take is < 1 or > 1000)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "skip phải từ 0 trở lên và take phải từ 1 đến 1000.";
                return BadRequest(new { message = errorMessage });
            }

            if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Từ ngày không được lớn hơn đến ngày.";
                return BadRequest(new { message = errorMessage });
            }

            var pmisUrl = await _systemParamRepository.GetValueAsync("PmisUrl");
            if (string.IsNullOrWhiteSpace(pmisUrl))
            {
                statusCode = StatusCodes.Status500InternalServerError;
                errorMessage = "Chưa cấu hình tham số hệ thống PmisUrl.";
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage });
            }

            var (items, totalCount) = await _equipmentRepository.GetExternalListAsync(request);
            pmisUrl = pmisUrl + "/#/equipment/equipment-external?equipmentId=";
            foreach (var item in items)
            {
                item.Link = $"{pmisUrl}{Uri.EscapeDataString(item.Id.ToString())}";
               // item.MaQRCode = CreateQrCodeBase64(item.Link);
            }

            responseSummary = $"totalCount={totalCount}";
            return Ok(new { items, totalCount, request.Skip, request.Take });
        }
        catch (Exception ex)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            await LogApiCallAsync("pmis-getlist", keyName, apiKeyId, statusCode, errorMessage, responseSummary, stopwatch.ElapsedMilliseconds);
        }
    }
    [HttpGet("pmis-getlist-factory")]
    public async Task<IActionResult> GetPmisListEquipmentFactory(
       //   [FromHeader(Name = "X-Pmis-Key-Name")] string? keyName,
       [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
       [FromQuery] PmisEquipmentListRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        long? apiKeyId = null;
        int statusCode = StatusCodes.Status200OK;
        string? errorMessage = null;
        string? responseSummary = null;

        try
        {
            if (string.IsNullOrWhiteSpace(keyNameLLTB) || string.IsNullOrWhiteSpace(privateKey))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            apiKeyId = await _externalApiKeyValidator.ValidateAsync(keyNameLLTB.Trim(), ComputeSha256(privateKey));
            if (apiKeyId is null)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            if (request.Loai is not null and not 1 and not 2)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Loại chỉ nhận giá trị 1 (trạm biến áp) hoặc 2 (đường dây).";
                return BadRequest(new { message = errorMessage });
            }

            if (request.Skip < 0 || request.Take is < 1 or > 1000)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "skip phải từ 0 trở lên và take phải từ 1 đến 1000.";
                return BadRequest(new { message = errorMessage });
            }

            if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Từ ngày không được lớn hơn đến ngày.";
                return BadRequest(new { message = errorMessage });
            }

            var pmisUrl = await _systemParamRepository.GetValueAsync("PmisUrl");
            if (string.IsNullOrWhiteSpace(pmisUrl))
            {
                statusCode = StatusCodes.Status500InternalServerError;
                errorMessage = "Chưa cấu hình tham số hệ thống PmisUrl.";
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage });
            }
            pmisUrl = pmisUrl + "/#/equipment/equipment-factory?equipmentId=";
            var (items, totalCount) = await _equipmentRepository.GetExternalListAsync(request);

            foreach (var item in items)
            {
                item.Link = $"{pmisUrl}{Uri.EscapeDataString(item.Id.ToString())}";
                // item.MaQRCode = CreateQrCodeBase64(item.Link);
            }

            responseSummary = $"totalCount={totalCount}";
            return Ok(new { items, totalCount, request.Skip, request.Take });
        }
        catch (Exception ex)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            await LogApiCallAsync("pmis-getlist-factory", keyNameLLTB, apiKeyId, statusCode, errorMessage, responseSummary, stopwatch.ElapsedMilliseconds);
        }
    }
    [HttpGet("pmis-getlist-cbm")]
    public async Task<IActionResult> GetPmisListEquipmentCBM(
     //   [FromHeader(Name = "X-Pmis-Key-Name")] string? keyName,
     [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
     [FromQuery] PmisEquipmentListRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        long? apiKeyId = null;
        int statusCode = StatusCodes.Status200OK;
        string? errorMessage = null;
        string? responseSummary = null;

        try
        {
            if (string.IsNullOrWhiteSpace(keyNameCBM) || string.IsNullOrWhiteSpace(privateKey))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            apiKeyId = await _externalApiKeyValidator.ValidateAsync(keyNameCBM.Trim(), ComputeSha256(privateKey));
            if (apiKeyId is null)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            if (request.Loai is not null and not 1 and not 2)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Loại chỉ nhận giá trị 1 (trạm biến áp) hoặc 2 (đường dây).";
                return BadRequest(new { message = errorMessage });
            }

            if (request.Skip < 0 || request.Take is < 1 or > 1000)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "skip phải từ 0 trở lên và take phải từ 1 đến 1000.";
                return BadRequest(new { message = errorMessage });
            }

            if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Từ ngày không được lớn hơn đến ngày.";
                return BadRequest(new { message = errorMessage });
            }

            var pmisUrl = await _systemParamRepository.GetValueAsync("PmisUrl");
            if (string.IsNullOrWhiteSpace(pmisUrl))
            {
                statusCode = StatusCodes.Status500InternalServerError;
                errorMessage = "Chưa cấu hình tham số hệ thống PmisUrl.";
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage });
            }
            pmisUrl = pmisUrl + "/#/equipment/equipment-cbm?equipmentId=";
            var (items, totalCount) = await _equipmentRepository.GetExternalListAsync(request);

            foreach (var item in items)
            {
                item.Link = $"{pmisUrl}{Uri.EscapeDataString(item.Id.ToString())}";
                // item.MaQRCode = CreateQrCodeBase64(item.Link);
            }

            responseSummary = $"totalCount={totalCount}";
            return Ok(new { items, totalCount, request.Skip, request.Take });
        }
        catch (Exception ex)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            await LogApiCallAsync("pmis-getlist-cbm", keyNameCBM, apiKeyId, statusCode, errorMessage, responseSummary, stopwatch.ElapsedMilliseconds);
        }
    }
    [HttpGet("pmis-getlist-tsvh")]
    public async Task<IActionResult> GetPmisListEquipmentTSVH(
   //   [FromHeader(Name = "X-Pmis-Key-Name")] string? keyName,
   [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
   [FromQuery] PmisEquipmentListRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        long? apiKeyId = null;
        int statusCode = StatusCodes.Status200OK;
        string? errorMessage = null;
        string? responseSummary = null;

        try
        {
            if (string.IsNullOrWhiteSpace(keyNameTSVH) || string.IsNullOrWhiteSpace(privateKey))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            apiKeyId = await _externalApiKeyValidator.ValidateAsync(keyNameTSVH.Trim(), ComputeSha256(privateKey));
            if (apiKeyId is null)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            if (request.Loai is not null and not 1 and not 2)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Loại chỉ nhận giá trị 1 (trạm biến áp) hoặc 2 (đường dây).";
                return BadRequest(new { message = errorMessage });
            }

            if (request.Skip < 0 || request.Take is < 1 or > 1000)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "skip phải từ 0 trở lên và take phải từ 1 đến 1000.";
                return BadRequest(new { message = errorMessage });
            }

            if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Từ ngày không được lớn hơn đến ngày.";
                return BadRequest(new { message = errorMessage });
            }

            var pmisUrl = await _systemParamRepository.GetValueAsync("PmisUrl");
            if (string.IsNullOrWhiteSpace(pmisUrl))
            {
                statusCode = StatusCodes.Status500InternalServerError;
                errorMessage = "Chưa cấu hình tham số hệ thống PmisUrl.";
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage });
            }
            pmisUrl = pmisUrl + "/#/equipment/equipment-detail?equipmentId=";
            var (items, totalCount) = await _equipmentRepository.GetExternalListAsync(request);

            foreach (var item in items)
            {
                item.Link = $"{pmisUrl}{Uri.EscapeDataString(item.Id.ToString())}";
                // item.MaQRCode = CreateQrCodeBase64(item.Link);
            }

            responseSummary = $"totalCount={totalCount}";
            return Ok(new { items, totalCount, request.Skip, request.Take });
        }
        catch (Exception ex)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            await LogApiCallAsync("pmis-getlist-tsvh", keyNameTSVH, apiKeyId, statusCode, errorMessage, responseSummary, stopwatch.ElapsedMilliseconds);
        }
    }
    [HttpGet("pmis-getlist-tskt")]
    public async Task<IActionResult> GetPmisListEquipmentTSKT(
       [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
       [FromQuery] PmisEquipmentListRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        long? apiKeyId = null;
        int statusCode = StatusCodes.Status200OK;
        string? errorMessage = null;
        string? responseSummary = null;

        try
        {
            if (string.IsNullOrWhiteSpace(keyNameTSKT) || string.IsNullOrWhiteSpace(privateKey))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            apiKeyId = await _externalApiKeyValidator.ValidateAsync(keyNameTSKT.Trim(), ComputeSha256(privateKey));
            if (apiKeyId is null)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            //if (request.Loai is not null and not 1 and not 2)
            //{
            //    statusCode = StatusCodes.Status400BadRequest;
            //    errorMessage = "Loại chỉ nhận giá trị 1 (trạm biến áp) hoặc 2 (đường dây).";
            //    return BadRequest(new { message = errorMessage });
            //}

            //if (request.Skip < 0 || request.Take is < 1 or > 1000)
            //{
            //    statusCode = StatusCodes.Status400BadRequest;
            //    errorMessage = "skip phải từ 0 trở lên và take phải từ 1 đến 1000.";
            //    return BadRequest(new { message = errorMessage });
            //}

            //if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            //{
            //    statusCode = StatusCodes.Status400BadRequest;
            //    errorMessage = "Từ ngày không được lớn hơn đến ngày.";
            //    return BadRequest(new { message = errorMessage });
            //}

            var (data, totalCount) = await _equipmentRepository.GetExternalListWithItemsAsync(request);

            foreach (var item in data)
            {
                item.Items = BuildTechnicalParameters(item.FormSchema, item.FormValues);
            }

            responseSummary = $"totalCount={totalCount}";
            return Ok(new { data, totalCount, request.Skip, request.Take });
        }
        catch (Exception ex)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            await LogApiCallAsync("pmis-getlist-tskt", keyNameTSKT, apiKeyId, statusCode, errorMessage, responseSummary, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task LogApiCallAsync(
        string endpoint,
        string requestedKeyName,
        long? apiKeyId,
        int statusCode,
        string? errorMessage,
        string? responseSummary,
        long durationMs)
    {
        try
        {
            await _externalApiCallLogRepository.LogAsync(new ExternalApiCallLogEntry
            {
                ApiKeyId = apiKeyId,
                KeyName = requestedKeyName,
                Endpoint = $"api/v1/equipment/{endpoint}",
                HttpMethod = Request.Method,
                RequestQuery = Request.QueryString.HasValue ? Request.QueryString.Value : null,
                RequestIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                StatusCode = statusCode,
                IsSuccess = statusCode is >= 200 and < 300,
                DurationMs = durationMs,
                ResponseSummary = responseSummary,
                ErrorMessage = errorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write external API call log for endpoint {Endpoint}.", endpoint);
        }
    }

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>
    /// Dựng danh sách thông số kỹ thuật động từ FormSchema và FormValues. Key đầu ra được sinh từ
    /// label theo camelCase không dấu; value giữ nguyên kiểu JSON đã lưu.
    /// </summary>
    private static Dictionary<string, JsonElement> BuildTechnicalParameters(
        string? formSchemaJson,
        string? formValuesJson)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(formSchemaJson) || string.IsNullOrWhiteSpace(formValuesJson))
            return result;

        try
        {
            using var schemaDocument = JsonDocument.Parse(formSchemaJson);
            using var valuesDocument = JsonDocument.Parse(formValuesJson);

            if (valuesDocument.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            var schemaRoot = schemaDocument.RootElement;
            IEnumerable<JsonElement> fields = schemaRoot.ValueKind switch
            {
                JsonValueKind.Array => schemaRoot.EnumerateArray(),
                JsonValueKind.Object when TryGetPropertyIgnoreCase(schemaRoot, "fields", out var fieldsElement)
                                          && fieldsElement.ValueKind == JsonValueKind.Array
                    => fieldsElement.EnumerateArray(),
                _ => Array.Empty<JsonElement>()
            };

            foreach (var field in fields)
            {
                if (TryGetPropertyIgnoreCase(field, "active", out var activeElement)
                    && activeElement.ValueKind == JsonValueKind.False)
                {
                    continue;
                }

                var lookupKey = ResolveSchemaFieldName(field);
                var label = ReadSchemaString(field, "label", "Label");
                if (string.IsNullOrWhiteSpace(lookupKey) || string.IsNullOrWhiteSpace(label))
                    continue;

                var hasValue = TryGetPropertyIgnoreCase(
                    valuesDocument.RootElement,
                    lookupKey,
                    out var valueElement)
                    && valueElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

                var outputKey = NormalizeLabelToJsonKey(label);
                if (string.IsNullOrWhiteSpace(outputKey))
                    continue;

                outputKey = ResolveUniqueOutputKey(result, outputKey, field, lookupKey);
                result[outputKey] = hasValue
                    ? valueElement.Clone()
                    : JsonSerializer.SerializeToElement(string.Empty);
            }
        }
        catch (JsonException)
        {
            // Schema hoặc values không hợp lệ: giữ response chung và trả items rỗng cho bản ghi này.
        }

        return result;
    }

    /// <summary>Chuyển label tiếng Việt thành JSON key camelCase, không dấu và không ký tự đặc biệt.</summary>
    private static string NormalizeLabelToJsonKey(string label)
    {
        var decomposed = label
            .Replace('Đ', 'D')
            .Replace('đ', 'd')
            .Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                withoutDiacritics.Append(character);
        }

        var tokens = Regex.Matches(withoutDiacritics.ToString(), @"[A-Za-z0-9]+")
            .Select(match => match.Value)
            .Where(token => token.Length > 0)
            .ToArray();
        if (tokens.Length == 0)
            return string.Empty;

        var key = tokens[0].ToLowerInvariant()
            + string.Concat(tokens.Skip(1).Select(ToPascalCaseToken));

        return char.IsDigit(key[0])
            ? "field" + ToPascalCaseToken(key)
            : key;
    }

    private static string ToPascalCaseToken(string token)
    {
        var lower = token.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower[1..];
    }

    /// <summary>
    /// Label trùng sau chuẩn hóa được nối với id trường, nhờ đó key không phụ thuộc vào vị trí field trong schema.
    /// </summary>
    private static string ResolveUniqueOutputKey(
        IReadOnlyDictionary<string, JsonElement> result,
        string outputKey,
        JsonElement field,
        string lookupKey)
    {
        if (!result.ContainsKey(outputKey))
            return outputKey;

        var fieldIdentity = ReadSchemaString(field, "id", "Id") ?? lookupKey;
        var normalizedIdentity = NormalizeLabelToJsonKey(fieldIdentity);
        var candidate = outputKey + (string.IsNullOrWhiteSpace(normalizedIdentity)
            ? "Field"
            : ToPascalCaseToken(normalizedIdentity));

        var suffix = 2;
        var uniqueKey = candidate;
        while (result.ContainsKey(uniqueKey))
            uniqueKey = candidate + suffix++;

        return uniqueKey;
    }
    /// <summary>Lấy mã trường EAV — ưu tiên name/key có giá trị, fallback id (form builder hay để name rỗng).</summary>
    private static string? ResolveSchemaFieldName(System.Text.Json.JsonElement item)
    {
        foreach (var property in new[] { "name", "Name", "key", "Key", "id", "Id", "fieldName", "FieldName" })
        {
            if (!TryGetPropertyIgnoreCase(item, property, out var el))
                continue;

            var value = ReadJsonScalarAsString(el);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? ReadSchemaString(System.Text.Json.JsonElement item, params string[] propertyNames)
    {
        foreach (var property in propertyNames)
        {
            if (!TryGetPropertyIgnoreCase(item, property, out var el))
                continue;

            var value = ReadJsonScalarAsString(el);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(System.Text.Json.JsonElement element, string propertyName, out System.Text.Json.JsonElement value)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
            return true;

        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? ReadJsonScalarAsString(System.Text.Json.JsonElement el) =>
        el.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => el.GetString(),
            System.Text.Json.JsonValueKind.Number => el.GetRawText(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            _ => null
        };

    //private static string CreateQrCodeBase64(string content)
    //{
    //    using var generator = new QRCodeGenerator();
    //    using var qrCodeData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
    //    var pngBytes = new PngByteQRCode(qrCodeData).GetGraphic(12);
    //    return Convert.ToBase64String(pngBytes);
    //}
}
