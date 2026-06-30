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
        var scope = DossierMenuScopes.Normalize(filter.MenuScope);

        if (DossierMenuScopes.IsPublisher(scope))
        {
            counts.PendingPublish = await CountAsync(CloneForTab(filter, DossierListTabs.PendingPublish), keyword);
            counts.Published = await CountAsync(CloneForTab(filter, DossierListTabs.Published), keyword);
            counts.Unpublished = await CountAsync(CloneForTab(filter, DossierListTabs.Unpublished), keyword);
            return counts;
        }

        if (DossierMenuScopes.IsCreator(scope))
        {
            counts.Draft = await CountAsync(CloneForTab(filter, DossierListTabs.Draft), keyword);
            counts.PendingAction = 0;
            counts.InProgress = await CountAsync(CloneForTab(filter, DossierListTabs.InProgress), keyword);
            counts.Completed = await CountAsync(CloneForTab(filter, DossierListTabs.Completed), keyword);
            counts.Returned = await CountAsync(CloneForTab(filter, DossierListTabs.Returned), keyword);
            return counts;
        }

        if (DossierMenuScopes.IsApprover(scope))
        {
            counts.Draft = 0;
            counts.PendingAction = await CountAsync(CloneForTab(filter, DossierListTabs.PendingAction), keyword);
            counts.InProgress = await CountAsync(CloneForTab(filter, DossierListTabs.InProgress), keyword);
            counts.Completed = await CountAsync(CloneForTab(filter, DossierListTabs.Completed), keyword);
            counts.Returned = 0;
            return counts;
        }

        var tasks = new[]
        {
            CountAsync(CloneForTab(filter, DossierListTabs.Draft), keyword),
            CountAsync(CloneForTab(filter, DossierListTabs.PendingAction), keyword),
            CountAsync(CloneForTab(filter, DossierListTabs.InProgress), keyword),
            CountAsync(CloneForTab(filter, DossierListTabs.Completed), keyword),
            CountAsync(CloneForTab(filter, DossierListTabs.Returned), keyword),
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
        var tabSlug = DossierTabEsQuery.ResolveTabSlug(filter);
        if (tabSlug == DossierListTabs.Draft)
        {
            return items
                .Where(item => string.Equals(item.Status, "Draft", StringComparison.Ordinal) ||
                               string.Equals(item.Status, "New", StringComparison.Ordinal) ||
                               string.Equals(item.Status, "CompletedInput", StringComparison.Ordinal))
                .ToList();
        }

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
        MenuScope = source.MenuScope,
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
            var mustQueries = new List<Query>();
            var mustNotQueries = new List<Query>();
            var filterQueries = new List<Query>();

            mustNotQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.IsDeleted).Value(true)));
            EnforceTabStatusMust(mustQueries, mustNotQueries, filter);

            var filters = new List<Action<QueryDescriptor<DossierEsDocument>>>();

            if (filter.GridTypeId.HasValue)
                filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.GridTypeId).Value(filter.GridTypeId.Value)));

            if (filter.InfrastructureId.HasValue)
            {
                var infraVariants = DossierIndexIdNormalizer.GetGuidTermVariants(filter.InfrastructureId.Value.ToString());
                filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                    .Field(DossierEsFieldNames.InfrastructureId)
                    .Terms(new TermsQueryField(infraVariants.Select(FieldValue.String).ToArray()))));
            }

            if (filter.DossierTypeId.HasValue)
            {
                var dossierTypeVariants = DossierIndexIdNormalizer.GetGuidTermVariants(filter.DossierTypeId.Value.ToString());
                b.Filter(f => f.Terms(t => t
                    .Field(DossierEsFieldNames.DossierTypeId)
                    .Terms(new TermsQueryField(dossierTypeVariants.Select(FieldValue.String).ToArray()))));
            }

            if (filter.UnitScopeIds is { Count: > 0 })
            {
                filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                    .Field(DossierEsFieldNames.UnitId)
                    .Terms(new TermsQueryField(filter.UnitScopeIds.Select(FieldValue.Long).ToArray()))));
            }

            ApplyTabOrStatusFilter(mustQueries, filterQueries, mustNotQueries, filter);
            ApplyVisibilityFilter(filterQueries, filter);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Bool(bb => bb
                    .MinimumShouldMatch(1)
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
                ));
            }

            if (mustQueries.Count > 0)
                b.Must(mustQueries);
            if (mustNotQueries.Count > 0)
                b.MustNot(mustNotQueries);
            if (filterQueries.Count > 0)
                b.Filter(filterQueries);
        });
    }

    /// <summary>
    /// Ép filter status nghiệp vụ theo tab — Must (không Filter) để tránh bool query ES bị bỏ qua.
    /// </summary>
    private static void EnforceTabStatusMust(List<Query> mustQueries, List<Query> mustNotQueries, DossierFilterDto filter)
    {
        switch (DossierTabEsQuery.ResolveTabSlug(filter))
        {
            case DossierListTabs.Draft:
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                    .Field(DossierEsFieldNames.Status)
                    .Terms(new TermsQueryField(new[] { "Draft", "New", "CompletedInput" }.Select(FieldValue.String).ToArray()))));
                mustNotQueries.Add(new QueryDescriptor<DossierEsDocument>().Exists(e => e.Field(DossierEsFieldNames.WorkflowInstanceId)));
                break;

            case DossierListTabs.Completed:
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.Status).Value("Approved")));
                mustNotQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t
                    .Field(DossierEsFieldNames.WorkflowInstanceStatus)
                    .Value("Running")));
                break;

            case DossierListTabs.Returned:
                // WF đang chạy, bị từ chối/trả lại và quay về bước đầu người tạo.
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.WorkflowInstanceStatus).Value("Running")));
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Exists(e => e.Field(DossierEsFieldNames.WorkflowInstanceId)));
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.IsReturnedToCreatorStep).Value(true)));
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.Status).Value("Returned")));
                break;

            case DossierListTabs.InProgress:
            case DossierListTabs.PendingAction:
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                    .Field(DossierEsFieldNames.Status)
                    .Terms(new TermsQueryField(
                        DossierTabEsQuery.InPipelineStatuses.Select(FieldValue.String).ToArray()))));
                break;

            case DossierListTabs.PendingPublish:
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.Status).Value("Approved")));
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Bool(bb => bb
                    .MinimumShouldMatch(1)
                    .Should(
                        sh => sh.Term(t => t.Field(DossierEsFieldNames.PublishStatusId).Value(1)),
                        sh => sh.Bool(bNull => bNull.MustNot(mn => mn.Exists(e => e.Field(DossierEsFieldNames.PublishStatusId))))
                    )
                ));
                break;

            case DossierListTabs.Published:
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.PublishStatusId).Value(2)));
                break;

            case DossierListTabs.Unpublished:
                mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.PublishStatusId).Value(3)));
                break;
        }
    }

    private static void ApplyTabOrStatusFilter(List<Query> mustQueries, List<Query> filterQueries, List<Query> mustNotQueries, DossierFilterDto filter)
    {
        var tabSlug = DossierTabEsQuery.ResolveTabSlug(filter);
        if (tabSlug is not null)
        {
            ApplyTabSlugFilter(mustQueries, filterQueries, mustNotQueries, tabSlug, filter);
            return;
        }

        var esStatus = DossierTabEsQuery.ResolveEsStatusFilter(filter);
        if (esStatus is not null)
            filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.Status).Value(esStatus)));
    }

    /// <summary>
    /// Tab UI (slug) → filter ES. Không bao giờ gửi slug tab vào field status.
    /// </summary>
    private static void ApplyTabSlugFilter(
        List<Query> mustQueries,
        List<Query> filterQueries,
        List<Query> mustNotQueries,
        string tabSlug,
        DossierFilterDto filter)
    {
        switch (tabSlug)
        {
            case DossierListTabs.Draft:
                break;

            case DossierListTabs.InProgress:
                // Tab "Đang xử lý" — loại inbox cá nhân (status đã ép ở EnforceTabStatusMust).
                ApplyActiveWorkflowTabFilter(filterQueries, mustNotQueries);
                
                var innerBool = new QueryDescriptor<DossierEsDocument>();
                innerBool.Bool(bb =>
                {
                    ApplyPendingActionInboxMatch(bb, filter);
                });
                mustNotQueries.Add(innerBool);
                break;

            case DossierListTabs.Completed:
            case DossierListTabs.Returned:
            case DossierListTabs.PendingPublish:
            case DossierListTabs.Published:
            case DossierListTabs.Unpublished:
                // Status đã ép ở EnforceTabStatusMust.
                break;

            case DossierListTabs.PendingAction:
                // Status đã ép ở EnforceTabStatusMust — chỉ thêm inbox.
                ApplyPendingActionInboxClause(mustQueries, filterQueries, filter);
                break;
        }
    }

    private static void ApplyActiveWorkflowTabFilter(List<Query> filterQueries, List<Query> mustNotQueries)
    {
        filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Exists(e => e.Field(DossierEsFieldNames.WorkflowInstanceId)));
        mustNotQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.WorkflowInstanceStatus).Value("Completed")));
        mustNotQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.WorkflowInstanceStatus).Value("Terminated")));
    }

    /// <summary>
    /// Inbox tab Chờ xử lý — ưu tiên filter phẳng (terms) như /diag/inbox; OR role/creator qua Must bool.
    /// </summary>
    private static void ApplyPendingActionInboxClause(List<Query> mustQueries, List<Query> filterQueries, DossierFilterDto filter)
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
            filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));
            return;
        }

        // userId + roles → Must bool OR (assignee trực tiếp hoặc role pool sau submit/move).
        if (hasUserId && hasRoles)
        {
            mustQueries.Add(new QueryDescriptor<DossierEsDocument>().Bool(bb => bb
                .MinimumShouldMatch(1)
                .Should(
                    sh => sh.Terms(t => t
                        .Field(DossierEsFieldNames.PendingAssigneeUserId)
                        .Terms(new TermsQueryField(UserIdTermValues(userId!)))),
                    sh => sh.Terms(t => t
                        .Field(DossierEsFieldNames.PendingAssignedRoles)
                        .Terms(new TermsQueryField(roles!.Select(FieldValue.String).ToArray())))
                )));
            return;
        }

        if (hasUserId)
        {
            filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                .Field(DossierEsFieldNames.PendingAssigneeUserId)
                .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
            return;
        }

        var rolePoolBool = new QueryDescriptor<DossierEsDocument>();
        rolePoolBool.Bool(bb => ApplyPendingActionRolePoolInbox(bb, roles!));
        filterQueries.Add(rolePoolBool);
    }

    /// <summary>OR inbox: assignee | role | creator (bước đầu chưa gán).</summary>
    private static void ApplyPendingActionInboxOrFilter(
        BoolQueryDescriptor<DossierEsDocument> bb,
        string userId,
        string[] roles)
    {
        bb.MinimumShouldMatch(1);

        var shouldQueries = new List<Query>();

        shouldQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
            .Field(DossierEsFieldNames.PendingAssigneeUserId)
            .Terms(new TermsQueryField(UserIdTermValues(userId)))));

        shouldQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
            .Field(DossierEsFieldNames.PendingAssignedRoles)
            .Terms(new TermsQueryField(roles.Select(FieldValue.String).ToArray()))));

        var creatorInbox = new QueryDescriptor<DossierEsDocument>();
        creatorInbox.Bool(cb =>
        {
            cb.Must(m => m.Terms(t => t
                .Field(DossierEsFieldNames.CreatorId)
                .Terms(new TermsQueryField(UserIdTermValues(userId)))));
            cb.MustNot(mn => mn.Exists(e => e
                .Field(DossierEsFieldNames.PendingAssigneeUserId)));
            cb.MustNot(mn => mn.Exists(e => e
                .Field(DossierEsFieldNames.PendingAssignedRoles)));
        });
        shouldQueries.Add(creatorInbox);

        bb.Should(shouldQueries);
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

        var shouldQueries = new List<Query>();

        if (hasUserId)
        {
            shouldQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                .Field(DossierEsFieldNames.PendingAssigneeUserId)
                .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
        }
        else if (hasRoles)
        {
            var rolePool = new QueryDescriptor<DossierEsDocument>();
            rolePool.Bool(rolePoolBool =>
            {
                rolePoolBool.Must(m => m.Terms(t => t
                    .Field(DossierEsFieldNames.PendingAssignedRoles)
                    .Terms(new TermsQueryField(roles!.Select(FieldValue.String).ToArray()))));
                rolePoolBool.MustNot(mn => mn.Exists(e => e
                    .Field(DossierEsFieldNames.PendingAssigneeUserId)));
            });
            shouldQueries.Add(rolePool);
        }

        bb.Should(shouldQueries);
    }

    /// <summary>
    /// Phân quyền xem hồ sơ theo tab / menuScope. ADMIN xem tất cả.
    /// Tab pending-action đã có inbox filter riêng — không lặp lại ở đây.
    /// </summary>
    private static void ApplyVisibilityFilter(List<Query> filterQueries, DossierFilterDto filter)
    {
        var scope = DossierMenuScopes.Normalize(filter.MenuScope);
        if (filter.IsAdmin || DossierMenuScopes.IsPublisher(scope))
            return;

        var userId = NormalizeFilterUserId(filter.UserId);
        var roles = filter.UserRoles?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (string.IsNullOrWhiteSpace(userId) && (roles is null or { Length: 0 }))
        {
            filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));
            return;
        }

        var tab = DossierTabEsQuery.ResolveTabSlug(filter) ?? filter.Tab?.Trim().ToLowerInvariant();

        if (DossierMenuScopes.IsCreator(scope))
        {
            ApplyCreatorOnlyVisibility(filterQueries, userId);
            return;
        }

        if (DossierMenuScopes.IsApprover(scope))
        {
            switch (tab)
            {
                case DossierListTabs.PendingAction:
                    // Inbox đã được lọc trong ApplyTabOrStatusFilter.
                    break;
                case DossierListTabs.InProgress:
                case DossierListTabs.Completed:
                    ApplyParticipantOnlyVisibility(filterQueries, userId);
                    break;
                default:
                    filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));
                    break;
            }
            return;
        }

        switch (tab)
        {
            case DossierListTabs.Draft:
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                        .Field(DossierEsFieldNames.CreatorId)
                        .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
                }
                break;

            case DossierListTabs.PendingAction:
                break;

            case DossierListTabs.InProgress:
                var inProgressBool = new QueryDescriptor<DossierEsDocument>();
                inProgressBool.Bool(bb => ApplyCreatorOrParticipantVisibility(bb, userId));
                filterQueries.Add(inProgressBool);
                break;

            case DossierListTabs.Completed:
            case DossierListTabs.Returned:
                var creatorOrPartBool = new QueryDescriptor<DossierEsDocument>();
                creatorOrPartBool.Bool(bb => ApplyCreatorOrParticipantVisibility(bb, userId));
                filterQueries.Add(creatorOrPartBool);
                break;

            default:
                var pipelineBool = new QueryDescriptor<DossierEsDocument>();
                pipelineBool.Bool(bb => ApplyPipelineVisibility(bb, userId, roles, includeInbox: true));
                filterQueries.Add(pipelineBool);
                break;
        }
    }

    private static void ApplyCreatorOnlyVisibility(List<Query> filterQueries, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));
            return;
        }

        filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
            .Field(DossierEsFieldNames.CreatorId)
            .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
    }

    private static void ApplyParticipantOnlyVisibility(List<Query> filterQueries, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));
            return;
        }

        filterQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
            .Field(DossierEsFieldNames.WorkflowParticipantUserIds)
            .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
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

        bb.Should(
            sh => sh.Terms(t => t
                .Field(DossierEsFieldNames.CreatorId)
                .Terms(new TermsQueryField(UserIdTermValues(userId!)))),
            sh => sh.Terms(t => t
                .Field(DossierEsFieldNames.WorkflowParticipantUserIds)
                .Terms(new TermsQueryField(UserIdTermValues(userId!))))
        );
    }

    private static void ApplyPipelineVisibility(
        BoolQueryDescriptor<DossierEsDocument> bb,
        string? userId,
        string[]? roles,
        bool includeInbox)
    {
        bb.MinimumShouldMatch(1);

        var shouldQueries = new List<Query>();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            shouldQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                .Field(DossierEsFieldNames.CreatorId)
                .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
            shouldQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                .Field(DossierEsFieldNames.WorkflowParticipantUserIds)
                .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
        }

        if (includeInbox)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                shouldQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                    .Field(DossierEsFieldNames.PendingAssigneeUserId)
                    .Terms(new TermsQueryField(UserIdTermValues(userId!)))));
            }

            if (roles is { Length: > 0 })
            {
                shouldQueries.Add(new QueryDescriptor<DossierEsDocument>().Terms(t => t
                    .Field(DossierEsFieldNames.PendingAssignedRoles)
                    .Terms(new TermsQueryField(roles.Select(FieldValue.String).ToArray()))));
            }
        }

        if (shouldQueries.Count == 0)
            shouldQueries.Add(new QueryDescriptor<DossierEsDocument>().Term(t => t.Field(DossierEsFieldNames.Id).Value("__none__")));

        bb.Should(shouldQueries);
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
            Creator = new CreatorInfoDto
            {
                Id = doc.CreatorId ?? string.Empty,
                Username = doc.CreatorUsername ?? string.Empty,
                Name = doc.CreatorName ?? string.Empty
            },
            PendingAssigneeUserId = doc.PendingAssigneeUserId,
            PendingAssignedRoles = doc.PendingAssignedRoles ?? new List<string>(),
            WorkflowParticipantUserIds = doc.WorkflowParticipantUserIds ?? new List<string>(),
            CreatedDate = doc.CreatedDate,
            CurrentStepId = doc.CurrentStepId,
            CurrentAssignees = doc.CurrentAssignees ?? new List<string>(),
            AvailableActions = doc.AvailableActions ?? new List<WorkflowActionEsDto>(),
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
