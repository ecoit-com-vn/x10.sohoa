using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.DigitizationService.Models;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.DigitizationService.Repositories
{
    public class VirtualFolderRepository : IVirtualFolderRepository
    {
        private readonly string _connectionString;

        public VirtualFolderRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration));
        }

        private IDbConnection CreateConnection()
        {
            return new OracleConnection(_connectionString);
        }

        public async Task<IEnumerable<VirtualFolder>> GetAllAsync(long? unitId = null, string? equipmentId = null)
        {
            using var connection = CreateConnection();
            if (!string.IsNullOrEmpty(equipmentId))
            {
                var sql = "SELECT Id, Name, ParentId, UnitId, EquipmentId, CreatedBy, CreatedDate FROM VIRTUAL_FOLDERS WHERE EquipmentId = :EquipmentId ORDER BY CreatedDate ASC";
                return await connection.QueryAsync<VirtualFolder>(sql, new { EquipmentId = equipmentId });
            }
            else
            {
                var sql = "SELECT Id, Name, ParentId, UnitId, EquipmentId, CreatedBy, CreatedDate FROM VIRTUAL_FOLDERS WHERE (UnitId IS NULL";
                if (unitId.HasValue)
                {
                    sql += " OR UnitId = :UnitId";
                }
                sql += ") AND EquipmentId IS NULL ORDER BY CreatedDate ASC";
                return await connection.QueryAsync<VirtualFolder>(sql, new { UnitId = unitId });
            }
        }

        public async Task<VirtualFolder?> GetByIdAsync(long id)
        {
            using var connection = CreateConnection();
            var sql = "SELECT Id, Name, ParentId, UnitId, EquipmentId, CreatedBy, CreatedDate FROM VIRTUAL_FOLDERS WHERE Id = :Id";
            return await connection.QuerySingleOrDefaultAsync<VirtualFolder>(sql, new { Id = id });
        }

        public async Task<long> CreateAsync(VirtualFolder folder)
        {
            using var connection = CreateConnection();
            var sql = @"
                INSERT INTO VIRTUAL_FOLDERS (Name, ParentId, UnitId, EquipmentId, CreatedBy, CreatedDate)
                VALUES (:Name, :ParentId, :UnitId, :EquipmentId, :CreatedBy, :CreatedDate)
                RETURNING Id INTO :Id";

            var parameters = new DynamicParameters(folder);
            parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

            await connection.ExecuteAsync(sql, parameters);
            return parameters.Get<long>("Id");
        }

        public async Task<bool> UpdateAsync(VirtualFolder folder)
        {
            using var connection = CreateConnection();
            var sql = @"
                UPDATE VIRTUAL_FOLDERS 
                SET Name = :Name, ParentId = :ParentId, UnitId = :UnitId, EquipmentId = :EquipmentId 
                WHERE Id = :Id";
            var affected = await connection.ExecuteAsync(sql, folder);
            return affected > 0;
        }


        public async Task<bool> DeleteAsync(long id)
        {
            using var connection = CreateConnection();
            var sql = "DELETE FROM VIRTUAL_FOLDERS WHERE Id = :Id";
            var affected = await connection.ExecuteAsync(sql, new { Id = id });
            return affected > 0;
        }

        public async Task AddDocumentToFolderAsync(long folderId, int documentId)
        {
            using var connection = CreateConnection();
            var sql = "INSERT INTO FOLDER_DOCUMENTS (FolderId, DocumentId) VALUES (:FolderId, :DocumentId)";
            await connection.ExecuteAsync(sql, new { FolderId = folderId, DocumentId = documentId });
        }

        public async Task RemoveDocumentFromFolderAsync(long folderId, int documentId)
        {
            using var connection = CreateConnection();
            var sql = "DELETE FROM FOLDER_DOCUMENTS WHERE FolderId = :FolderId AND DocumentId = :DocumentId";
            await connection.ExecuteAsync(sql, new { FolderId = folderId, DocumentId = documentId });
        }

        public async Task<IEnumerable<FileAttachment>> GetDocumentsInFolderAsync(long folderId)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT f.ID as Id, f.FILE_NAME as FileName, f.FILE_PATH as FilePath, 
                       f.CONTENT_TYPE as ContentType, f.FILE_SIZE as FileSize, 
                       f.UPLOADED_AT as UploadedAt, f.UPLOADED_BY as UploadedBy, f.STATUS as Status
                FROM FILE_ATTACHMENT f
                JOIN FOLDER_DOCUMENTS fd ON f.ID = fd.DocumentId
                WHERE fd.FolderId = :FolderId";
            return await connection.QueryAsync<FileAttachment>(sql, new { FolderId = folderId });
        }

        public async Task<IEnumerable<VirtualFolder>> GetChildFoldersAsync(long parentId)
        {
            using var connection = CreateConnection();
            var sql = "SELECT Id, Name, ParentId, UnitId, EquipmentId, CreatedBy, CreatedDate FROM VIRTUAL_FOLDERS WHERE ParentId = :ParentId ORDER BY CreatedDate ASC";
            return await connection.QueryAsync<VirtualFolder>(sql, new { ParentId = parentId });
        }
    }
}
