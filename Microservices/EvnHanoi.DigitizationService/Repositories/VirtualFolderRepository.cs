using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.DigitizationService.Models;

namespace EvnHanoi.DigitizationService.Repositories
{
    public class VirtualFolderRepository : IVirtualFolderRepository
    {
        private readonly IDbConnection _connection;

        public VirtualFolderRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<IEnumerable<VirtualFolder>> GetAllAsync(long? unitId = null, string? equipmentId = null)
        {
            if (!string.IsNullOrEmpty(equipmentId))
            {
                var sql = $"SELECT {nameof(VirtualFolder.Id)}, {nameof(VirtualFolder.Name)}, {nameof(VirtualFolder.ParentId)}, {nameof(VirtualFolder.UnitId)}, {nameof(VirtualFolder.EquipmentId)}, {nameof(VirtualFolder.CreatedBy)}, {nameof(VirtualFolder.CreatedDate)} FROM VIRTUAL_FOLDERS WHERE {nameof(VirtualFolder.EquipmentId)} = :EquipmentId ORDER BY {nameof(VirtualFolder.CreatedDate)} ASC";
                return await _connection.QueryAsync<VirtualFolder>(sql, new { EquipmentId = equipmentId });
            }
            else
            {
                var sql = $"SELECT {nameof(VirtualFolder.Id)}, {nameof(VirtualFolder.Name)}, {nameof(VirtualFolder.ParentId)}, {nameof(VirtualFolder.UnitId)}, {nameof(VirtualFolder.EquipmentId)}, {nameof(VirtualFolder.CreatedBy)}, {nameof(VirtualFolder.CreatedDate)} FROM VIRTUAL_FOLDERS WHERE ({nameof(VirtualFolder.UnitId)} IS NULL";
                if (unitId.HasValue)
                {
                    sql += $" OR {nameof(VirtualFolder.UnitId)} = :UnitId";
                }
                sql += $") AND {nameof(VirtualFolder.EquipmentId)} IS NULL ORDER BY {nameof(VirtualFolder.CreatedDate)} ASC";
                return await _connection.QueryAsync<VirtualFolder>(sql, new { UnitId = unitId });
            }
        }

        public async Task<VirtualFolder?> GetByIdAsync(long id)
        {
            var sql = $"SELECT {nameof(VirtualFolder.Id)}, {nameof(VirtualFolder.Name)}, {nameof(VirtualFolder.ParentId)}, {nameof(VirtualFolder.UnitId)}, {nameof(VirtualFolder.EquipmentId)}, {nameof(VirtualFolder.CreatedBy)}, {nameof(VirtualFolder.CreatedDate)} FROM VIRTUAL_FOLDERS WHERE {nameof(VirtualFolder.Id)} = :Id";
            return await _connection.QuerySingleOrDefaultAsync<VirtualFolder>(sql, new { Id = id });
        }

        public async Task<long> CreateAsync(VirtualFolder folder)
        {
            var sql = $@"
                INSERT INTO VIRTUAL_FOLDERS (
                    {nameof(VirtualFolder.Name)}, {nameof(VirtualFolder.ParentId)}, {nameof(VirtualFolder.UnitId)}, {nameof(VirtualFolder.EquipmentId)}, {nameof(VirtualFolder.CreatedBy)}, {nameof(VirtualFolder.CreatedDate)}
                )
                VALUES (:Name, :ParentId, :UnitId, :EquipmentId, :CreatedBy, :CreatedDate)
                RETURNING Id INTO :Id";

            var parameters = new DynamicParameters(folder);
            parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

            await _connection.ExecuteAsync(sql, parameters);
            return parameters.Get<long>("Id");
        }

        public async Task<bool> UpdateAsync(VirtualFolder folder)
        {
            var sql = $@"
                UPDATE VIRTUAL_FOLDERS 
                SET {nameof(VirtualFolder.Name)} = :Name, {nameof(VirtualFolder.ParentId)} = :ParentId, {nameof(VirtualFolder.UnitId)} = :UnitId, {nameof(VirtualFolder.EquipmentId)} = :EquipmentId 
                WHERE {nameof(VirtualFolder.Id)} = :Id";
            var affected = await _connection.ExecuteAsync(sql, folder);
            return affected > 0;
        }


        public async Task<bool> DeleteAsync(long id)
        {
            var sql = $"DELETE FROM VIRTUAL_FOLDERS WHERE {nameof(VirtualFolder.Id)} = :Id";
            var affected = await _connection.ExecuteAsync(sql, new { Id = id });
            return affected > 0;
        }

        public async Task AddDocumentToFolderAsync(long folderId, int documentId)
        {
            var sql = "INSERT INTO FOLDER_DOCUMENTS (FolderId, DocumentId) VALUES (:FolderId, :DocumentId)";
            await _connection.ExecuteAsync(sql, new { FolderId = folderId, DocumentId = documentId });
        }

        public async Task RemoveDocumentFromFolderAsync(long folderId, int documentId)
        {
            var sql = "DELETE FROM FOLDER_DOCUMENTS WHERE FolderId = :FolderId AND DocumentId = :DocumentId";
            await _connection.ExecuteAsync(sql, new { FolderId = folderId, DocumentId = documentId });
        }

        public async Task<IEnumerable<FileAttachment>> GetDocumentsInFolderAsync(long folderId)
        {
            var sql = $@"
                SELECT f.ID as {nameof(FileAttachment.Id)}, f.FILE_NAME as {nameof(FileAttachment.FileName)}, f.FILE_PATH as {nameof(FileAttachment.FilePath)}, 
                       f.CONTENT_TYPE as {nameof(FileAttachment.ContentType)}, f.FILE_SIZE as {nameof(FileAttachment.FileSize)}, 
                       f.UPLOADED_AT as {nameof(FileAttachment.UploadedAt)}, f.UPLOADED_BY as {nameof(FileAttachment.UploadedBy)}, f.STATUS as {nameof(FileAttachment.Status)}
                FROM FILE_ATTACHMENT f
                JOIN FOLDER_DOCUMENTS fd ON f.ID = fd.DocumentId
                WHERE fd.FolderId = :FolderId";
            return await _connection.QueryAsync<FileAttachment>(sql, new { FolderId = folderId });
        }

        public async Task<IEnumerable<VirtualFolder>> GetChildFoldersAsync(long parentId)
        {
            var sql = $"SELECT {nameof(VirtualFolder.Id)}, {nameof(VirtualFolder.Name)}, {nameof(VirtualFolder.ParentId)}, {nameof(VirtualFolder.UnitId)}, {nameof(VirtualFolder.EquipmentId)}, {nameof(VirtualFolder.CreatedBy)}, {nameof(VirtualFolder.CreatedDate)} FROM VIRTUAL_FOLDERS WHERE {nameof(VirtualFolder.ParentId)} = :ParentId ORDER BY {nameof(VirtualFolder.CreatedDate)} ASC";
            return await _connection.QueryAsync<VirtualFolder>(sql, new { ParentId = parentId });
        }
    }
}
