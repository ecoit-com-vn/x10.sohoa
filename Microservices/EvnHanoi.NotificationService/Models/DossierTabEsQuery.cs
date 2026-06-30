namespace EvnHanoi.NotificationService.Models;

/// <summary>
/// Ánh xạ tab UI/API → điều kiện truy vấn Elasticsearch.
/// Tab slug (vd. pending-action) KHÔNG phải giá trị field <c>status</c> trong ES.
/// </summary>
public static class DossierTabEsQuery
{
    /// <summary>Giá trị <c>status</c> nghiệp vụ trên ES / Oracle DOSSIERS (3 = PendingApproval, 4 = InProgress).</summary>
    public static readonly int[] InPipelineStatuses = [3, 4];

    public static readonly string[] AllTabSlugs =
    [
        DossierListTabs.Draft,
        DossierListTabs.PendingAction,
        DossierListTabs.InProgress,
        DossierListTabs.Completed,
        DossierListTabs.Returned,
        DossierListTabs.PendingPublish,
        DossierListTabs.Published,
        DossierListTabs.Unpublished
    ];

    /// <summary>
    /// Tab ưu tiên; nếu client gửi nhầm slug tab vào query param thì vẫn map đúng.
    /// </summary>
    public static string? ResolveTabSlug(DossierFilterDto filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Tab))
        {
            var normalized = filter.Tab.Trim().ToLowerInvariant();
            return IsTabSlug(normalized) ? normalized : null;
        }

        return null;
    }

    /// <summary>Chỉ dùng khi không có tab — filter trực tiếp theo status nghiệp vụ ES.</summary>
    public static int? ResolveEsStatusFilter(DossierFilterDto filter)
    {
        if (ResolveTabSlug(filter) is not null)
            return null;

        if (!filter.StatusId.HasValue || filter.StatusId.Value <= 0)
            return null;

        return filter.StatusId.Value;
    }

    public static bool IsTabSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim().ToLowerInvariant();
        return v is DossierListTabs.Draft
            or DossierListTabs.PendingAction
            or DossierListTabs.InProgress
            or DossierListTabs.Completed
            or DossierListTabs.Returned
            or DossierListTabs.PendingPublish
            or DossierListTabs.Published
            or DossierListTabs.Unpublished;
    }
}
