using System.Data;

using Dapper;

using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;

using Microsoft.Extensions.Configuration;

using Oracle.ManagedDataAccess.Client;



namespace EvnHanoi.NotificationService.Repositories;



/// <summary>

/// Enrich hồ sơ từ Oracle trước khi index ES.

/// WORKFLOWINSTANCES.Status: Running | Completed | Terminated (không có Pending).

/// Inbox ES lấy từ WORKFLOWTASKS.Status = 'Pending' khi instance đang Running.

/// </summary>

public class DossierEnrichmentRepository : IDossierEnrichmentRepository

{

    private const int DossierWorkflowTypeId = 1;
    private const int DossierDigitizationWorkflowTypeId = 3;

    private static int ResolveWorkflowTypeId(int? kindId) =>
        kindId == 1 ? DossierDigitizationWorkflowTypeId : DossierWorkflowTypeId;

    private const string InstanceRunning = "Running";

    private const string TaskPending = "Pending";



    private readonly string _connectionString;



    public DossierEnrichmentRepository(IConfiguration configuration)

    {

        _connectionString = configuration.GetConnectionString("DefaultConnection")

            ?? throw new InvalidOperationException(

                "ConnectionStrings:DefaultConnection chưa được cấu hình cho NotificationService.");

    }



    public Task<DossierEnrichmentData?> GetByIdAsync(string dossierId) =>

        WithConnectionAsync(async connection =>

        {

            const string sql = """
                SELECT
                    d.Id,
                    d.GridTypeId,
                    gt.Name AS GridTypeName,
                    d.InfrastructureId,
                    i.NAME AS InfrastructureName,
                    i.CODE AS InfrastructureCode,
                    i.UNIT_ID AS UnitId,
                    d.DossierSetId,
                    ds.Name AS DossierSetName,
                    d.DossierTypeId,
                    dt.Name AS DossierTypeName,
                    d.FormDataJson,
                    d.STATUS_ID AS StatusId,
                    dstat.CODE AS StatusCode,
                    dstat.NAME AS StatusName,
                    d.WorkflowStatusName,
                    d.WorkflowInstanceId,
                    d.CreatorId,
                    d.CreatorUsername,
                    d.CreatorName,
                    d.CreatedDate,
                    d.ModifiedDate,
                    d.IsDeleted,
                    d.PUBLISHSTATUSID AS PublishStatusId,
                    ps.CODE AS PublishStatusCode,
                    ps.NAME AS PublishStatusName,
                    d.KIND_ID AS KindId,
                    dk.CODE AS KindCode,
                    d.KIND_ID AS KindId,
                    dk.CODE AS KindCode,
                    COALESCE((
                        SELECT MAX(v.VersionNumber)
                        FROM DOSSIER_VERSIONS v
                        WHERE v.DossierId = d.Id
                    ), 0) AS CurrentVersionNumber,
                    COALESCE((
                        SELECT COUNT(1)
                        FROM DOCUMENTS doc
                        WHERE doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
                    ), 0) AS DocumentCount,
                    wta.CURRENT_STEP_ID AS CurrentStepId,
                    wta.CURRENT_ASSIGNEES AS CurrentAssignees,
                    wta.AVAILABLE_ACTIONS AS AvailableActionsJson
                FROM DOSSIERS d
                LEFT JOIN GridTypes gt ON d.GridTypeId = gt.Id
                LEFT JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                LEFT JOIN DOSSIER_SETS ds ON d.DossierSetId = ds.Id
                LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.Id
                LEFT JOIN WORKFLOW_TASKS_ACTIVE wta ON d.Id = wta.DOSSIER_ID
                LEFT JOIN PUBLISH_STATUSES ps ON d.PUBLISHSTATUSID = ps.ID
                LEFT JOIN DOSSIER_KINDS dk ON d.KIND_ID = dk.ID
                LEFT JOIN DOSSIER_STATUSES dstat ON d.STATUS_ID = dstat.ID
                WHERE LOWER(TRIM(d.Id)) = LOWER(TRIM(:DossierId))
                """;



            var data = await connection.QuerySingleOrDefaultAsync<DossierEnrichmentData>(

                sql, new { DossierId = dossierId.Trim() });



            if (data is null)

                return null;



            await EnrichWorkflowFieldsAsync(connection, data);

            await ResolveCurrentHandlerNameAsync(connection, data);

            return data;

        });



    private static async Task EnrichWorkflowFieldsAsync(IDbConnection connection, DossierEnrichmentData data)

    {

        var dossierId = data.Id;



        var workflowTypeId = ResolveWorkflowTypeId(data.KindId);

        const string instanceSql = """

            SELECT wi.Id, wi.Status, wi.CurrentNodeName, wi.CurrentStepOrder

            FROM WORKFLOWINSTANCES wi

            WHERE LOWER(TRIM(wi.TargetEntityId)) = LOWER(TRIM(:DossierId))

              AND wi.WORKFLOW_TYPE_ID = :WorkflowTypeId

            ORDER BY CASE WHEN UPPER(TRIM(wi.Status)) = 'RUNNING' THEN 0 ELSE 1 END, wi.CreatedAt DESC

            FETCH FIRST 1 ROWS ONLY

            """;



        var instance = await connection.QueryFirstOrDefaultAsync<(string Id, string Status, string? CurrentNodeName, int CurrentStepOrder)>(

            instanceSql, new { DossierId = dossierId, WorkflowTypeId = workflowTypeId });



        if (string.IsNullOrWhiteSpace(instance.Id))

        {

            ClearInboxFields(data);

            data.WorkflowParticipantUserIds = EnsureParticipants(data, await LoadParticipantUserIdsAsync(connection, dossierId, workflowTypeId));

            return;

        }



        var instanceId = DossierIndexIdNormalizer.Normalize(instance.Id);

        data.WorkflowInstanceId = instanceId;

        data.WorkflowInstanceStatus = instance.Status?.Trim();



        if (IsRunningStatus(instance.Status))

            await MapRunningInstanceToEsAsync(connection, data, dossierId, instanceId, instance.CurrentNodeName);

        else

            MapClosedInstanceToEs(data, instance.CurrentNodeName);



        await ApplyReturnedToCreatorStepFlagAsync(

            connection, data, instanceId, instance.CurrentStepOrder, instance.Status);



        var participants = await LoadParticipantUserIdsAsync(connection, dossierId, workflowTypeId);

        if (!string.IsNullOrWhiteSpace(data.PendingAssigneeUserId))

        {

            var normalized = DossierIndexIdNormalizer.Normalize(data.PendingAssigneeUserId);

            if (!participants.Contains(normalized, StringComparer.Ordinal))

                participants.Add(normalized);

        }



        data.WorkflowParticipantUserIds = EnsureParticipants(data, participants);

    }



    private static async Task MapRunningInstanceToEsAsync(

        IDbConnection connection,

        DossierEnrichmentData data,

        string dossierId,

        string instanceId,

        string? currentNodeName)

    {

        const string pendingTaskSql = """

            SELECT wt.AssignedRole, wt.AssigneeUserId, wt.StepName, ws.AllowEdit

            FROM WORKFLOWTASKS wt

            LEFT JOIN WORKFLOWINSTANCES wi ON LOWER(TRIM(wt.WorkflowInstanceId)) = LOWER(TRIM(wi.Id))

            LEFT JOIN WORKFLOWSTEPS ws ON ws.WorkflowDefinitionId = wi.WorkflowDefinitionId

                AND (

                    LOWER(TRIM(ws.Id)) = LOWER(TRIM(wt.StepId))

                    OR LOWER(ws.StepName) = LOWER(wt.StepName)

                )

            WHERE LOWER(TRIM(wt.WorkflowInstanceId)) = LOWER(TRIM(:InstanceId))

              AND wt.Status = 'Pending'

            ORDER BY wt.CreatedAt ASC

            FETCH FIRST 1 ROWS ONLY

            """;



        var pending = await connection.QueryFirstOrDefaultAsync<(string? AssignedRole, string? AssigneeUserId, string? StepName, int? AllowEdit)>(

            pendingTaskSql, new { InstanceId = instanceId });



        data.PendingAssignedRoles = string.IsNullOrWhiteSpace(pending.AssignedRole)

            ? new List<string>()

            : new List<string> { pending.AssignedRole!.Trim() };



        data.PendingAssigneeUserId = string.IsNullOrWhiteSpace(pending.AssigneeUserId)

            ? null

            : DossierIndexIdNormalizer.Normalize(pending.AssigneeUserId);



        data.CurrentStepAllowEdit = pending.AllowEdit is 1;



        if (string.IsNullOrWhiteSpace(data.PendingAssigneeUserId) && !string.IsNullOrWhiteSpace(pending.StepName))

        {

            data.PendingAssigneeUserId = await ResolvePriorStepAssigneeAsync(

                connection, instanceId, pending.StepName);

        }



        if (string.IsNullOrWhiteSpace(data.PendingAssigneeUserId) && data.CurrentStepAllowEdit)

        {

            data.PendingAssigneeUserId = await ResolveSubmitterUserIdAsync(connection, instanceId)

                ?? DossierIndexIdNormalizer.NormalizeOrNull(data.CreatorId);

        }

        if (!string.IsNullOrWhiteSpace(data.PendingAssigneeUserId))
        {
            data.PendingAssignedRoles = new List<string>();
        }

        data.WorkflowStatusName = !string.IsNullOrWhiteSpace(pending.StepName)

            ? pending.StepName

            : ResolveDisplayStepName(data.WorkflowStatusName, currentNodeName);

    }



    /// <summary>Task Pending chưa gán — lấy assignee từ task Completed/Returned cùng bước (chu kỳ WF trước).</summary>

    private static async Task<string?> ResolvePriorStepAssigneeAsync(

        IDbConnection connection,

        string instanceId,

        string stepName)

    {

        const string sql = """

            SELECT wt.AssigneeUserId

            FROM WORKFLOWTASKS wt

            WHERE LOWER(TRIM(wt.WorkflowInstanceId)) = LOWER(TRIM(:InstanceId))

              AND wt.Status <> 'Pending'

              AND wt.AssigneeUserId IS NOT NULL

              AND LOWER(wt.StepName) = LOWER(:StepName)

            ORDER BY wt.CreatedAt DESC

            FETCH FIRST 1 ROWS ONLY

            """;



        var assignee = await connection.QueryFirstOrDefaultAsync<string>(

            sql, new { InstanceId = instanceId, StepName = stepName });



        return string.IsNullOrWhiteSpace(assignee)

            ? null

            : DossierIndexIdNormalizer.Normalize(assignee);

    }



    private static async Task<string?> ResolveSubmitterUserIdAsync(IDbConnection connection, string instanceId)

    {

        const string sql = """

            SELECT h.ActionByUserId

            FROM WORKFLOWHISTORY h

            WHERE LOWER(TRIM(h.WorkflowInstanceId)) = LOWER(TRIM(:InstanceId))

              AND UPPER(h.""ACTION"") = 'SUBMIT'

              AND h.ActionByUserId IS NOT NULL

            ORDER BY h.ActionDate ASC

            FETCH FIRST 1 ROWS ONLY

            """;



        var submitter = await connection.QueryFirstOrDefaultAsync<string>(sql, new { InstanceId = instanceId });

        return string.IsNullOrWhiteSpace(submitter)

            ? null

            : DossierIndexIdNormalizer.Normalize(submitter);

    }



    /// <summary>
    /// Tab Trả lại: WF đang Running, hành động gần nhất Reject, quay về bước đầu (người tạo).
    /// </summary>
    private static async Task ApplyReturnedToCreatorStepFlagAsync(
        IDbConnection connection,
        DossierEnrichmentData data,
        string instanceId,
        int currentStepOrder,
        string? instanceStatus)
    {
        data.CurrentStepOrder = currentStepOrder;
        data.WorkflowLastAction = await LoadLastHistoryActionAsync(connection, instanceId);
        data.IsReturnedToCreatorStep = false;

        if (!IsRunningStatus(instanceStatus))
            return;

        if (!string.Equals(data.WorkflowLastAction, "Reject", StringComparison.OrdinalIgnoreCase))
            return;

        var firstStepOrder = await LoadFirstStepOrderAsync(connection, instanceId);
        data.IsReturnedToCreatorStep = firstStepOrder is null
            ? currentStepOrder <= 1
            : currentStepOrder == firstStepOrder.Value;
    }

    private static async Task<string?> LoadLastHistoryActionAsync(IDbConnection connection, string instanceId)
    {
        const string sql = """
            SELECT h."ACTION" AS ActionName
            FROM WORKFLOWHISTORY h
            WHERE LOWER(TRIM(h.WorkflowInstanceId)) = LOWER(TRIM(:InstanceId))
            ORDER BY h.ActionDate DESC
            FETCH FIRST 1 ROWS ONLY
            """;

        return await connection.QueryFirstOrDefaultAsync<string>(sql, new { InstanceId = instanceId });
    }

    private static async Task<int?> LoadFirstStepOrderAsync(IDbConnection connection, string instanceId)
    {
        const string sql = """
            SELECT MIN(ws."Order") AS FirstStepOrder
            FROM WORKFLOWSTEPS ws
            INNER JOIN WORKFLOWINSTANCES wi
                ON wi.WorkflowDefinitionId = ws.WorkflowDefinitionId
            WHERE LOWER(TRIM(wi.Id)) = LOWER(TRIM(:InstanceId))
            """;

        return await connection.QueryFirstOrDefaultAsync<int?>(sql, new { InstanceId = instanceId });
    }



    private static void MapClosedInstanceToEs(DossierEnrichmentData data, string? currentNodeName)

    {

        ClearInboxFields(data);

        if (!string.IsNullOrWhiteSpace(currentNodeName) && !IsTechnicalWorkflowLabel(currentNodeName))

            data.WorkflowStatusName = currentNodeName;

    }



    private static void ClearInboxFields(DossierEnrichmentData data)

    {

        data.PendingAssignedRoles = new List<string>();

        data.PendingAssigneeUserId = null;

        data.CurrentStepAllowEdit = false;

        data.CurrentStepId = null;

        data.CurrentAssignees = null;

        data.AvailableActionsJson = null;

    }



    private static List<string> EnsureParticipants(DossierEnrichmentData data, List<string> participants)

    {

        if (participants.Count > 0)

            return participants;



        if (!string.IsNullOrWhiteSpace(data.CreatorId))

        {

            participants.Add(DossierIndexIdNormalizer.Normalize(data.CreatorId));

            return participants;

        }



        if (!string.IsNullOrWhiteSpace(data.PendingAssigneeUserId))

            participants.Add(data.PendingAssigneeUserId);



        return participants;

    }



    private static async Task<List<string>> LoadParticipantUserIdsAsync(IDbConnection connection, string dossierId, int workflowTypeId)

    {

        const string sql = """

            SELECT DISTINCT participant_id AS UserId FROM (

                SELECT wt.AssigneeUserId AS participant_id

                FROM WORKFLOWTASKS wt

                INNER JOIN WORKFLOWINSTANCES wi ON LOWER(TRIM(wt.WorkflowInstanceId)) = LOWER(TRIM(wi.Id))

                WHERE LOWER(TRIM(wi.TargetEntityId)) = LOWER(TRIM(:DossierId))

                  AND wi.WORKFLOW_TYPE_ID = :WorkflowTypeId

                  AND wt.AssigneeUserId IS NOT NULL

                UNION

                SELECT h.ActionByUserId AS participant_id

                FROM WORKFLOWHISTORY h

                INNER JOIN WORKFLOWINSTANCES wi ON LOWER(TRIM(h.WorkflowInstanceId)) = LOWER(TRIM(wi.Id))

                WHERE LOWER(TRIM(wi.TargetEntityId)) = LOWER(TRIM(:DossierId))

                  AND wi.WORKFLOW_TYPE_ID = :WorkflowTypeId

                  AND h.ActionByUserId IS NOT NULL

                UNION

                SELECT d.CreatorId AS participant_id

                FROM DOSSIERS d

                WHERE LOWER(TRIM(d.Id)) = LOWER(TRIM(:DossierId))

                  AND d.CreatorId IS NOT NULL

            )

            """;



        var ids = (await connection.QueryAsync<string>(

            sql,

            new { DossierId = dossierId, WorkflowTypeId = workflowTypeId })).ToList();

        return ids

            .Where(id => !string.IsNullOrWhiteSpace(id))

            .Select(id => DossierIndexIdNormalizer.Normalize(id!))

            .Distinct(StringComparer.Ordinal)

            .ToList();

    }



    public Task<IEnumerable<string>> GetAllIdsAsync() =>

        WithConnectionAsync(connection =>

            connection.QueryAsync<string>("SELECT Id FROM DOSSIERS WHERE IsDeleted = 0"));



    public Task<IEnumerable<string>> GetSoftDeletedIdsAsync() =>

        WithConnectionAsync(connection =>

            connection.QueryAsync<string>("SELECT Id FROM DOSSIERS WHERE IsDeleted = 1"));



    public Task<IEnumerable<BhsCatalogDefinition>> GetBhsCatalogDefinitionsAsync() =>

        WithConnectionAsync(async connection =>

        {

            const string sql = """

                SELECT c.Code, c.Name, c.Priority

                FROM CATALOG c

                INNER JOIN CATALOG_TYPE ct ON c.CatalogTypeId = ct.Id

                WHERE ct.Code = 'BHS'

                  AND c.IsDeleted = 0

                  AND ct.IsDeleted = 0

                ORDER BY c.Priority ASC, c.Name ASC

                """;



            return await connection.QueryAsync<BhsCatalogDefinition>(sql);

        });



    public Task<IEnumerable<DossierEquipmentEnrichment>> GetEquipmentsAsync(string dossierId) =>

        WithConnectionAsync(async connection =>

        {

            const string sql = """

                SELECT

                    de.EquipmentId,

                    e.CODE AS EquipmentCode,

                    e.NAME AS EquipmentName,

                    e.SerialNumber

                FROM DOSSIER_EQUIPMENTS de

                INNER JOIN Equipments e ON de.EquipmentId = e.Id

                WHERE LOWER(de.DossierId) = LOWER(:DossierId)

                """;



            return await connection.QueryAsync<DossierEquipmentEnrichment>(

                sql, new { DossierId = dossierId.Trim() });

        });



    private async Task<T> WithConnectionAsync<T>(Func<IDbConnection, Task<T>> action)

    {

        await using var connection = new OracleConnection(_connectionString);

        if (connection.State != ConnectionState.Open)

            await connection.OpenAsync();

        return await action(connection);

    }



    private static bool IsRunningStatus(string? status) =>

        string.Equals(status?.Trim(), InstanceRunning, StringComparison.OrdinalIgnoreCase);



    private static async Task ResolveCurrentHandlerNameAsync(IDbConnection connection, DossierEnrichmentData data)

    {

        if (!string.IsNullOrWhiteSpace(data.PendingAssigneeUserId))

        {

            data.CurrentHandlerName = await LookupUserFullNameAsync(connection, data.PendingAssigneeUserId);

            return;

        }



        if (string.IsNullOrWhiteSpace(data.WorkflowInstanceId) && (data.StatusId == 1 || data.StatusId == 2))

        {

            data.CurrentHandlerName = string.IsNullOrWhiteSpace(data.CreatorName)

                ? await LookupUserFullNameAsync(connection, data.CreatorId)

                : data.CreatorName;

            return;

        }



        data.CurrentHandlerName = null;

    }



    private static async Task<string?> LookupUserFullNameAsync(IDbConnection connection, string? userId)

    {

        if (string.IsNullOrWhiteSpace(userId))

            return null;



        const string sql = """

            SELECT FullName

            FROM APP_USER

            WHERE LOWER(TRIM(Id)) = LOWER(TRIM(:UserId))

            FETCH FIRST 1 ROWS ONLY

            """;



        return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { UserId = userId.Trim() });

    }



    private static string? ResolveDisplayStepName(string? dossierStepName, string? currentNodeName)

    {

        if (!IsWeakDisplayLabel(currentNodeName))

            return currentNodeName;

        if (!IsWeakDisplayLabel(dossierStepName))

            return dossierStepName;

        return currentNodeName ?? dossierStepName;

    }



    private static bool IsWeakDisplayLabel(string? value)

    {

        if (string.IsNullOrWhiteSpace(value))

            return true;



        if (IsTechnicalWorkflowLabel(value))

            return true;



        var trimmed = value.Trim();

        if (trimmed.All(char.IsDigit))

            return true;



        return trimmed.StartsWith("Activity_", StringComparison.OrdinalIgnoreCase);

    }



    private static bool IsTechnicalWorkflowLabel(string? value)

    {

        if (string.IsNullOrWhiteSpace(value))

            return false;



        return value.Equals("Running", StringComparison.OrdinalIgnoreCase)

            || value.Equals("Completed", StringComparison.OrdinalIgnoreCase)

            || value.Equals("Terminated", StringComparison.OrdinalIgnoreCase);

    }

}


