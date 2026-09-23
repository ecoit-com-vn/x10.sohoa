using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

using Infrastructure = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;
using OrganizationDto = EvnHanoi.EquipmentService.Core.Entities.OrganizationDto;

public class InfrastructureRepository : IInfrastructureRepository
{
    private readonly IDbConnection _connection;

    // Precomposed lowercase Vietnamese characters mapped to their unaccented base letter.
    // Used to fold both the search keyword and the compared SQL columns so that typing
    // "tram" (no diacritics) still matches "trạm".
    private static readonly (string From, string To)[] VietnameseDiacriticsMap = new[]
    {
        ("à","a"),("á","a"),("ả","a"),("ã","a"),("ạ","a"),
        ("ă","a"),("ắ","a"),("ằ","a"),("ẳ","a"),("ẵ","a"),("ặ","a"),
        ("â","a"),("ấ","a"),("ầ","a"),("ẩ","a"),("ẫ","a"),("ậ","a"),
        ("è","e"),("é","e"),("ẻ","e"),("ẽ","e"),("ẹ","e"),
        ("ê","e"),("ế","e"),("ề","e"),("ể","e"),("ễ","e"),("ệ","e"),
        ("ì","i"),("í","i"),("ỉ","i"),("ĩ","i"),("ị","i"),
        ("ò","o"),("ó","o"),("ỏ","o"),("õ","o"),("ọ","o"),
        ("ô","o"),("ố","o"),("ồ","o"),("ổ","o"),("ỗ","o"),("ộ","o"),
        ("ơ","o"),("ớ","o"),("ờ","o"),("ở","o"),("ỡ","o"),("ợ","o"),
        ("ù","u"),("ú","u"),("ủ","u"),("ũ","u"),("ụ","u"),
        ("ư","u"),("ứ","u"),("ừ","u"),("ử","u"),("ữ","u"),("ự","u"),
        ("ỳ","y"),("ý","y"),("ỷ","y"),("ỹ","y"),("ỵ","y"),
        ("đ","d")
    };

    private static string BuildUnaccentSql(string columnExpr)
    {
        var result = columnExpr;
        foreach (var (from, to) in VietnameseDiacriticsMap)
        {
            result = $"REPLACE({result}, '{from}', '{to}')";
        }
        return result;
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = text.Replace('đ', 'd').Replace('Đ', 'D');
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public InfrastructureRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<Infrastructure?> GetByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"SELECT i.{nameof(Infrastructure.Id)},
                            i.{nameof(Infrastructure.Code)},
                            i.{nameof(Infrastructure.Name)},
                            i.{nameof(Infrastructure.Address)},
                            i.INFRA_TYPE_ID as {nameof(Infrastructure.InfraTypeId)},
                            i.UNIT_ID as {nameof(Infrastructure.UnitId)},
                            i.GRIDTYPEID as {nameof(Infrastructure.GridTypeId)},
                            i.OPERATION_DATE as {nameof(Infrastructure.OperationDate)},
                            i.IS_ACTIVE as {nameof(Infrastructure.IsActive)},
                            i.{nameof(Infrastructure.CreatedBy)},
                            i.{nameof(Infrastructure.CreatedDate)},
                            i.{nameof(Infrastructure.ModifiedBy)},
                            i.{nameof(Infrastructure.ModifiedDate)},
                            i.PARENT_ID as {nameof(Infrastructure.ParentId)},
                            p.NAME as {nameof(Infrastructure.ParentName)},
                            p.CODE as {nameof(Infrastructure.ParentCode)},
                            it.NAME as {nameof(Infrastructure.InfraTypeName)},
                            u.NAME as {nameof(Infrastructure.UnitName)},
                            u.Id as OrgId,
                            u.Code as OrgCode,
                            u.Name as OrgName,
                            (SELECT COUNT(1) FROM EQUIPMENTS eq WHERE eq.INFRASTRUCTURE_ID = i.{nameof(Infrastructure.Id)} AND eq.IsDeleted = 0 AND {EquipmentSqlFilters.NotTransferredAway("eq")}) AS {nameof(Infrastructure.EquipmentCount)}
                     FROM INFRASTRUCTURE i
                     LEFT JOIN INFRASTRUCTURE p ON i.PARENT_ID = p.ID
                     LEFT JOIN INFRASTRUCTURE_TYPE it ON i.INFRA_TYPE_ID = it.ID
                     LEFT JOIN ORGANIZATION_UNIT u ON i.UNIT_ID = u.Id
                     WHERE i.{nameof(Infrastructure.Id)} = :Id AND i.{nameof(Infrastructure.IsDeleted)} = 0";

        var result = await _connection.QueryAsync<Infrastructure, OrganizationDto, Infrastructure>(
            sql, 
            (infra, org) => {
                if (org != null && org.Id > 0) {
                    infra.Organization = org;
                }
                return infra;
            },
            new { Id = id.ToString() },
            splitOn: "OrgId"
        );
        return result.FirstOrDefault();
    }

    public async Task<Infrastructure?> GetByCodeAsync(string code)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"SELECT i.{nameof(Infrastructure.Id)},
                            i.{nameof(Infrastructure.Code)},
                            i.{nameof(Infrastructure.Name)},
                            i.{nameof(Infrastructure.Address)},
                            i.INFRA_TYPE_ID as {nameof(Infrastructure.InfraTypeId)},
                            i.UNIT_ID as {nameof(Infrastructure.UnitId)},
                            i.GRIDTYPEID as {nameof(Infrastructure.GridTypeId)},
                            i.OPERATION_DATE as {nameof(Infrastructure.OperationDate)},
                            i.IS_ACTIVE as {nameof(Infrastructure.IsActive)},
                            i.{nameof(Infrastructure.CreatedBy)},
                            i.{nameof(Infrastructure.CreatedDate)},
                            i.{nameof(Infrastructure.ModifiedBy)},
                            i.{nameof(Infrastructure.ModifiedDate)},
                            i.PARENT_ID as {nameof(Infrastructure.ParentId)},
                            p.NAME as {nameof(Infrastructure.ParentName)},
                            p.CODE as {nameof(Infrastructure.ParentCode)},
                            it.NAME as {nameof(Infrastructure.InfraTypeName)},
                            u.NAME as {nameof(Infrastructure.UnitName)},
                            u.Id as OrgId,
                            u.Code as OrgCode,
                            u.Name as OrgName,
                            (SELECT COUNT(1) FROM EQUIPMENTS eq WHERE eq.INFRASTRUCTURE_ID = i.{nameof(Infrastructure.Id)} AND eq.IsDeleted = 0 AND {EquipmentSqlFilters.NotTransferredAway("eq")}) AS {nameof(Infrastructure.EquipmentCount)}
                     FROM INFRASTRUCTURE i
                     LEFT JOIN INFRASTRUCTURE p ON i.PARENT_ID = p.ID
                     LEFT JOIN INFRASTRUCTURE_TYPE it ON i.INFRA_TYPE_ID = it.ID
                     LEFT JOIN ORGANIZATION_UNIT u ON i.UNIT_ID = u.Id
                     WHERE LOWER(i.{nameof(Infrastructure.Code)}) = :Code AND i.{nameof(Infrastructure.IsDeleted)} = 0";

        var result = await _connection.QueryAsync<Infrastructure, OrganizationDto, Infrastructure>(
            sql, 
            (infra, org) => {
                if (org != null && org.Id > 0) {
                    infra.Organization = org;
                }
                return infra;
            },
            new { Code = code.ToLower().Trim() },
            splitOn: "OrgId"
        );
        return result.FirstOrDefault();
    }

    public async Task<(IEnumerable<Infrastructure> Items, int TotalCount)> GetPagedAsync(
        int page, 
        int pageSize, 
        int infraTypeId, 
        string? keyword, 
        int? status,
        IEnumerable<long>? unitIds = null,
        long? unitId = null,
        int? gridTypeId = null,
        DateTime? fromOperationDate = null,
        DateTime? toOperationDate = null,
        bool rootOnly = false)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sqlBase = $@"FROM INFRASTRUCTURE i
                          LEFT JOIN INFRASTRUCTURE p ON i.PARENT_ID = p.ID
                          LEFT JOIN INFRASTRUCTURE_TYPE it ON i.INFRA_TYPE_ID = it.ID
                          LEFT JOIN ORGANIZATION_UNIT u ON i.UNIT_ID = u.Id
                          WHERE i.{nameof(Infrastructure.IsDeleted)} = 0 AND i.INFRA_TYPE_ID = :InfraTypeId";

        var parameters = new DynamicParameters();
        parameters.Add("InfraTypeId", infraTypeId);

        // Chỉ lấy đường dây CẤP 1 (đường trục, không có cha) — dùng cho màn Danh mục đường dây: phân
        // trang toàn bộ 14000+ dòng (cha lẫn con lẫn lộn) không đảm bảo 1 cha và các con của nó luôn rơi
        // cùng 1 trang, nên FE không thể tự gom cây từ 1 trang riêng lẻ. Cha hiển thị trước, nhánh con
        // tải lười riêng qua GetChildLinesAsync khi người dùng bấm mở rộng.
        if (rootOnly)
        {
            sqlBase += " AND i.PARENT_ID IS NULL";
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            var codeExpr = BuildUnaccentSql($"LOWER(i.{nameof(Infrastructure.Code)})");
            var nameExpr = BuildUnaccentSql($"LOWER(i.{nameof(Infrastructure.Name)})");
            sqlBase += $" AND ({codeExpr} LIKE :Keyword OR {nameExpr} LIKE :Keyword)";
            parameters.Add("Keyword", $"%{RemoveDiacritics(keyword.ToLower().Trim())}%");
        }

        if (status.HasValue)
        {
            sqlBase += $" AND i.IS_ACTIVE = :Status";
            parameters.Add("Status", status.Value);
        }

        if (gridTypeId.HasValue)
        {
            sqlBase += $" AND i.GRIDTYPEID = :GridTypeId";
            parameters.Add("GridTypeId", gridTypeId.Value);
        }

        if (fromOperationDate.HasValue)
        {
            sqlBase += " AND i.OPERATION_DATE >= :FromOperationDate";
            parameters.Add("FromOperationDate", fromOperationDate.Value.Date);
        }

        if (toOperationDate.HasValue)
        {
            sqlBase += " AND i.OPERATION_DATE < :ToOperationDateExclusive";
            parameters.Add("ToOperationDateExclusive", toOperationDate.Value.Date.AddDays(1));
        }

        if (unitId.HasValue && unitId.Value > 0)
        {
            sqlBase += $" AND i.UNIT_ID = :UnitId";
            parameters.Add("UnitId", unitId.Value);
        }
        else if (unitIds != null && unitIds.Any())
        {
            sqlBase += $" AND i.UNIT_ID IN :UnitIds";
            parameters.Add("UnitIds", unitIds);
        }

        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var selectSql = $@"SELECT i.{nameof(Infrastructure.Id)},
                           i.{nameof(Infrastructure.Code)},
                           i.{nameof(Infrastructure.Name)},
                           i.{nameof(Infrastructure.Address)},
                           i.INFRA_TYPE_ID AS {nameof(Infrastructure.InfraTypeId)},
                           i.UNIT_ID AS {nameof(Infrastructure.UnitId)},
                           i.GRIDTYPEID AS {nameof(Infrastructure.GridTypeId)},
                           i.OPERATION_DATE AS {nameof(Infrastructure.OperationDate)},
                           i.IS_ACTIVE AS {nameof(Infrastructure.IsActive)},
                           i.{nameof(Infrastructure.CreatedBy)},
                           i.{nameof(Infrastructure.CreatedDate)},
                           i.{nameof(Infrastructure.ModifiedBy)},
                           i.{nameof(Infrastructure.ModifiedDate)},
                           i.{nameof(Infrastructure.IsDeleted)},
                           i.PARENT_ID AS {nameof(Infrastructure.ParentId)},
                           p.NAME AS {nameof(Infrastructure.ParentName)},
                           p.CODE AS {nameof(Infrastructure.ParentCode)},
                           it.NAME AS {nameof(Infrastructure.InfraTypeName)},
                           u.NAME AS {nameof(Infrastructure.UnitName)},
                           u.Id AS OrgId,
                           u.Code AS OrgCode,
                           u.Name AS OrgName,
                           (SELECT COUNT(1)
                              FROM EQUIPMENTS eq
                             WHERE eq.INFRASTRUCTURE_ID = i.{nameof(Infrastructure.Id)}
                               AND eq.IsDeleted = 0 AND {EquipmentSqlFilters.NotTransferredAway("eq")}) AS {nameof(Infrastructure.EquipmentCount)},
                           (SELECT COUNT(1)
                              FROM INFRASTRUCTURE c
                             WHERE c.PARENT_ID = i.{nameof(Infrastructure.Id)}
                               AND c.{nameof(Infrastructure.IsDeleted)} = 0) AS {nameof(Infrastructure.ChildLineCount)}
                   {sqlBase}
                    ORDER BY i.IS_ACTIVE DESC,
                             COALESCE(p.CODE, i.{nameof(Infrastructure.Code)}) ASC,
                             CASE WHEN i.PARENT_ID IS NULL THEN 0 ELSE 1 END ASC,
                             i.{nameof(Infrastructure.Code)} ASC,
                             i.{nameof(Infrastructure.CreatedDate)} DESC
                    OFFSET :Offset ROWS
                    FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var items = await _connection.QueryAsync<Infrastructure, OrganizationDto, Infrastructure>(
            selectSql,
            (infra, org) => {
                if (org != null && org.Id > 0) {
                    infra.Organization = org;
                }
                return infra;
            },
            parameters,
            splitOn: "OrgId"
        );

        return (items, totalCount);
    }

    public async Task<IEnumerable<Infrastructure>> GetChildLinesAsync(Guid parentId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"SELECT i.{nameof(Infrastructure.Id)},
                            i.{nameof(Infrastructure.Code)},
                            i.{nameof(Infrastructure.Name)},
                            i.{nameof(Infrastructure.Address)},
                            i.INFRA_TYPE_ID AS {nameof(Infrastructure.InfraTypeId)},
                            i.UNIT_ID AS {nameof(Infrastructure.UnitId)},
                            i.GRIDTYPEID AS {nameof(Infrastructure.GridTypeId)},
                            i.OPERATION_DATE AS {nameof(Infrastructure.OperationDate)},
                            i.IS_ACTIVE AS {nameof(Infrastructure.IsActive)},
                            i.PARENT_ID AS {nameof(Infrastructure.ParentId)},
                            u.NAME AS {nameof(Infrastructure.UnitName)},
                            u.Id AS OrgId,
                            u.Code AS OrgCode,
                            u.Name AS OrgName,
                            (SELECT COUNT(1)
                               FROM EQUIPMENTS eq
                              WHERE eq.INFRASTRUCTURE_ID = i.{nameof(Infrastructure.Id)}
                                AND eq.IsDeleted = 0 AND {EquipmentSqlFilters.NotTransferredAway("eq")}) AS {nameof(Infrastructure.EquipmentCount)},
                            (SELECT COUNT(1)
                               FROM INFRASTRUCTURE c
                              WHERE c.PARENT_ID = i.{nameof(Infrastructure.Id)}
                                AND c.{nameof(Infrastructure.IsDeleted)} = 0) AS {nameof(Infrastructure.ChildLineCount)}
                     FROM INFRASTRUCTURE i
                     LEFT JOIN ORGANIZATION_UNIT u ON i.UNIT_ID = u.Id
                     WHERE i.PARENT_ID = :ParentId AND i.{nameof(Infrastructure.IsDeleted)} = 0
                     ORDER BY i.{nameof(Infrastructure.Code)} ASC";

        return await _connection.QueryAsync<Infrastructure, OrganizationDto, Infrastructure>(
            sql,
            (infra, org) => {
                if (org != null && org.Id > 0) {
                    infra.Organization = org;
                }
                return infra;
            },
            new { ParentId = parentId.ToString() },
            splitOn: "OrgId"
        );
    }

    public async Task<Guid> CreateAsync(Infrastructure infrastructure)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        if (infrastructure.Id == Guid.Empty)
        {
            infrastructure.Id = Guid.Parse(EvnHanoi.Infrastructure.Database.UuidHelper.NewUuid());
        }

        var sql = $@"INSERT INTO INFRASTRUCTURE (
                        {nameof(Infrastructure.Id)},
                        {nameof(Infrastructure.Code)},
                        {nameof(Infrastructure.Name)},
                        {nameof(Infrastructure.Address)},
                        PARENT_ID,
                        INFRA_TYPE_ID,
                        UNIT_ID,
                        GRIDTYPEID,
                        OPERATION_DATE,
                        IS_ACTIVE,
                        {nameof(Infrastructure.CreatedBy)},
                        {nameof(Infrastructure.CreatedDate)},
                        {nameof(Infrastructure.IsDeleted)}
                    )
                    VALUES (:Id, :Code, :Name, :Address, :ParentId, :InfraTypeId, :UnitId, :GridTypeId, :OperationDate, :IsActive, :CreatedBy, :CreatedDate, :IsDeleted)";

        var param = new
        {
            Id = infrastructure.Id.ToString(),
            infrastructure.Code,
            infrastructure.Name,
            infrastructure.Address,
            ParentId = infrastructure.ParentId.HasValue ? infrastructure.ParentId.Value.ToString() : null,
            infrastructure.InfraTypeId,
            infrastructure.UnitId,
            infrastructure.GridTypeId,
            infrastructure.OperationDate,
            IsActive = infrastructure.IsActive ? 1 : 0,
            infrastructure.CreatedBy,
            infrastructure.CreatedDate,
            IsDeleted = infrastructure.IsDeleted ? 1 : 0
        };

        await _connection.ExecuteAsync(sql, param);
        return infrastructure.Id;
    }

    public async Task<bool> UpdateAsync(Infrastructure infrastructure)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"UPDATE INFRASTRUCTURE
                    SET {nameof(Infrastructure.Code)} = :Code,
                        {nameof(Infrastructure.Name)} = :Name,
                        {nameof(Infrastructure.Address)} = :Address,
                        PARENT_ID = :ParentId,
                        INFRA_TYPE_ID = :InfraTypeId,
                        UNIT_ID = :UnitId,
                        GRIDTYPEID = :GridTypeId,
                        OPERATION_DATE = :OperationDate,
                        IS_ACTIVE = :IsActive,
                        {nameof(Infrastructure.ModifiedBy)} = :ModifiedBy,
                        {nameof(Infrastructure.ModifiedDate)} = :ModifiedDate
                    WHERE {nameof(Infrastructure.Id)} = :Id AND {nameof(Infrastructure.IsDeleted)} = 0";

        var param = new
        {
            Id = infrastructure.Id.ToString(),
            infrastructure.Code,
            infrastructure.Name,
            infrastructure.Address,
            ParentId = infrastructure.ParentId.HasValue ? infrastructure.ParentId.Value.ToString() : null,
            infrastructure.InfraTypeId,
            infrastructure.UnitId,
            infrastructure.GridTypeId,
            infrastructure.OperationDate,
            IsActive = infrastructure.IsActive ? 1 : 0,
            infrastructure.ModifiedBy,
            infrastructure.ModifiedDate
        };

        var affected = await _connection.ExecuteAsync(sql, param);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"UPDATE INFRASTRUCTURE
                    SET {nameof(Infrastructure.IsDeleted)} = 1
                    WHERE {nameof(Infrastructure.Id)} = :Id";

        var affected = await _connection.ExecuteAsync(sql, new { Id = id.ToString() });
        return affected > 0;
    }

    private class InfraCompareRow
    {
        public string Id { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public long? UnitId { get; set; }
        public DateTime? OperationDate { get; set; }
        public int? GridTypeId { get; set; }
        public string? ParentId { get; set; }
    }

    public async Task<(Guid Id, bool WasCreated, bool HasChanged, bool ParentUnresolved)> UpsertFromPmisAsync(
        int infraTypeId, string pmisCode, string code, string name, string? address, string? unitCode, DateTime? operationDate,
        int? gridTypeId = null, string? parentPmisCode = null)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        // Mã đơn vị PMIS (vd. "HN0200") KHÔNG khớp trực tiếp ORGANIZATION_UNIT.Code (vd. "HN02") —
        // xác nhận bằng dữ liệu thật, xem PMIS_UNIT_CODE_MAPPING (Migration0051). Không fallback so
        // khớp trực tiếp Code = Code để tránh khớp nhầm ngẫu nhiên.
        long? unitId = null;
        if (!string.IsNullOrWhiteSpace(unitCode))
        {
            unitId = await _connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT UnitId FROM PMIS_UNIT_CODE_MAPPING WHERE PmisUnitCode = :Code AND IsDeleted = 0", new { Code = unitCode });
        }

        // Quan hệ cha-con giữa các Đường dây: SyncService giờ chỉ gửi THẲNG mã PMIS của cha (field "maCha"
        // PMIS bổ sung 2026-09-23, xem UpsertInfrastructureFromPmisRequest.ParentPmisCode) — tự SELECT
        // ngay tại đây, giống hệt cách ParentPmisCode được resolve cho Thiết bị (xem
        // EquipmentRepository.UpsertFromPmisAsync). Trước 2026-09-23 phải tự tra theo TÊN qua 1 danh mục
        // tải sẵn cả lượt đồng bộ (không có mã cha thật) — không còn cần thiết nữa.
        // Chỉ áp dụng cho Đường dây (infraTypeId = 2) — Trạm biến áp không có khái niệm này, không đụng PARENT_ID.
        var isLine = infraTypeId == 2;

        // PMIS đôi khi tự trỏ maCha về chính mã của dòng đó (lỗi nhập liệu nguồn, cùng lớp lỗi với
        // PMIS_CODE lệch khoảng trắng/hoa-thường đã gặp thật — xem Migration0060) — nếu không chặn, dòng
        // này sẽ được gán làm CHA CỦA CHÍNH NÓ (PARENT_ID = Id của chính nó), khiến GetChildLinesAsync trả
        // nó về như con của chính nó và nó biến mất khỏi danh sách gốc (rootOnly=true lọc PARENT_ID IS
        // NULL). Coi trường hợp này như KHÔNG xác định được cha (parentInfrastructureId giữ null) — không
        // tự "sửa" bằng cách bỏ qua field, để ParentUnresolved bên dưới vẫn báo cảnh báo cho admin biết.
        var isSelfReferencingParent = isLine && !string.IsNullOrWhiteSpace(parentPmisCode) &&
            string.Equals(parentPmisCode.Trim(), pmisCode?.Trim(), StringComparison.OrdinalIgnoreCase);

        Guid? parentInfrastructureId = null;
        int? parentGridTypeId = null;
        if (isLine && !isSelfReferencingParent && !string.IsNullOrWhiteSpace(parentPmisCode))
        {
            // Không tìm thấy (đường trục chưa đồng bộ tới, hoặc chưa tồn tại) → giữ nguyên effectiveParentId
            // cũ bên dưới (không xoá), tự khớp đúng ở lượt đồng bộ kế tiếp — PMIS trả toàn bộ dữ liệu mỗi
            // lượt (không phải delta) nên không mất dấu.
            var parentRow = await InfrastructurePmisLookup.ResolveByPmisCodeAsync(_connection, parentPmisCode);
            if (parentRow != null)
            {
                parentInfrastructureId = Guid.Parse(parentRow.Value.Id);
                parentGridTypeId = parentRow.Value.GridTypeId;
            }
        }

        // true khi parentPmisCode CÓ giá trị nhưng không khớp được (hoặc bị chặn vì tự trỏ về chính nó) —
        // khác với parentPmisCode rỗng (đường trục gốc thật, hợp lệ). Trả lên caller (SyncService) để ghi
        // 1 dòng Warning thấy được trong "Lịch sử đồng bộ" thay vì âm thầm mãi mãi — trước đây có job Quartz
        // riêng (LineParentBackfillJob, đã bỏ 2026-09-23 vì maCha giờ tra được thẳng, tự khớp lại mỗi lượt
        // resync) báo cảnh báo này; giờ tính lại NGAY tại đây, mỗi lần upsert, không cần job riêng.
        var parentUnresolved = isLine && !string.IsNullOrWhiteSpace(parentPmisCode) && parentInfrastructureId == null;

        // Nhánh mượn tạm GridTypeId của cha khi bản thân không có capDienAp riêng (PMIS không trả field
        // này cho phần lớn nhánh, chỉ trục mới luôn có) — KHÔNG ảnh hưởng trục gốc thật (isLine && cha rỗng)
        // hay nhánh đã tự xác định được cấp điện áp riêng (gridTypeId đã có giá trị).
        var borrowedGridTypeId = gridTypeId ?? parentGridTypeId;

        // UPPER(TRIM(...)) ở cả 2 bên: SyncService giờ đã tự Trim() trước khi gửi (xem PmisSyncExecutionService),
        // nhưng vẫn so khớp "lỏng" ở đây để dữ liệu CŨ đã lưu lệch chuẩn từ trước tự khớp lại đúng ngay ở lần
        // sync tới — tránh lặp lại lỗi thật đã gặp: PMIS_CODE lệch khoảng trắng/hoa-thường khiến hệ thống
        // không tìm thấy trạm cũ, tự tạo thêm 1 dòng INFRASTRUCTURE trùng cho CÙNG 1 trạm thật, kéo theo
        // TOÀN BỘ thiết bị của trạm bị coi là "chuyển sang trạm mới" ở lượt kế tiếp (xem Migration0060).
        var existing = await _connection.QuerySingleOrDefaultAsync<InfraCompareRow>(
            $@"SELECT {nameof(Infrastructure.Id)} AS Id, {nameof(Infrastructure.Code)} AS Code, {nameof(Infrastructure.Name)} AS Name,
                      {nameof(Infrastructure.Address)} AS Address, UNIT_ID AS UnitId, OPERATION_DATE AS OperationDate, GRIDTYPEID AS GridTypeId,
                      PARENT_ID AS ParentId
               FROM INFRASTRUCTURE
               WHERE UPPER(TRIM(PMIS_CODE)) = UPPER(TRIM(:PmisCode)) AND {nameof(Infrastructure.IsDeleted)} = 0",
            new { PmisCode = pmisCode });

        if (existing != null)
        {
            var effectiveGridTypeId = borrowedGridTypeId ?? existing.GridTypeId; // giữ đúng ngữ nghĩa COALESCE của câu UPDATE cũ

            // Với Đường dây: parentPmisCode rỗng nghĩa là PMIS đổi thành đường gốc thật sự → xoá hẳn
            // PARENT_ID. Còn có parentPmisCode nhưng KHÔNG khớp được INFRASTRUCTURE nào (đường trục chưa
            // đồng bộ tới trong lượt này) là tình huống tạm thời — giữ nguyên PARENT_ID cũ, tự sửa đúng ở
            // lượt kế tiếp. Trạm biến áp không có khái niệm này, giữ nguyên.
            string? effectiveParentId = !isLine
                ? existing.ParentId
                : string.IsNullOrWhiteSpace(parentPmisCode) ? null : parentInfrastructureId?.ToString() ?? existing.ParentId;

            // Chỉ update khi có ít nhất 1 trường thay đổi thật — tránh ghi đè/tăng ModifiedDate vô ích
            // mỗi lần resync khi PMIS trả về y hệt dữ liệu đã lưu.
            var hasChanged =
                existing.Code != code ||
                existing.Name != name ||
                existing.Address != address ||
                existing.UnitId != unitId ||
                existing.OperationDate != operationDate ||
                existing.GridTypeId != effectiveGridTypeId ||
                existing.ParentId != effectiveParentId;

            if (!hasChanged)
                return (Guid.Parse(existing.Id), false, false, parentUnresolved);

            var updateSql = $@"UPDATE INFRASTRUCTURE
                        SET {nameof(Infrastructure.Code)} = :Code,
                            {nameof(Infrastructure.Name)} = :Name,
                            {nameof(Infrastructure.Address)} = :Address,
                            UNIT_ID = :UnitId,
                            OPERATION_DATE = :OperationDate,
                            GRIDTYPEID = COALESCE(:GridTypeId, GRIDTYPEID),
                            PARENT_ID = :EffectiveParentId,
                            LAST_SYNCED_FROM_PMIS_AT = SYSTIMESTAMP,
                            {nameof(Infrastructure.ModifiedBy)} = :ModifiedBy,
                            {nameof(Infrastructure.ModifiedDate)} = SYSTIMESTAMP
                        WHERE {nameof(Infrastructure.Id)} = :Id";

            await _connection.ExecuteAsync(updateSql, new
            {
                Id = existing.Id,
                Code = code,
                Name = name,
                Address = address,
                UnitId = unitId,
                OperationDate = operationDate,
                GridTypeId = borrowedGridTypeId,
                EffectiveParentId = effectiveParentId,
                ModifiedBy = "PMIS_SYNC"
            });
            return (Guid.Parse(existing.Id), false, true, parentUnresolved);
        }

        var newId = Guid.Parse(EvnHanoi.Infrastructure.Database.UuidHelper.NewUuid());
        var insertSql = $@"INSERT INTO INFRASTRUCTURE (
                        {nameof(Infrastructure.Id)}, {nameof(Infrastructure.Code)}, {nameof(Infrastructure.Name)},
                        {nameof(Infrastructure.Address)}, INFRA_TYPE_ID, UNIT_ID, OPERATION_DATE, GRIDTYPEID, PARENT_ID, IS_ACTIVE,
                        PMIS_CODE, LAST_SYNCED_FROM_PMIS_AT,
                        {nameof(Infrastructure.CreatedBy)}, {nameof(Infrastructure.CreatedDate)}, {nameof(Infrastructure.IsDeleted)}
                    ) VALUES (
                        :Id, :Code, :Name, :Address, :InfraTypeId, :UnitId, :OperationDate, :GridTypeId, :ParentId, 1,
                        :PmisCode, SYSTIMESTAMP, :CreatedBy, SYSTIMESTAMP, 0
                    )";

        try
        {
            await _connection.ExecuteAsync(insertSql, new
            {
                Id = newId.ToString(),
                Code = code,
                Name = name,
                Address = address,
                InfraTypeId = infraTypeId,
                UnitId = unitId,
                OperationDate = operationDate,
                GridTypeId = borrowedGridTypeId,
                ParentId = isLine ? parentInfrastructureId?.ToString() : null,
                PmisCode = pmisCode,
                CreatedBy = "PMIS_SYNC"
            });
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
        {
            // Race THẬT giữa 2 lượt sync đồng thời (thủ công + tự động, không có RedLock chung) cùng
            // insert 1 Trạm/Đường dây MỚI với PMIS_CODE trùng nhau (UX_INFRASTRUCTURE_ACTIVE_PMIS_CODE,
            // Migration0060) — method này không có kiểu Fail() riêng (chỉ trả tuple), nên ném lại exception
            // với message tiếng Việt rõ nghĩa để caller (InternalPmisSyncController) hiển thị đúng thay vì
            // lộ nguyên văn lỗi Oracle.
            throw new InvalidOperationException(
                $"Không thể tạo mới Trạm/Đường dây: mã PMIS '{pmisCode}' đã được dùng cho 1 bản ghi khác đang hoạt động — có thể do 2 lượt đồng bộ chạy đồng thời, vui lòng đồng bộ lại.");
        }
        return (newId, true, true, parentUnresolved);
    }

    private class SyncedPmisCodeRow
    {
        public string PmisCode { get; set; } = string.Empty;
        public int InfraTypeId { get; set; }
    }

    public async Task<IEnumerable<(string PmisCode, int InfraTypeId)>> GetSyncedPmisCodesAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var rows = await _connection.QueryAsync<SyncedPmisCodeRow>(
            $"SELECT PMIS_CODE AS PmisCode, INFRA_TYPE_ID AS InfraTypeId FROM INFRASTRUCTURE WHERE PMIS_CODE IS NOT NULL AND {nameof(Infrastructure.IsDeleted)} = 0");
        return rows.Select(r => (r.PmisCode, r.InfraTypeId));
    }

}
