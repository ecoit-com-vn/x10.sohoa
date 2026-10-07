using System.Text.Json;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Internal;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Schedulers;
using EvnHanoi.SyncService.Services;
using Moq;
using Xunit;

namespace EvnHanoi.SyncService.Tests;

public class PmisRecordHasherTests
{
    private static JsonElement Json(object o) => JsonSerializer.SerializeToElement(o);

    [Fact]
    public void SameContent_SameHash()
    {
        var a = Json(new PmisSubstationDto { MaTBA = "T1", TenTBA = "Tram 1" });
        var b = Json(new PmisSubstationDto { MaTBA = "T1", TenTBA = "Tram 1" });
        Assert.Equal(PmisRecordHasher.Compute(a), PmisRecordHasher.Compute(b));
        Assert.Equal(64, PmisRecordHasher.Compute(a).Length);
    }

    [Fact]
    public void DifferentContent_DifferentHash()
    {
        var a = Json(new PmisSubstationDto { MaTBA = "T1", TenTBA = "Tram 1" });
        var b = Json(new PmisSubstationDto { MaTBA = "T1", TenTBA = "Tram 1 (doi ten)" });
        Assert.NotEqual(PmisRecordHasher.Compute(a), PmisRecordHasher.Compute(b));
    }

    [Fact]
    public void InfrastructureCode_TrimsAndPicksFieldByType()
    {
        Assert.Equal("T1", PmisRecordHasher.InfrastructureCode(Json(new PmisSubstationDto { MaTBA = " T1 " }), 1));
        Assert.Equal("D1", PmisRecordHasher.InfrastructureCode(Json(new PmisLineDto { MaDuongDay = "D1 " }), 2));
    }

    [Fact]
    public void EquipmentCode_PrefersMaThietBiThenMaTB()
    {
        Assert.Equal("TB-TBA", PmisRecordHasher.EquipmentCode(Json(new { MaThietBi = " TB-TBA ", MaTB = "" })));
        Assert.Equal("TB-DZ", PmisRecordHasher.EquipmentCode(Json(new PmisLineDeviceDto { MaTB = "TB-DZ" })));
        Assert.Equal(string.Empty, PmisRecordHasher.EquipmentCode(Json(new { Foo = 1 })));
    }
}

public class IncrementalContextTests
{
    private static readonly DateTime Sweep = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    private static IncrementalContext Ctx(DateTime? sweepStart = null, int version = 1, bool detail = true, DateTime? lastPushed = null) => new()
    {
        Existing = new Dictionary<string, PmisSyncStateRow>(StringComparer.OrdinalIgnoreCase)
        {
            ["ABC"] = new() { PmisCode = "ABC", ContentHash = "H1", HashVersion = 1, DetailSynced = detail, LastPushedAt = lastPushed }
        },
        SweepStartUtc = sweepStart,
        HashVersion = version
    };

    [Fact] public void SameHash_IsUnchanged() => Assert.True(Ctx().IsUnchanged("abc", "H1", requireDetail: false));
    [Fact] public void DifferentHash_IsChanged() => Assert.False(Ctx().IsUnchanged("ABC", "H2", requireDetail: false));
    [Fact] public void UnknownCode_IsChanged() => Assert.False(Ctx().IsUnchanged("XYZ", "H1", requireDetail: false));
    [Fact] public void NewHashVersion_IsChanged() => Assert.False(Ctx(version: 2).IsUnchanged("ABC", "H1", requireDetail: false));
    [Fact] public void RequireDetail_WithoutDetail_IsChanged() => Assert.False(Ctx(detail: false).IsUnchanged("ABC", "H1", requireDetail: true));
    [Fact] public void RequireDetail_WithDetail_IsUnchanged() => Assert.True(Ctx(detail: true).IsUnchanged("ABC", "H1", requireDetail: true));

    // Đợt quét đầy đủ: chỉ bỏ qua bản ghi đã được đẩy SAU mốc bắt đầu (để tiếp tục được khi lượt bị cắt giữa chừng).
    [Fact] public void Sweep_PushedBeforeStart_IsChanged() => Assert.False(Ctx(Sweep, lastPushed: Sweep.AddHours(-1)).IsUnchanged("ABC", "H1", false));
    [Fact] public void Sweep_NeverPushed_IsChanged() => Assert.False(Ctx(Sweep, lastPushed: null).IsUnchanged("ABC", "H1", false));
    [Fact] public void Sweep_PushedAfterStart_IsUnchanged() => Assert.True(Ctx(Sweep, lastPushed: Sweep.AddMinutes(5)).IsUnchanged("ABC", "H1", false));
}

public class OrderParentsForScanTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Rescan = TimeSpan.FromHours(24);

    private static Dictionary<string, PmisSyncStateRow> Rows(params PmisSyncStateRow[] rows) =>
        rows.ToDictionary(r => r.PmisCode, StringComparer.OrdinalIgnoreCase);

    private static SyncedInfrastructurePmisCode P(string code, int type = 1) => new SyncedInfrastructurePmisCode { PmisCode = code, InfraTypeId = type };

    [Fact]
    public void NeverScanned_First_ThenChangedParent_ThenDue_AndFreshIsDropped()
    {
        var all = new List<SyncedInfrastructurePmisCode> { P("FRESH"), P("DUE"), P("CHANGED"), P("NEVER") };
        var scan = Rows(
            new PmisSyncStateRow { PmisCode = "FRESH", LastScanAt = Now.AddHours(-1) },
            new PmisSyncStateRow { PmisCode = "DUE", LastScanAt = Now.AddHours(-30) },
            new PmisSyncStateRow { PmisCode = "CHANGED", LastScanAt = Now.AddHours(-2) });
        var substation = Rows(new PmisSyncStateRow { PmisCode = "CHANGED", LastPushedAt = Now.AddHours(-1) }); // đổi SAU lần quét

        var result = PmisScheduledSyncJob.OrderParentsForScan(all, scan, substation, Rows(), null, Rescan, Now)
            .Select(x => x.PmisCode).ToList();

        Assert.Equal(new[] { "NEVER", "CHANGED", "DUE" }, result);
    }

    [Fact]
    public void Sweep_IncludesFreshParentsNotScannedSinceSweepStart_OldestScanFirst()
    {
        var all = new List<SyncedInfrastructurePmisCode> { P("A"), P("B") };
        var scan = Rows(
            new PmisSyncStateRow { PmisCode = "A", LastScanAt = Now.AddHours(-1) },
            new PmisSyncStateRow { PmisCode = "B", LastScanAt = Now.AddHours(-5) });

        var result = PmisScheduledSyncJob.OrderParentsForScan(all, scan, Rows(), Rows(), Now.AddMinutes(-30), Rescan, Now)
            .Select(x => x.PmisCode).ToList();

        Assert.Equal(new[] { "B", "A" }, result);
    }

    [Fact]
    public void Sweep_ParentScannedAfterSweepStart_IsDropped()
    {
        var all = new List<SyncedInfrastructurePmisCode> { P("A"), P("B") };
        var scan = Rows(
            new PmisSyncStateRow { PmisCode = "A", LastScanAt = Now.AddMinutes(-10) },   // sau mốc → xong
            new PmisSyncStateRow { PmisCode = "B", LastScanAt = Now.AddHours(-5) });      // trước mốc → còn phải quét

        var result = PmisScheduledSyncJob.OrderParentsForScan(all, scan, Rows(), Rows(), Now.AddMinutes(-30), Rescan, Now)
            .Select(x => x.PmisCode).ToList();

        Assert.Equal(new[] { "B" }, result);
    }

    [Fact]
    public void LineParent_UsesLineState_NotSubstationState()
    {
        var all = new List<SyncedInfrastructurePmisCode> { P("L1", 2) };
        var scan = Rows(new PmisSyncStateRow { PmisCode = "L1", LastScanAt = Now.AddHours(-3) });
        var substationOnly = Rows(new PmisSyncStateRow { PmisCode = "L1", LastPushedAt = Now });   // sai loại → phải bị bỏ qua
        var line = Rows(new PmisSyncStateRow { PmisCode = "L1", LastPushedAt = Now.AddHours(-10) }); // trước lần quét

        Assert.Empty(PmisScheduledSyncJob.OrderParentsForScan(all, scan, substationOnly, line, null, Rescan, Now));
    }
}

public class ExecutorIncrementalTests
{
    private readonly Mock<IEquipmentServiceClient> _equipment = new();
    private readonly Mock<ISyncHistoryRepository> _history = new();
    private readonly Mock<IPmisClient> _pmis = new();
    private readonly Mock<IPmisEndpointConfigProvider> _endpoints = new();

    private PmisSyncExecutionService Sut() =>
        new(_equipment.Object, _history.Object, _pmis.Object, _endpoints.Object);

    private static JsonElement Json(object o) => JsonSerializer.SerializeToElement(o);

    private static IncrementalContext Ctx(params PmisSyncStateRow[] rows) => new()
    {
        Existing = rows.ToDictionary(r => r.PmisCode, StringComparer.OrdinalIgnoreCase),
        HashVersion = 1
    };

    // ---------- Trạm / Đường dây ----------

    [Fact]
    public async Task Infrastructure_UnchangedRecord_IsSkipped_AndNotSentToEquipmentService()
    {
        var item = Json(new PmisSubstationDto { MaTBA = "T1", TenTBA = "Tram 1" });
        var inc = Ctx(new PmisSyncStateRow { PmisCode = "T1", ContentHash = PmisRecordHasher.Compute(item), HashVersion = 1 });

        var (success, failed, _, _) = await Sut().SyncInfrastructureAsync(1, "h1", new[] { item }, syncDocuments: false, inc);

        Assert.Equal(0, success);   // job tự cộng UnchangedCodes vào Success
        Assert.Equal(0, failed);
        Assert.Equal(new[] { "T1" }, inc.UnchangedCodes);
        Assert.Empty(inc.ToSave);
        _equipment.Verify(e => e.UpsertInfrastructureAsync(It.IsAny<List<UpsertInfrastructureFromPmisRequest>>()), Times.Never);
    }

    [Fact]
    public async Task Infrastructure_ChangedAndNew_AreSent_AndSavedOnlyWhenSuccessful()
    {
        var changed = Json(new PmisSubstationDto { MaTBA = "T1", TenTBA = "Ten moi" });
        var fresh = Json(new PmisSubstationDto { MaTBA = "T2", TenTBA = "Moi" });
        var failing = Json(new PmisSubstationDto { MaTBA = "T3", TenTBA = "Loi" });
        var inc = Ctx(new PmisSyncStateRow { PmisCode = "T1", ContentHash = "OLD", HashVersion = 1 });

        _equipment.Setup(e => e.UpsertInfrastructureAsync(It.IsAny<List<UpsertInfrastructureFromPmisRequest>>()))
            .ReturnsAsync((List<UpsertInfrastructureFromPmisRequest> reqs) => reqs.Select(r => new UpsertInfrastructureFromPmisResult
            {
                PmisCode = r.PmisCode,
                Success = r.PmisCode != "T3",
                ErrorMessage = r.PmisCode == "T3" ? "boom" : null
            }).ToList());

        var (success, failed, _, _) = await Sut().SyncInfrastructureAsync(1, "h1", new[] { changed, fresh, failing }, syncDocuments: false, inc);

        Assert.Equal(2, success);
        Assert.Equal(1, failed);
        Assert.Equal(new[] { "T1", "T2" }, inc.ToSave.Select(x => x.PmisCode).OrderBy(x => x));
        Assert.All(inc.ToSave, x => Assert.Equal(PmisRecordHasher.Compute(x.PmisCode == "T1" ? changed : fresh), x.ContentHash));
        Assert.Empty(inc.UnchangedCodes);
    }

    [Fact]
    public async Task Line_ParentUnresolved_IsNotSaved_SoItRetriesNextRun()
    {
        var branch = Json(new PmisLineDto { MaDuongDay = "D-NHANH", TenDuongDay = "Nhanh" });
        var inc = Ctx();
        _equipment.Setup(e => e.UpsertInfrastructureAsync(It.IsAny<List<UpsertInfrastructureFromPmisRequest>>()))
            .ReturnsAsync((List<UpsertInfrastructureFromPmisRequest> reqs) => reqs.Select(r => new UpsertInfrastructureFromPmisResult
            {
                PmisCode = r.PmisCode, Success = true, ParentUnresolved = true
            }).ToList());

        await Sut().SyncInfrastructureAsync(2, "h1", new[] { branch }, syncDocuments: false, inc);

        Assert.Empty(inc.ToSave);
    }

    [Fact]
    public async Task Infrastructure_NullContext_KeepsOldBehaviour_SendsEverything()
    {
        var item = Json(new PmisSubstationDto { MaTBA = "T1", TenTBA = "Tram 1" });
        _equipment.Setup(e => e.UpsertInfrastructureAsync(It.IsAny<List<UpsertInfrastructureFromPmisRequest>>()))
            .ReturnsAsync((List<UpsertInfrastructureFromPmisRequest> reqs) =>
                reqs.Select(r => new UpsertInfrastructureFromPmisResult { PmisCode = r.PmisCode, Success = true }).ToList());

        var (success, _, _, _) = await Sut().SyncInfrastructureAsync(1, "h1", new[] { item }, syncDocuments: false);

        Assert.Equal(1, success);
        _equipment.Verify(e => e.UpsertInfrastructureAsync(It.Is<List<UpsertInfrastructureFromPmisRequest>>(l => l.Count == 1)), Times.Once);
    }

    // ---------- Thiết bị ----------

    [Fact]
    public async Task Equipment_UnchangedWithDetail_IsSkipped_WithoutAnyPmisCall()
    {
        var item = Json(new PmisLineDeviceDto { MaTB = "TB1", TenTB = "May 1", MaQRCode = "http://qr/1" });
        var inc = Ctx(new PmisSyncStateRow { PmisCode = "TB1", ContentHash = PmisRecordHasher.Compute(item), HashVersion = 1, DetailSynced = true });

        await Sut().SyncEquipmentAsync("h1", new[] { item }, "D1", inc);

        Assert.Equal(new[] { "TB1" }, inc.UnchangedCodes);
        _equipment.Verify(e => e.UpsertEquipmentAsync(It.IsAny<List<UpsertEquipmentFromPmisRequest>>()), Times.Never);
        _pmis.Verify(p => p.GetDeviceQrImageBytesAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Equipment_UnchangedButDetailMissing_IsPushedAgain()
    {
        var item = Json(new PmisLineDeviceDto { MaTB = "TB1", TenTB = "May 1", MaQRCode = "http://qr/1" });
        var inc = Ctx(new PmisSyncStateRow { PmisCode = "TB1", ContentHash = PmisRecordHasher.Compute(item), HashVersion = 1, DetailSynced = false });
        _pmis.Setup(p => p.GetDeviceQrImageBytesAsync("TB1")).ReturnsAsync(new byte[] { 1, 2, 3 });
        _equipment.Setup(e => e.UpsertEquipmentAsync(It.IsAny<List<UpsertEquipmentFromPmisRequest>>()))
            .ReturnsAsync((List<UpsertEquipmentFromPmisRequest> reqs) =>
                reqs.Select(r => new UpsertEquipmentFromPmisResult { PmisCode = r.PmisCode, Success = true }).ToList());
        _pmis.Setup(p => p.GetLineDocumentsAsync(It.IsAny<PmisLineDocumentSearchRequest>()))
            .ReturnsAsync(new PmisListResponse<PmisLineDocumentDto>());

        await Sut().SyncEquipmentAsync("h1", new[] { item }, "D1", inc);

        Assert.Empty(inc.UnchangedCodes);
        var saved = Assert.Single(inc.ToSave);
        Assert.Equal("TB1", saved.PmisCode);
        Assert.True(saved.DetailSynced);   // QR tải được → đủ chi tiết
    }

    [Fact]
    public async Task Equipment_QrDownloadFails_IsSavedWithDetailNotSynced()
    {
        var item = Json(new PmisLineDeviceDto { MaTB = "TB1", TenTB = "May 1", MaQRCode = "http://qr/1" });
        var inc = Ctx();
        _pmis.Setup(p => p.GetDeviceQrImageBytesAsync("TB1")).ThrowsAsync(new HttpRequestException("timeout"));
        _equipment.Setup(e => e.UpsertEquipmentAsync(It.IsAny<List<UpsertEquipmentFromPmisRequest>>()))
            .ReturnsAsync((List<UpsertEquipmentFromPmisRequest> reqs) =>
                reqs.Select(r => new UpsertEquipmentFromPmisResult { PmisCode = r.PmisCode, Success = true }).ToList());
        _pmis.Setup(p => p.GetLineDocumentsAsync(It.IsAny<PmisLineDocumentSearchRequest>()))
            .ReturnsAsync(new PmisListResponse<PmisLineDocumentDto>());

        await Sut().SyncEquipmentAsync("h1", new[] { item }, "D1", inc);

        var saved = Assert.Single(inc.ToSave);
        Assert.False(saved.DetailSynced);  // lượt sau (hoặc lượt quét đầy đủ) sẽ lấy lại QR
    }
}

public class SyncRunBudgetTests
{
    private static readonly SyncScheduleOptions Opt = new() { MinFrequencyMinutes = 120, RunBudgetBufferMinutes = 10 };

    [Theory]
    [InlineData(2, "HOUR", 110)]
    [InlineData(3, "HOUR", 170)]
    [InlineData(1, "DAY", 1430)]
    public void Budget_IsFrequencyMinusBuffer(int value, string unit, int expectedMinutes) =>
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), SyncRunBudget.For(value, unit, Opt));

    [Fact]
    public void Frequency_BelowMinimum_IsRaisedToMinimum()
    {
        Assert.Equal(TimeSpan.FromHours(2), SyncRunBudget.EffectiveFrequency(30, "MINUTE", Opt));
        Assert.Equal(TimeSpan.FromHours(2), SyncRunBudget.EffectiveFrequency(1, "HOUR", Opt));
        Assert.True(SyncRunBudget.IsBelowMinimum(119, "MINUTE", Opt));
        Assert.False(SyncRunBudget.IsBelowMinimum(2, "HOUR", Opt));
        Assert.Equal(TimeSpan.FromMinutes(110), SyncRunBudget.For(30, "MINUTE", Opt));
    }

    [Fact]
    public void Budget_WithSmallFrequency_NeverZeroOrNegative()
    {
        var low = new SyncScheduleOptions { MinFrequencyMinutes = 1, RunBudgetBufferMinutes = 10 };
        Assert.Equal(TimeSpan.FromMinutes(5), SyncRunBudget.For(10, "MINUTE", low));   // 10-10=0 -> sàn max(5, 5)
        Assert.Equal(TimeSpan.FromMinutes(7.5), SyncRunBudget.For(15, "MINUTE", low)); // 15-10=5 < 7,5 -> 7,5
        Assert.Equal(TimeSpan.FromMinutes(5), SyncRunBudget.For(5, "MINUTE", low));    // sàn 5, không vượt tần suất
    }

    [Theory]
    [InlineData("AUTO", 2, "HOUR", 125)]     // ngân sách 110 + 15
    [InlineData("AUTO", 30, "MINUTE", 125)]  // nâng lên 2h trước khi tính
    [InlineData("MANUAL", 2, "HOUR", 60)]
    [InlineData("AUTO", null, null, 60)]
    public void StaleAfter_IsBudgetPlusGrace(string syncType, int? value, string? unit, int expectedMinutes) =>
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), SyncRunBudget.StaleAfter(syncType, value, unit, Opt));
}
