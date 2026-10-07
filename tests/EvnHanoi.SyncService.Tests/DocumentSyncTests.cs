using EvnHanoi.SyncService.Services;
using Xunit;

namespace EvnHanoi.SyncService.Tests;

public class DocumentFileErrorClassifierTests
{
    [Theory]
    [InlineData(404, DocumentFileOutcomeKind.Permanent)]
    [InlineData(400, DocumentFileOutcomeKind.Permanent)]
    [InlineData(410, DocumentFileOutcomeKind.Permanent)]
    [InlineData(500, DocumentFileOutcomeKind.Transient)]
    [InlineData(502, DocumentFileOutcomeKind.Transient)]
    [InlineData(503, DocumentFileOutcomeKind.Transient)]
    [InlineData(429, DocumentFileOutcomeKind.Transient)]
    [InlineData(408, DocumentFileOutcomeKind.Transient)]
    [InlineData(401, DocumentFileOutcomeKind.Unauthorized)]
    [InlineData(403, DocumentFileOutcomeKind.Unauthorized)]
    [InlineData(200, DocumentFileOutcomeKind.Ok)]
    public void ClassifyStatus(int status, DocumentFileOutcomeKind expected) =>
        Assert.Equal(expected, DocumentFileErrorClassifier.ClassifyStatus(status));

    [Fact]
    public void ClassifyException_CircuitOpen_And_Others()
    {
        Assert.Equal(DocumentFileOutcomeKind.CircuitOpen,
            DocumentFileErrorClassifier.ClassifyException(new Polly.CircuitBreaker.BrokenCircuitException("open")));
        Assert.Equal(DocumentFileOutcomeKind.Transient, DocumentFileErrorClassifier.ClassifyException(new TaskCanceledException()));
        Assert.Equal(DocumentFileOutcomeKind.Transient, DocumentFileErrorClassifier.ClassifyException(new HttpRequestException("x")));
    }

    [Theory]
    [InlineData("application/json", true)]
    [InlineData("APPLICATION/JSON", true)]
    [InlineData("text/json", true)]
    [InlineData("image/jpeg", false)]
    [InlineData(null, false)]
    public void IsJsonMediaType(string? mediaType, bool expected) =>
        Assert.Equal(expected, DocumentFileErrorClassifier.IsJsonMediaType(mediaType));
}

public class DocumentFileThrottleTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);

    private static void Feed(DocumentFileThrottle t, int transientCount, int total, int max = 8)
    {
        for (var i = 0; i < total; i++) t.Record(i < transientCount, max, Now);
    }

    [Fact]
    public void HighTransientRatio_HalvesParallelAndCoolsDown()
    {
        var t = new DocumentFileThrottle(8);
        Feed(t, transientCount: 10, total: DocumentFileThrottle.WindowSize); // 50% > 30%
        Assert.Equal(4, t.Parallel);
        Assert.True(t.InCooldown(Now.AddMinutes(1)));
        Assert.False(t.InCooldown(Now.AddMinutes(3)));
    }

    [Fact]
    public void Parallel_NeverBelowOne()
    {
        var t = new DocumentFileThrottle(1);
        Feed(t, 20, 20);
        Assert.Equal(1, t.Parallel);
    }

    [Fact]
    public void TwoCleanWindows_IncreaseParallelUpToMax()
    {
        var t = new DocumentFileThrottle(2);
        Feed(t, 0, 20);
        Assert.Equal(2, t.Parallel); // chưa đủ 2 cửa sổ tốt
        Feed(t, 0, 20);
        Assert.Equal(3, t.Parallel);
        for (var i = 0; i < 20; i++) Feed(t, 0, 20, max: 4);
        Assert.Equal(4, t.Parallel);
    }

    [Fact]
    public void ConsecutiveTransient_AbortsBatch_UntilReset()
    {
        var t = new DocumentFileThrottle(4);
        for (var i = 0; i < DocumentFileThrottle.ConsecutiveTransientToAbort; i++) t.Record(true, 8, Now);
        Assert.True(t.ShouldAbortBatch);
        t.ResetBatch();
        Assert.False(t.ShouldAbortBatch);
    }

    [Fact]
    public void SuccessInterruptsConsecutiveTransient()
    {
        var t = new DocumentFileThrottle(4);
        for (var i = 0; i < 9; i++) t.Record(true, 8, Now);
        t.Record(false, 8, Now);
        for (var i = 0; i < 9; i++) t.Record(true, 8, Now);
        Assert.False(t.ShouldAbortBatch);
    }
}

public class DocumentFileSpoolTests
{
    private static MemoryStream Bytes(int n)
    {
        var data = new byte[n];
        new Random(7).NextBytes(data);
        return new MemoryStream(data);
    }

    [Fact]
    public async Task SmallFile_KeptInMemory_WithCorrectHash()
    {
        var src = Bytes(1000);
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(src.ToArray())).ToLowerInvariant();
        await using var result = await DocumentFileDownloadResult.SpoolAsync(src, 1000, 4096, 1_000_000, 200, default);
        Assert.NotNull(result);
        Assert.Equal(1000, result!.Length);
        Assert.Equal(expected, result.Sha256Hex);
        using var read = result.OpenRead();
        using var copy = new MemoryStream();
        await read.CopyToAsync(copy);
        Assert.Equal(src.ToArray(), copy.ToArray());
    }

    [Fact]
    public async Task LargeFile_UsesTempFile_AndDeletesOnDispose()
    {
        var src = Bytes(300_000);
        var result = await DocumentFileDownloadResult.SpoolAsync(src, 300_000, 4096, 10_000_000, 200, default);
        Assert.NotNull(result);
        Assert.Equal(300_000, result!.Length);
        using (var read = result.OpenRead())
        {
            Assert.IsType<FileStream>(read);
            var path = ((FileStream)read).Name;
            Assert.True(File.Exists(path));
            using var copy = new MemoryStream();
            await read.CopyToAsync(copy);
            Assert.Equal(src.ToArray(), copy.ToArray());
            read.Close();
            await result.DisposeAsync();
            Assert.False(File.Exists(path));
        }
    }

    [Fact]
    public async Task UnknownLength_LargerThanThreshold_SwitchesToTempFile()
    {
        var src = Bytes(50_000);
        await using var result = await DocumentFileDownloadResult.SpoolAsync(src, null, 4096, 10_000_000, 200, default);
        Assert.NotNull(result);
        Assert.Equal(50_000, result!.Length);
        using var read = result.OpenRead();
        Assert.IsType<FileStream>(read);
        using var copy = new MemoryStream();
        await read.CopyToAsync(copy);
        Assert.Equal(src.ToArray(), copy.ToArray());
    }

    [Fact]
    public async Task OverMaxBytes_ReturnsNull()
    {
        var result = await DocumentFileDownloadResult.SpoolAsync(Bytes(10_000), null, 4096, 5_000, 200, default);
        Assert.Null(result);
    }
}

public class DocumentOwnerSyncPlannerTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);
    private static readonly PmisDocumentSyncOptions Opt = new();

    private static DocumentOwnerState State(int remote, int local, int fetchedDaysAgo = 1, int? fullDaysAgo = 30, int? scanSkip = null) => new()
    {
        PmisCode = "T1", RemoteTotal = remote, LocalCount = local, ScanSkip = scanSkip,
        LastFetchAt = Now.AddDays(-fetchedDaysAgo), LastFullAt = fullDaysAgo == null ? null : Now.AddDays(-fullDaysAgo.Value), LastCountAt = Now.AddHours(-13)
    };

    [Fact]
    public void NoState_DbAlreadyComplete_Seeds()
    {
        var plan = DocumentOwnerSyncPlanner.Decide(null, 100, 100, Now, Opt);
        Assert.Equal(DocumentSyncAction.Seed, plan.Action);
    }

    [Fact]
    public void NoState_DbHasMore_Seeds()
        => Assert.Equal(DocumentSyncAction.Seed, DocumentOwnerSyncPlanner.Decide(null, 100, 120, Now, Opt).Action);

    [Fact]
    public void NoState_DbMissing_FullScan()
        => Assert.Equal(DocumentSyncAction.Full, DocumentOwnerSyncPlanner.Decide(null, 100, 40, Now, Opt).Action);

    [Fact]
    public void NoState_EmptyOwner_Seeds()
        => Assert.Equal(DocumentSyncAction.Seed, DocumentOwnerSyncPlanner.Decide(null, 0, 0, Now, Opt).Action);

    [Fact]
    public void NoState_HugeOwner_Windowed()
        => Assert.Equal(DocumentSyncAction.Windowed, DocumentOwnerSyncPlanner.Decide(null, 71_953, 50_000, Now, Opt).Action);

    [Fact]
    public void SameTotal_AllReceived_Skips()
        => Assert.Equal(DocumentSyncAction.Skip, DocumentOwnerSyncPlanner.Decide(State(21, 21), 21, null, Now, Opt).Action);

    [Fact]
    public void TotalIncreased_Delta_FromWatermarkMinusMargin()
    {
        var plan = DocumentOwnerSyncPlanner.Decide(State(21, 21, fetchedDaysAgo: 3), 24, null, Now, Opt);
        Assert.Equal(DocumentSyncAction.Delta, plan.Action);
        Assert.Equal(Now.AddDays(-3).AddDays(-7).Date, plan.TuNgay);
    }

    [Fact]
    public void TotalSame_ButReceivedLess_FullOnlyOncePerInterval()
    {
        var due = DocumentOwnerSyncPlanner.Decide(State(21, 15, fullDaysAgo: 8), 21, null, Now, Opt);
        Assert.Equal(DocumentSyncAction.Full, due.Action);
        var recent = DocumentOwnerSyncPlanner.Decide(State(21, 15, fullDaysAgo: 2), 21, null, Now, Opt);
        Assert.Equal(DocumentSyncAction.Skip, recent.Action);
    }

    [Fact]
    public void TotalDecreased_Skips_NoDeletion()
        => Assert.Equal(DocumentSyncAction.Skip, DocumentOwnerSyncPlanner.Decide(State(21, 21), 18, null, Now, Opt).Action);

    [Fact]
    public void InProgressScan_Resumes()
    {
        Assert.Equal(DocumentSyncAction.Full, DocumentOwnerSyncPlanner.Decide(State(300, 120, scanSkip: 120), 300, null, Now, Opt).Action);
        Assert.Equal(DocumentSyncAction.Windowed,
            DocumentOwnerSyncPlanner.Decide(State(71_953, 0, scanSkip: DocumentOwnerSyncPlanner.WindowedMarker), 71_953, null, Now, Opt).Action);
    }

    [Fact]
    public void IsCountDue_RespectsRecheckHours()
    {
        Assert.True(DocumentOwnerSyncPlanner.IsCountDue(null, Now, Opt));
        Assert.True(DocumentOwnerSyncPlanner.IsCountDue(new DocumentOwnerState { LastCountAt = Now.AddHours(-13) }, Now, Opt));
        Assert.False(DocumentOwnerSyncPlanner.IsCountDue(new DocumentOwnerState { LastCountAt = Now.AddHours(-11) }, Now, Opt));
    }

    [Fact]
    public void WindowKeyAndRange()
    {
        Assert.Equal("X|2024", DocumentOwnerSyncPlanner.WindowKey("X", 2024));
        Assert.Equal("X|202402", DocumentOwnerSyncPlanner.WindowKey("X", 2024, 2));
        Assert.Equal((new DateTime(2024, 1, 1), new DateTime(2024, 12, 31, 23, 59, 59)), DocumentOwnerSyncPlanner.WindowRange(2024));
        Assert.Equal((new DateTime(2024, 2, 1), new DateTime(2024, 2, 29, 23, 59, 59)), DocumentOwnerSyncPlanner.WindowRange(2024, 2));
    }
}
