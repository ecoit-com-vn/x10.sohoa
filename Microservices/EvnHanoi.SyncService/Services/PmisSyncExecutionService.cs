using System.Text.Json;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Internal;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Repositories;

namespace EvnHanoi.SyncService.Services;

public class PmisSyncExecutionService : IPmisSyncExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly ISyncHistoryRepository _syncHistoryRepository;

    public PmisSyncExecutionService(IEquipmentServiceClient equipmentServiceClient, ISyncHistoryRepository syncHistoryRepository)
    {
        _equipmentServiceClient = equipmentServiceClient;
        _syncHistoryRepository = syncHistoryRepository;
    }

    public async Task<(int Success, int Failed, List<string> Errors)> SyncInfrastructureAsync(
        int infraTypeId, string syncHistoryId, IReadOnlyList<JsonElement> rawItems)
    {
        var upsertRequests = new List<UpsertInfrastructureFromPmisRequest>();
        foreach (var raw in rawItems)
        {
            if (infraTypeId == 1)
            {
                var item = raw.Deserialize<PmisSubstationDto>(JsonOptions)!;
                upsertRequests.Add(new UpsertInfrastructureFromPmisRequest
                {
                    InfraTypeId = 1,
                    PmisCode = item.MaTBA,
                    Code = item.MaTBA,
                    Name = item.TenTBA,
                    Address = item.DiaDiem,
                    UnitCode = item.MaDonVi,
                    OperationDate = item.NgayVanHanh
                });
            }
            else
            {
                var item = raw.Deserialize<PmisLineDto>(JsonOptions)!;
                upsertRequests.Add(new UpsertInfrastructureFromPmisRequest
                {
                    InfraTypeId = 2,
                    PmisCode = item.MaDuongDay,
                    Code = item.MaDuongDay,
                    Name = item.TenDuongDay,
                    UnitCode = item.MaDonVi,
                    OperationDate = item.NgayVanHanh
                });
            }
        }

        if (upsertRequests.Count == 0) return (0, 0, []);

        var results = await _equipmentServiceClient.UpsertInfrastructureAsync(upsertRequests);

        var details = new List<SyncHistoryDetail>();
        var errors = new List<string>();
        var successCount = 0;
        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            if (result.Success) successCount++;
            else errors.Add($"{result.PmisCode}: {result.ErrorMessage}");

            details.Add(new SyncHistoryDetail
            {
                SyncHistoryId = syncHistoryId,
                SourceId = result.PmisCode,
                SourceCode = result.PmisCode,
                SourceName = upsertRequests[i].Name,
                TargetId = result.InfrastructureId?.ToString(),
                ActionType = result.WasCreated ? SyncActionType.Create : SyncActionType.Update,
                Status = result.Success ? SyncDetailStatus.Success : SyncDetailStatus.Failed,
                DataContent = rawItems[i].GetRawText(),
                ErrorMessage = result.ErrorMessage
            });
        }

        await _syncHistoryRepository.InsertDetailsAsync(details);
        return (successCount, results.Count - successCount, errors);
    }

    public async Task<(int Success, int Failed, List<string> Errors)> SyncEquipmentAsync(
        string syncHistoryId, IReadOnlyList<JsonElement> rawItems)
    {
        var upsertRequests = new List<UpsertEquipmentFromPmisRequest>();
        foreach (var raw in rawItems)
        {
            var item = raw.Deserialize<EquipmentSaveShape>(JsonOptions)!;
            upsertRequests.Add(new UpsertEquipmentFromPmisRequest
            {
                PmisCode = item.MaTB,
                Code = item.MaTB,
                Name = item.TenTB,
                EquipmentTypeCode = item.MaLoaiTB ?? string.Empty,
                ParentPmisCode = item.MaTBA ?? item.MaDuongDay,
                UnitCode = item.MaDonVi,
                ManufactureYear = item.NamSanXuat,
                QrCodeBase64 = item.MaQRCode,
                ThongSoKyThuat = item.ThongSoKyThuat
            });
        }

        if (upsertRequests.Count == 0) return (0, 0, []);

        var results = await _equipmentServiceClient.UpsertEquipmentAsync(upsertRequests);

        var details = new List<SyncHistoryDetail>();
        var errors = new List<string>();
        var successCount = 0;
        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            if (result.Success) successCount++;
            else errors.Add($"{result.PmisCode}: {result.ErrorMessage}");

            details.Add(new SyncHistoryDetail
            {
                SyncHistoryId = syncHistoryId,
                SourceId = result.PmisCode,
                SourceCode = result.PmisCode,
                SourceName = upsertRequests[i].Name,
                TargetId = result.EquipmentId?.ToString(),
                ActionType = result.WasCreated ? SyncActionType.Create : SyncActionType.Update,
                Status = result.Success ? SyncDetailStatus.Success : SyncDetailStatus.Failed,
                DataContent = rawItems[i].GetRawText(),
                ErrorMessage = result.ErrorMessage
            });
        }

        await _syncHistoryRepository.InsertDetailsAsync(details);
        return (successCount, results.Count - successCount, errors);
    }

    private class EquipmentSaveShape
    {
        public string MaTB { get; set; } = string.Empty;
        public string TenTB { get; set; } = string.Empty;
        public string? MaLoaiTB { get; set; }
        public string? MaTBA { get; set; }
        public string? MaDuongDay { get; set; }
        public string? MaDonVi { get; set; }
        public int? NamSanXuat { get; set; }
        public string? MaQRCode { get; set; }
        public string? ThongSoKyThuat { get; set; }
    }
}
