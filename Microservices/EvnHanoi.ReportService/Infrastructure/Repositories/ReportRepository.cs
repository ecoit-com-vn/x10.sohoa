// Microservices/EvnHanoi.ReportService/Infrastructure/Repositories/ReportRepository.cs
using Dapper;
using EvnHanoi.ReportService.Core.Entities;
using EvnHanoi.ReportService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace EvnHanoi.ReportService.Infrastructure.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly IDbConnection _connection;

        public ReportRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        // --- SYSTEM REPORT GROUP ---

        public async Task<IEnumerable<ReportGroup>> GetReportGroupsAsync()
        {
            var sqlGroup = @"
                SELECT g.Id, g.Code, g.Name, g.SortOrder, g.Description, g.IsActive, g.IsDeleted,
                       (SELECT COUNT(1) FROM REPORT_GROUP_REPORTS rgr WHERE rgr.ReportGroupId = g.Id) AS ReportCount,
                       (SELECT COUNT(1) FROM REPORT_GROUP_UNITS rgu WHERE rgu.ReportGroupId = g.Id) AS UnitCount
                FROM REPORT_GROUPS g
                WHERE g.IsDeleted = 0
                ORDER BY g.SortOrder, g.Name";

            return await _connection.QueryAsync<ReportGroup>(sqlGroup);
        }

        public async Task<ReportGroup?> GetReportGroupByIdAsync(long id)
        {
            var sqlGroup = "SELECT Id, Code, Name, SortOrder, Description, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, IsActive FROM REPORT_GROUPS WHERE Id = :Id AND IsDeleted = 0";
            var sqlReports = "SELECT r.Id, r.Code, r.Name FROM REPORTS r JOIN REPORT_GROUP_REPORTS rgr ON r.Id = rgr.ReportId WHERE rgr.ReportGroupId = :Id";
            var sqlUnits = "SELECT UnitId FROM REPORT_GROUP_UNITS WHERE ReportGroupId = :Id";

            var group = await _connection.QueryFirstOrDefaultAsync<ReportGroup>(sqlGroup, new { Id = id });
            if (group != null)
            {
                var reports = await _connection.QueryAsync<Report>(sqlReports, new { Id = id });
                var unitIds = await _connection.QueryAsync<long>(sqlUnits, new { Id = id });

                group.Reports = reports.ToList();
                group.UnitIds = unitIds.ToList();
            }

            return group;
        }

        public async Task<long> CreateReportGroupAsync(ReportGroup group, List<long> reportIds, List<long> unitIds)
        {
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    var sql = @"INSERT INTO REPORT_GROUPS (
                                    Code, 
                                    Name, 
                                    SortOrder, 
                                    Description, 
                                    CreatedBy,
                                    IsDeleted,
                                    IsActive
                                ) 
                                VALUES (:Code, :Name, :SortOrder, :Description, :CreatedBy, 0, :IsActive) 
                                RETURNING Id INTO :Id";

                    var p = new DynamicParameters();
                    p.Add("Code", group.Code.Trim());
                    p.Add("Name", group.Name.Trim());
                    p.Add("SortOrder", group.SortOrder);
                    p.Add("Description", group.Description?.Trim());
                    p.Add("CreatedBy", group.CreatedBy);
                    p.Add("IsActive", group.IsActive);
                    p.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

                    await _connection.ExecuteAsync(sql, p, transaction);
                    long newGroupId = p.Get<long>("Id");

                    // Insert mapping reports
                    if (reportIds != null && reportIds.Any())
                    {
                        var sqlReports = "INSERT INTO REPORT_GROUP_REPORTS (ReportGroupId, ReportId) VALUES (:ReportGroupId, :ReportId)";
                        var reportMappings = reportIds.Select(rId => new { ReportGroupId = newGroupId, ReportId = rId });
                        await _connection.ExecuteAsync(sqlReports, reportMappings, transaction);
                    }

                    // Insert mapping units
                    if (unitIds != null && unitIds.Any())
                    {
                        var sqlUnits = "INSERT INTO REPORT_GROUP_UNITS (ReportGroupId, UnitId) VALUES (:ReportGroupId, :UnitId)";
                        var unitMappings = unitIds.Select(uId => new { ReportGroupId = newGroupId, UnitId = uId });
                        await _connection.ExecuteAsync(sqlUnits, unitMappings, transaction);
                    }

                    transaction.Commit();
                    return newGroupId;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public async Task<bool> UpdateReportGroupAsync(ReportGroup group, List<long> reportIds, List<long> unitIds)
        {
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    var sql = @"
                        UPDATE REPORT_GROUPS 
                        SET Code = :Code,
                            Name = :Name, 
                            SortOrder = :SortOrder, 
                            Description = :Description, 
                            IsActive = :IsActive,
                            UpdatedAt = :UpdatedAt, 
                            UpdatedBy = :UpdatedBy 
                        WHERE Id = :Id AND IsDeleted = 0";

                    var rows = await _connection.ExecuteAsync(sql, new
                    {
                        Code = group.Code.Trim(),
                        Name = group.Name.Trim(),
                        group.SortOrder,
                        Description = group.Description?.Trim(),
                        group.IsActive,
                        UpdatedAt = DateTime.UtcNow,
                        group.UpdatedBy,
                        group.Id
                    }, transaction);

                    if (rows == 0)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    // Clear old mapping reports and insert new ones
                    await _connection.ExecuteAsync("DELETE FROM REPORT_GROUP_REPORTS WHERE ReportGroupId = :Id", new { Id = group.Id }, transaction);
                    if (reportIds != null && reportIds.Any())
                    {
                        var sqlReports = "INSERT INTO REPORT_GROUP_REPORTS (ReportGroupId, ReportId) VALUES (:ReportGroupId, :ReportId)";
                        var reportMappings = reportIds.Select(rId => new { ReportGroupId = group.Id, ReportId = rId });
                        await _connection.ExecuteAsync(sqlReports, reportMappings, transaction);
                    }

                    // Clear old mapping units and insert new ones
                    await _connection.ExecuteAsync("DELETE FROM REPORT_GROUP_UNITS WHERE ReportGroupId = :Id", new { Id = group.Id }, transaction);
                    if (unitIds != null && unitIds.Any())
                    {
                        var sqlUnits = "INSERT INTO REPORT_GROUP_UNITS (ReportGroupId, UnitId) VALUES (:ReportGroupId, :UnitId)";
                        var unitMappings = unitIds.Select(uId => new { ReportGroupId = group.Id, UnitId = uId });
                        await _connection.ExecuteAsync(sqlUnits, unitMappings, transaction);
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public async Task<bool> DeleteReportGroupAsync(long id)
        {
            // Soft delete
            var sql = "UPDATE REPORT_GROUPS SET IsDeleted = 1, UpdatedAt = :UpdatedAt WHERE Id = :Id AND IsDeleted = 0";
            var rows = await _connection.ExecuteAsync(sql, new { Id = id, UpdatedAt = DateTime.UtcNow });
            return rows > 0;
        }

        // --- SYSTEM REPORTS LOOKUP ---

        public async Task<IEnumerable<Report>> GetSystemReportsAsync()
        {
            var sql = "SELECT Id, Code, Name FROM REPORTS ORDER BY Id";
            return await _connection.QueryAsync<Report>(sql);
        }

        // --- REPORT UNIT PUBLISH ---

        public async Task<IEnumerable<ReportUnitPublish>> GetReportUnitPublishesAsync(long unitId)
        {
            // Danh sách báo cáo mà đơn vị được gán xem, dựa trên Nhóm báo cáo hệ thống đã gán cho đơn vị.
            var sqlReports = @"
                SELECT DISTINCT
                       r.Id AS ReportId,
                       r.Code AS ReportCode,
                       r.Name AS ReportName,
                       NVL(rup.Id, 0) AS Id,
                       NVL(rup.IsPublish, 0) AS IsPublish
                FROM REPORTS r
                INNER JOIN REPORT_GROUP_REPORTS rgr ON rgr.ReportId = r.Id
                INNER JOIN REPORT_GROUP_UNITS rgu ON rgu.ReportGroupId = rgr.ReportGroupId
                INNER JOIN REPORT_GROUPS rg ON rg.Id = rgu.ReportGroupId AND rg.IsDeleted = 0
                LEFT JOIN REPORT_UNIT_PUBLISH rup ON rup.ReportId = r.Id AND rup.UnitId = :UnitId
                WHERE rgu.UnitId = :UnitId
                ORDER BY r.Id";

            var reports = (await _connection.QueryAsync<ReportUnitPublish>(sqlReports, new { UnitId = unitId })).ToList();

            if (!reports.Any())
                return reports;

            var sqlRoles = @"
                SELECT rup.Id AS PublishId, rupr.RoleId
                FROM REPORT_UNIT_PUBLISH rup
                INNER JOIN REPORT_UNIT_PUBLISH_ROLE rupr ON rupr.PublishId = rup.Id
                WHERE rup.UnitId = :UnitId";

            var roleRows = await _connection.QueryAsync<(long PublishId, long RoleId)>(sqlRoles, new { UnitId = unitId });
            var rolesByPublishId = roleRows
                .GroupBy(r => r.PublishId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.RoleId).ToList());

            foreach (var report in reports)
            {
                if (report.Id > 0 && rolesByPublishId.TryGetValue(report.Id, out var roleIds))
                {
                    report.RoleIds = roleIds;
                }
            }

            return reports;
        }

        public async Task<bool> SaveReportUnitPublishAsync(long unitId, long reportId, int isPublish, List<long> roleIds, string? updatedBy)
        {
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    var existingId = await _connection.QueryFirstOrDefaultAsync<long?>(
                        "SELECT Id FROM REPORT_UNIT_PUBLISH WHERE ReportId = :ReportId AND UnitId = :UnitId",
                        new { ReportId = reportId, UnitId = unitId },
                        transaction);

                    long publishId;

                    if (existingId.HasValue)
                    {
                        publishId = existingId.Value;
                        await _connection.ExecuteAsync(@"
                            UPDATE REPORT_UNIT_PUBLISH
                            SET IsPublish = :IsPublish,
                                UpdatedAt = :UpdatedAt,
                                UpdatedBy = :UpdatedBy
                            WHERE Id = :Id",
                            new { IsPublish = isPublish, UpdatedAt = DateTime.UtcNow, UpdatedBy = updatedBy, Id = publishId },
                            transaction);
                    }
                    else
                    {
                        var sql = @"INSERT INTO REPORT_UNIT_PUBLISH (
                                        ReportId,
                                        UnitId,
                                        IsPublish,
                                        CreatedBy
                                    )
                                    VALUES (:ReportId, :UnitId, :IsPublish, :CreatedBy)
                                    RETURNING Id INTO :Id";

                        var p = new DynamicParameters();
                        p.Add("ReportId", reportId);
                        p.Add("UnitId", unitId);
                        p.Add("IsPublish", isPublish);
                        p.Add("CreatedBy", updatedBy);
                        p.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

                        await _connection.ExecuteAsync(sql, p, transaction);
                        publishId = p.Get<long>("Id");
                    }

                    await _connection.ExecuteAsync(
                        "DELETE FROM REPORT_UNIT_PUBLISH_ROLE WHERE PublishId = :PublishId",
                        new { PublishId = publishId },
                        transaction);

                    if (roleIds != null && roleIds.Any())
                    {
                        var sqlRoles = "INSERT INTO REPORT_UNIT_PUBLISH_ROLE (PublishId, RoleId) VALUES (:PublishId, :RoleId)";
                        var roleMappings = roleIds.Distinct().Select(rId => new { PublishId = publishId, RoleId = rId });
                        await _connection.ExecuteAsync(sqlRoles, roleMappings, transaction);
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public async Task<IEnumerable<Report>> GetPublishedReportsForUserAsync(long unitId, List<string> roleCodes)
        {
            if (roleCodes == null || !roleCodes.Any())
                return Enumerable.Empty<Report>();

            var isAdmin = roleCodes.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(r, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase));

            if (isAdmin)
            {
                var sqlAdmin = @"
                    SELECT DISTINCT r.Id, r.Code, r.Name
                    FROM REPORTS r
                    INNER JOIN REPORT_UNIT_PUBLISH rup ON rup.ReportId = r.Id
                    WHERE rup.UnitId = :UnitId
                      AND rup.IsPublish = 1
                    ORDER BY r.Id";

                return await _connection.QueryAsync<Report>(sqlAdmin, new { UnitId = unitId });
            }

            var sql = @"
                SELECT DISTINCT r.Id, r.Code, r.Name
                FROM REPORTS r
                INNER JOIN REPORT_UNIT_PUBLISH rup ON rup.ReportId = r.Id
                INNER JOIN REPORT_UNIT_PUBLISH_ROLE rupr ON rupr.PublishId = rup.Id
                INNER JOIN ROLE ro ON ro.Id = rupr.RoleId
                WHERE rup.UnitId = :UnitId
                  AND rup.IsPublish = 1
                  AND UPPER(ro.Code) IN :RoleCodes
                ORDER BY r.Id";

            var upperRoleCodes = roleCodes.Select(r => r.ToUpperInvariant()).Distinct().ToList();
            return await _connection.QueryAsync<Report>(sql, new { UnitId = unitId, RoleCodes = upperRoleCodes });
        }
    }
}
