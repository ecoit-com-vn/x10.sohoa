using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Services;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.NotificationService.Repositories;

public class DossierSearchRepository : IDossierSearchRepository
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<DossierSearchRepository> _logger;

    public DossierSearchRepository(ElasticsearchClient client, ILogger<DossierSearchRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(
        DossierFilterDto filter,
        IReadOnlyList<BhsCatalogDefinition> bhsCatalogs)
    {
        var from = (filter.Page - 1) * filter.PageSize;
        var keyword = filter.Keyword?.Trim();

        var response = await _client.SearchAsync<DossierEsDocument>(s => s
            .Indices(DossierMessaging.IndexName)
            .From(from)
            .Size(filter.PageSize)
            .TrackTotalHits(true)
            .Sort(sort => sort.Field(f => f.CreatedDate, fs => fs.Order(SortOrder.Desc)))
            .Query(q => { ConfigureQuery(q, filter, keyword); })
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Elasticsearch dossier search failed: {Error}",
                response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation);
            throw new InvalidOperationException("Không thể truy vấn danh sách hồ sơ từ Elasticsearch.");
        }

        var items = response.Documents.Select(doc => MapToListItem(doc, bhsCatalogs)).ToList();
        items = FilterItemsForTab(items, filter);
        var totalCount = await CountAsync(filter, keyword);
        return (items, totalCount);
    }

    public async Task<DossierTabCountsDto> GetTabCountsAsync(DossierFilterDto filter)
    {
        var keyword = filter.Keyword?.Trim();
        var counts = new DossierTabCountsDto();

        var draftFilter = CloneForTab(filter, DossierListTabs.Draft);
        var pendingFilter = CloneForTab(filter, DossierListTabs.PendingAction);
        var inProgressFilter = CloneForTab(filter, DossierListTabs.InProgress);
        var completedFilter = CloneForTab(filter, DossierListTabs.Completed);
        var returnedFilter = CloneForTab(filter, DossierListTabs.Returned);

        var tasks = new[]
        {
            CountAsync(draftFilter, keyword),
            CountAsync(pendingFilter, keyword),
            CountAsync(inProgressFilter, keyword),
            CountAsync(completedFilter, keyword),
            CountAsync(returnedFilter, keyword),
        };

        var results = await Task.WhenAll(tasks);
        counts.Draft = results[0];
        counts.PendingAction = results[1];
        counts.InProgress = results[2];
        counts.Completed = results[3];
        counts.Returned = results[4];

        return counts;
    }

    private async Task<int> CountAsync(DossierFilterDto filter, string? keyword)
    {
        var response = await _client.SearchAsync<DossierEsDocument>(s => s
            .Indices(DossierMessaging.IndexName)
            .Size(0)
            .TrackTotalHits(true)
            .Query(q => { ConfigureQuery(q, filter, keyword); })
        );

        if (!response.IsValidResponse)
        {
            _logger.LogWarning(
                "Elasticsearch dossier tab count failed for tab {Tab}: {Error}",
                filter.Tab,
                response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation);
            return 0;
        }

        return (int)response.Total;
    }

    private static List<DossierListItemDto> FilterItemsForTab(
        List<DossierListItemDto> items,
        DossierFilterDto filter)
    {
        var expectedStatus = ResolveExpectedBusinessStatus(filter);
        if (expectedStatus is null)
            return items;

        return items
            .Where(item => string.Equals(item.Status, expectedStatus, StringComparison.Ordinal))
            .ToList();
    }

    private static string? ResolveExpectedBusinessStatus(DossierFilterDto filter)
    {
        return DossierTabEsQuery.ResolveTabSlug(filter) switch
        {
            DossierListTabs.Draft => "Draft",
            DossierListTabs.Completed => "Approved",
            DossierListTabs.Returned => "Returned",
            _ => null
        };
    }

    private static DossierFilterDto CloneForTab(DossierFilterDto source, string tab) => new()
    {
        Keyword = source.Keyword,
        InfrastructureId = source.InfrastructureId,
        GridTypeId = source.GridTypeId,
        UnitId = source.UnitId,
        UnitScopeIds = source.UnitScopeIds,
        UserId = source.UserId,
        UserRoles = source.UserRoles,
        IsAdmin = source.IsAdmin,
        Tab = tab,
        Page = 1,
        PageSize = 1
    };

    internal static void ConfigureQuery(
        QueryDescriptor<DossierEsDocument> q,
        DossierFilterDto filter,
        string? keyword)
    {
        q.Bool(b =>
        {
            b.MustNot(mn => mn.Term(t => t.Field(DossierEsFieldNames.IsDeleted).Value(true)));
            EnforceTabStatusMust(b, filter);

            if (filter.GridTypeId.HasValue)
                b.Filter(f => f.Term(t => t.Field(DossierEsFieldNames.GridTypeId).Value(filter.GridTypeId.Value)));

            if (filter.InfrastructureId.HasValue)
            {
                var infraVariants = DossierIndexIdNormalizer.GetGuidTermVariants(filter.InfrastructureId.Value.ToString());
                b.Filter(f => f.Terms(t => t
                    .Field(DossierEsFieldNames.InfrastructureId)
                    .Terms(new TermsQueryField(infraVariants.Select(FieldValue.String).ToArray()))));
            }

            if (filter.UnitScopeIds is { Count: > 0 })
            {
                b.Filter(f => f.Terms(t => t
                    .Field(DossierEsFieldNames.UnitId)
                    .Terms(new TermsQueryField(filter.UnitScopeIds.Select(FieldValue.Long).ToArray()))));
            }

            ApplyTabOrStatusFilter(b, filter);
            ApplyVisibilityFilter(b, filter);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                b.Must(m => m.Bool(bb => bb
                    .Should(
                        sh => sh.Nested(n => n
                            .Path(p => p.FormFields)
                            .Query(nq => nq.Match(mq => mq
                                .Field("formFields.textValue")
                                .Query(keyword)
                            ))
                        ),
                        sh => sh.Nested(n => n
                            .Path(p => p.CatalogFields)
                            .Query(nq => nq.Match(mq => mq
                                .Field("catalogFields.value")
                                .Query(keyword)
                            ))
                        ),
                        sh => sh.Match(mq => mq.Field(f => f.InfrastructureName).Query(keyword)),
                        sh => sh.Match(mq => mq.Field(f => f.InfrastructureCode).Query(keyword)),
                        sh => sh.Match(mq => mq.Field(f => f.CreatorName).Query(keyword)),
                        sh => sh.Match(mq => mq.Field(f => f.DossierSetName).Query(keyword)),
                        sh => sh.Match(mq => mq.Field(f => f.DossierTypeName).Query(keyword))
                    )
                    .MinimumShouldMatch(1)
                ));
            }
        });
    }

    /// <summary>
    /// Ép filter status nghiệp vụ theo tab — Must (không Filter) để tránh bool query ES bị bỏ qua.
    /// </summary>
    private static void EnforceTabStatusMust(BoolQueryDescriptor<DossierEsDocument> b, DossierFilterDto filter)
    {
        switch (DossierTabEsQuery.ResolveTabSlug(filter))
        {
            case DossierListTabs.Draft:
                b.Must(m => m.Term(t => t.Field(DossierEsFieldNames.Status).Value("Draft")));
                b.MustNot(mn => mn.Exists(e => e.Field(DossierEsFieldNames.WorkflowInstanceId)));
                break;

            case DossierListTabs.Completed:
                b.Must(m => m.Term(t => t.Field(DossierEsFieldNames.Status).Value("Approved")));
                b.MustNot(mn => mn.Term(t => t
                    .Field(DossierEsFieldNames.WorkflowInstanceStatus)
                    .Value("Running")));
                break;

            case DossierListTabs.Returned:
                b.Must(m => m.Term(t => t.Field(DossierEsFieldNames.Status).Value("Returned")));
                b.MustNot(mn => mn.Term(t => t
                    .Field(DossierEsFieldNames.WorkflowInstanceStatus)
                    .Value("Running")));
                break;

            case DossierListTabs.InProgress:
            case DossierListTabs.PendingAction:
                b.Must(m => m.Terms(t => t
                    .Field(DossierEsFieldNames.Status)
                    .Terms(new TermsQueryField(
                        DossierTabEsQuery.InPipelineStatuses.Select(FieldValue.String).ToArray()))));
                break;
        }
    }

    private static void ApplyTabOrStatusFilter(BoolQueryDescriptor<DossierEsDocument> b, DossierFilterDto filter)
    {
        var tabSlug = DossierTabEsQuery.ResolveTabSlug(filter);
        if (tabSlug is not null)
        {
            ApplyTabSlugFilter(b, tabSlug, filter);
            return;
        }

        var esStatus = DossierTabEsQuery.ResolveEsStatusFilter(filter);
        if (esStatus is not null)
            b.Filter(f => f.Term(t => t.Field(DossierEsFieldNames.Status).Value(esStatus)));
    }

    /// <summary>
    /// Tab UI (slug) → filter ES. Không bao giờ gửi slug tab vào field status.
    /// </summary>
    private static void ApplyTabSlugFilter(
        BoolQueryDescriptor<DossierEsDocument> b,
        string tabSlug,
        DossierFilterDto filter)
    {
        switch (tabSlug)
        {
            case DossierListTabs.Draft:
                break;

            case DossierListTabs.InProgress:
                // Tab "Đang xử lý" — loại inbox cá nhân (status đã ép ở EnforceTabStatusMust).
                ApplyActiveWorkflowTabFilter(b);
                b.MustNot(mn => mn.Bool(bb =>
                {
                    bb.MinimumShouldMatch(1);
                    ApplyPendingActionInboxMatch(bb, filter);
                }));
                break;

            case DossierListTabs.Completed:
            case DossierListTabs.Returned:
                // Status đã ép ở EnforceTabStatusMust.
                break;

            case DossierListTabs.PendingAction:
                // Status đã ép ở EnforceTabStatusMust — chỉ thêm inbox.
                ApplyPendingActionInboxClause(b, filter);
                break;
        }
    }

    private static void ApplyActiveWorkflowTabFilter(BoolQueryDescriptor<DossierEsDocument> b)
    {
        b.Filter(f => f.Exists(e => e.Field(DossierEsFieldNames.WorkflowInstanceId)));
        b.MustNot(mn => mn.Term(t => t.Field(DossierEsFieldNames.WorkflowInstanceStatus).Value("Completed")));
        b.MustNot(mn => mn.Term(t => t.Field(DossierEsFieldNames.WorkflowInstanceStatus).Value("Terminated")));
    }

    /// <summary>
    /// Inbox tab Chờ xử lý — ưu tiên filter phẳng (terms) như /diag/inbox; OR role/creator qua Must bool.
    /// </summary>
    private static void ApplyPendingActionInboxClause(BoolQueryDescriptor<DossierEsDocument> b, DossierFilterDto filter)
    {
        var userId = NormalizeFilterUserId(filter.UserId);
        var roles = filter.UserRoles?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasUserId = !string.IsNullOrWhiteSpace(userId);
        var hasRoles = roles is { Length: > 0 };

        if (!hasUserId && !hasRoles)
        {
            b.Filter(f => f.Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));
            return;
        }

        // userId + roles → Must bool OR (assignee trực tiếp hoặc role pool sau submit/move).
        if (hasUserId && hasRoles)
        {
            b.Must(m => m.Bool(bb =>
            {
                bb.MinimumShouldMatch(1);
                bb.Should(sh => sh.Terms(t => t
                    .Field(DossierEsFieldNames.PendingAssigneeUserId)
                    .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
                bb.Should(sh => sh.Terms(t => t
                    .Field(DossierEsFieldNames.PendingAssignedRoles)
                    .Terms(new TermsQueryField(roles!.Select(FieldValue.String).ToArray()))));
            }));
            return;
        }

        if (hasUserId)
        {
            b.Filter(f => f.Terms(t => t
                .Field(DossierEsFieldNames.PendingAssigneeUserId)
                .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
            return;
        }

        b.Filter(f => f.Bool(bb => ApplyPendingActionRolePoolInbox(bb, roles!)));
    }

    /// <summary>OR inbox: assignee | role | creator (bước đầu chưa gán).</summary>
    private static void ApplyPendingActionInboxOrFilter(
        BoolQueryDescriptor<DossierEsDocument> bb,
        string userId,
        string[] roles)
    {
        bb.MinimumShouldMatch(1);

        bb.Should(sh => sh.Terms(t => t
            .Field(DossierEsFieldNames.PendingAssigneeUserId)
            .Terms(new TermsQueryField(UserIdTermValues(userId)))));

        bb.Should(sh => sh.Terms(t => t
            .Field(DossierEsFieldNames.PendingAssignedRoles)
            .Terms(new TermsQueryField(roles.Select(FieldValue.String).ToArray()))));

        bb.Should(sh => sh.Bool(creatorInbox =>
        {
            creatorInbox.Must(m => m.Terms(t => t
                .Field(DossierEsFieldNames.CreatorId)
                .Terms(new TermsQueryField(UserIdTermValues(userId)))));
            creatorInbox.MustNot(mn => mn.Exists(e => e
                .Field(DossierEsFieldNames.PendingAssigneeUserId)));
            creatorInbox.MustNot(mn => mn.Exists(e => e
                .Field(DossierEsFieldNames.PendingAssignedRoles)));
        }));
    }

    /// <summary>Role pool khi chưa gán cá nhân.</summary>
    private static void ApplyPendingActionRolePoolInbox(
        BoolQueryDescriptor<DossierEsDocument> bb,
        string[] roles)
    {
        bb.Must(m => m.Terms(t => t
            .Field(DossierEsFieldNames.PendingAssignedRoles)
            .Terms(new TermsQueryField(roles.Select(FieldValue.String).ToArray()))));
        bb.MustNot(mn => mn.Exists(e => e
            .Field(DossierEsFieldNames.PendingAssigneeUserId)));
    }

    /// <summary>Doc đang nằm trong inbox pending của user (dùng loại trừ tab in-progress).</summary>
    internal static void ApplyPendingActionInboxMatch(BoolQueryDescriptor<DossierEsDocument> bb, DossierFilterDto filter)
    {
        bb.MinimumShouldMatch(1);

        var userId = NormalizeFilterUserId(filter.UserId);
        var roles = filter.UserRoles?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasUserId = !string.IsNullOrWhiteSpace(userId);
        var hasRoles = roles is { Length: > 0 };

        if (!hasUserId && !hasRoles)
        {
            bb.Should(sh => sh.Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));
            return;
        }

        if (hasUserId)
        {
            bb.Should(sh => sh.Terms(t => t
                .Field(DossierEsFieldNames.PendingAssigneeUserId)
                .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
            return;
        }

        bb.Should(sh => sh.Bool(rolePool =>
        {
            rolePool.Must(m => m.Terms(t => t
                .Field(DossierEsFieldNames.PendingAssignedRoles)
                .Terms(new TermsQueryField(roles!.Select(FieldValue.String).ToArray()))));
            rolePool.MustNot(mn => mn.Exists(e => e
                .Field(DossierEsFieldNames.PendingAssigneeUserId)));
        }));
    }

    /// <summary>
    /// Phân quyền xem hồ sơ theo tab. ADMIN xem tất cả.
    /// Tab pending-action đã có inbox filter riêng — không lặp lại ở đây.
    /// </summary>
    private static void ApplyVisibilityFilter(BoolQueryDescriptor<DossierEsDocument> b, DossierFilterDto filter)
    {
        if (filter.IsAdmin)
            return;

        var userId = NormalizeFilterUserId(filter.UserId);
        var roles = filter.UserRoles?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (string.IsNullOrWhiteSpace(userId) && (roles is null or { Length: 0 }))
        {
            b.Filter(f => f.Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));
            return;
        }

        var tab = DossierTabEsQuery.ResolveTabSlug(filter) ?? filter.Tab?.Trim().ToLowerInvariant();

        switch (tab)
        {
            case DossierListTabs.Draft:
                // Tab Nháp: chỉ hồ sơ do user đang đăng nhập tạo (kết hợp status=Draft ở tab filter).
                if (!string.IsNullOrWhiteSpace(userId))
                    b.Filter(f => f.Terms(t => t
                        .Field(DossierEsFieldNames.CreatorId)
                        .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
                break;

            case DossierListTabs.PendingAction:
                // Inbox đã được lọc trong ApplyTabOrStatusFilter — không thêm OR rộng tránh trùng tab khác.
                break;

            case DossierListTabs.InProgress:
                // Người tạo hoặc đã tham gia WF — không gồm inbox đang chờ xử lý (lọc ở tab filter).
                b.Filter(f => f.Bool(bb => ApplyCreatorOrParticipantVisibility(bb, userId)));
                break;

            case DossierListTabs.Completed:
            case DossierListTabs.Returned:
                b.Filter(f => f.Bool(bb => ApplyCreatorOrParticipantVisibility(bb, userId)));
                break;

            default:
                b.Filter(f => f.Bool(bb => ApplyPipelineVisibility(bb, userId, roles, includeInbox: true)));
                break;
        }
    }

    private static void ApplyCreatorOrParticipantVisibility(
        BoolQueryDescriptor<DossierEsDocument> bb,
        string? userId)
    {
        bb.MinimumShouldMatch(1);

        if (string.IsNullOrWhiteSpace(userId))
        {
            bb.Should(sh => sh.Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));
            return;
        }

        bb.Should(sh => sh.Terms(t => t
            .Field(DossierEsFieldNames.CreatorId)
            .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
        bb.Should(sh => sh.Terms(t => t
            .Field(DossierEsFieldNames.WorkflowParticipantUserIds)
            .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
    }

    private static void ApplyPipelineVisibility(
        BoolQueryDescriptor<DossierEsDocument> bb,
        string? userId,
        string[]? roles,
        bool includeInbox)
    {
        bb.MinimumShouldMatch(1);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            bb.Should(sh => sh.Terms(t => t
                .Field(DossierEsFieldNames.CreatorId)
                .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
            bb.Should(sh => sh.Terms(t => t
                .Field(DossierEsFieldNames.WorkflowParticipantUserIds)
                .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
        }

        if (includeInbox)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                bb.Should(sh => sh.Terms(t => t
                    .Field(DossierEsFieldNames.PendingAssigneeUserId)
                    .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
            }

            if (roles is { Length: > 0 })
            {
                bb.Should(sh => sh.Terms(t => t
                    .Field(DossierEsFieldNames.PendingAssignedRoles)
                    .Terms(new TermsQueryField(roles.Select(FieldValue.String).ToArray()))));
            }
        }

        if (string.IsNullOrWhiteSpace(userId) && (roles is null or { Length: 0 }))
            bb.Should(sh => sh.Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));
    }

    private static DossierListItemDto MapToListItem(
        DossierEsDocument doc,
        IReadOnlyList<BhsCatalogDefinition> bhsCatalogs)
    {
        return new DossierListItemDto
        {
            Id = Guid.TryParse(doc.Id, out var id) ? id : Guid.Empty,
            GridTypeId = doc.GridTypeId,
            GridTypeName = doc.GridTypeName,
            InfrastructureId = Guid.TryParse(doc.InfrastructureId, out var infraId) ? infraId : null,
            InfrastructureName = doc.InfrastructureName,
            InfrastructureCode = doc.InfrastructureCode,
            DossierSetId = Guid.TryParse(doc.DossierSetId, out var setId) ? setId : null,
            DossierSetName = doc.DossierSetName,
            DossierTypeId = Guid.TryParse(doc.DossierTypeId, out var typeId) ? typeId : Guid.Empty,
            DossierTypeName = doc.DossierTypeName,
            Status = doc.Status,
            WorkflowStepName = doc.WorkflowStatusName,
            WorkflowInstanceId = Guid.TryParse(doc.WorkflowInstanceId, out var wfId) ? wfId : null,
            WorkflowInstanceStatus = doc.WorkflowInstanceStatus,
            CurrentStepAllowEdit = doc.CurrentStepAllowEdit,
            DocumentCount = doc.DocumentCount,
            CreatorId = doc.CreatorId,
            CreatorName = doc.CreatorName,
            PendingAssigneeUserId = doc.PendingAssigneeUserId,
            PendingAssignedRoles = doc.PendingAssignedRoles ?? new List<string>(),
            WorkflowParticipantUserIds = doc.WorkflowParticipantUserIds ?? new List<string>(),
            CreatedDate = doc.CreatedDate,
            CatalogData = DossierCatalogDataMapper.ToCatalogData(doc.CatalogFields, doc.FormFields, bhsCatalogs)
        };
    }

    private static string? NormalizeFilterUserId(string? userId) =>
        string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();

    /// <summary>
    /// Term keyword GUID: khớp mọi biến thể hoa/thường và có/không dấu gạch (doc ES cũ + mới).
    /// </summary>
    private static FieldValue[] UserIdTermValues(string userId) =>
        DossierIndexIdNormalizer.GetGuidTermVariants(userId)
            .Select(FieldValue.String)
            .ToArray();
}
