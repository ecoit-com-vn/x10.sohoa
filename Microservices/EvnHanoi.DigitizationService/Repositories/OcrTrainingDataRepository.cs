using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Models.Dto;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.DigitizationService.Repositories
{
    public class OcrTrainingDataRepository : IOcrTrainingDataRepository
    {
        private readonly string _connectionString;

        public OcrTrainingDataRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

        public async Task<long> CreateAsync(OcrTrainingData data)
        {
            const string sql = @"
                INSERT INTO OCR_TRAINING_DATA (
                    FILE_NAME, FILE_PATH, BUCKET_NAME, CONTENT_TYPE, FILE_SIZE,
                    DOCUMENT_TYPE, LABEL_TEXT, QUALITY_SCORE, TRAINING_STATUS,
                    IS_VERIFIED, NOTES, UPLOADED_AT, UPLOADED_BY, CREATED_AT, UPDATED_AT
                ) VALUES (
                    :FileName, :FilePath, :BucketName, :ContentType, :FileSize,
                    :DocumentType, :LabelText, :QualityScore, :TrainingStatus,
                    :IsVerifiedNum, :Notes, :UploadedAt, :UploadedBy, :CreatedAt, :UpdatedAt
                ) RETURNING ID INTO :Id";

            using var connection = CreateConnection();
            var parameters = new DynamicParameters();
            parameters.Add("FileName", data.FileName);
            parameters.Add("FilePath", data.FilePath);
            parameters.Add("BucketName", data.BucketName);
            parameters.Add("ContentType", data.ContentType);
            parameters.Add("FileSize", data.FileSize);
            parameters.Add("DocumentType", data.DocumentType);
            parameters.Add("LabelText", data.LabelText);
            parameters.Add("QualityScore", data.QualityScore);
            parameters.Add("TrainingStatus", data.TrainingStatus);
            parameters.Add("IsVerifiedNum", data.IsVerified ? 1 : 0);
            parameters.Add("Notes", data.Notes);
            parameters.Add("UploadedAt", data.UploadedAt);
            parameters.Add("UploadedBy", data.UploadedBy);
            parameters.Add("CreatedAt", data.CreatedAt);
            parameters.Add("UpdatedAt", data.UpdatedAt);
            parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

            await connection.ExecuteAsync(sql, parameters);
            return parameters.Get<long>("Id");
        }

        public async Task<OcrTrainingData?> GetByIdAsync(long id)
        {
            const string sql = @"
                SELECT ID AS Id, FILE_NAME AS FileName, FILE_PATH AS FilePath,
                       BUCKET_NAME AS BucketName, CONTENT_TYPE AS ContentType,
                       FILE_SIZE AS FileSize, DOCUMENT_TYPE AS DocumentType,
                       LABEL_TEXT AS LabelText, QUALITY_SCORE AS QualityScore,
                       TRAINING_STATUS AS TrainingStatus,
                       CASE IS_VERIFIED WHEN 1 THEN 1 ELSE 0 END AS IsVerified,
                       VERIFIED_BY AS VerifiedBy, VERIFIED_AT AS VerifiedAt,
                       NOTES AS Notes, UPLOADED_AT AS UploadedAt,
                       UPLOADED_BY AS UploadedBy, CREATED_AT AS CreatedAt,
                       UPDATED_AT AS UpdatedAt
                FROM OCR_TRAINING_DATA WHERE ID = :Id";

            using var connection = CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<OcrTrainingData>(sql, new { Id = id });
        }

        public async Task<PagedResult<OcrTrainingDataSummaryDto>> GetPagedAsync(
            int page, int pageSize,
            string? documentType = null,
            string? trainingStatus = null,
            string? keyword = null)
        {
            var conditions = new List<string>();
            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(documentType))
            {
                conditions.Add("DOCUMENT_TYPE = :DocumentType");
                parameters.Add("DocumentType", documentType);
            }
            if (!string.IsNullOrWhiteSpace(trainingStatus))
            {
                conditions.Add("TRAINING_STATUS = :TrainingStatus");
                parameters.Add("TrainingStatus", trainingStatus);
            }
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                conditions.Add("(UPPER(FILE_NAME) LIKE UPPER(:Keyword) OR UPPER(UPLOADED_BY) LIKE UPPER(:Keyword))");
                parameters.Add("Keyword", $"%{keyword}%");
            }

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

            var countSql = $"SELECT COUNT(*) FROM OCR_TRAINING_DATA {whereClause}";
            var offset = (page - 1) * pageSize;

            var dataSql = $@"
                SELECT * FROM (
                    SELECT ID AS Id, FILE_NAME AS FileName, CONTENT_TYPE AS ContentType,
                           FILE_SIZE AS FileSize, DOCUMENT_TYPE AS DocumentType,
                           TRAINING_STATUS AS TrainingStatus,
                           CASE IS_VERIFIED WHEN 1 THEN 1 ELSE 0 END AS IsVerified,
                           QUALITY_SCORE AS QualityScore,
                           UPLOADED_BY AS UploadedBy, UPLOADED_AT AS UploadedAt,
                           ROW_NUMBER() OVER (ORDER BY UPLOADED_AT DESC) AS RN
                    FROM OCR_TRAINING_DATA {whereClause}
                ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";

            parameters.Add("Offset", offset);
            parameters.Add("OffsetPlusSize", offset + pageSize);

            using var connection = CreateConnection();
            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
            var items = (await connection.QueryAsync<OcrTrainingDataSummaryDto>(dataSql, parameters)).ToList();

            return new PagedResult<OcrTrainingDataSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task UpdateAsync(OcrTrainingData data)
        {
            const string sql = @"
                UPDATE OCR_TRAINING_DATA SET
                    DOCUMENT_TYPE = :DocumentType,
                    LABEL_TEXT = :LabelText,
                    QUALITY_SCORE = :QualityScore,
                    TRAINING_STATUS = :TrainingStatus,
                    IS_VERIFIED = :IsVerifiedNum,
                    VERIFIED_BY = :VerifiedBy,
                    VERIFIED_AT = :VerifiedAt,
                    NOTES = :Notes,
                    UPDATED_AT = :UpdatedAt
                WHERE ID = :Id";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new
            {
                data.DocumentType,
                data.LabelText,
                data.QualityScore,
                data.TrainingStatus,
                IsVerifiedNum = data.IsVerified ? 1 : 0,
                data.VerifiedBy,
                data.VerifiedAt,
                data.Notes,
                UpdatedAt = DateTime.UtcNow,
                data.Id
            });
        }

        public async Task UpdateLabelAsync(long id, string? labelText, string documentType,
            string trainingStatus, decimal? qualityScore, string? notes)
        {
            const string sql = @"
                UPDATE OCR_TRAINING_DATA SET
                    LABEL_TEXT = :LabelText, DOCUMENT_TYPE = :DocumentType,
                    TRAINING_STATUS = :TrainingStatus, QUALITY_SCORE = :QualityScore,
                    NOTES = :Notes, UPDATED_AT = :UpdatedAt
                WHERE ID = :Id";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new
            {
                LabelText = labelText,
                DocumentType = documentType,
                TrainingStatus = trainingStatus,
                QualityScore = qualityScore,
                Notes = notes,
                UpdatedAt = DateTime.UtcNow,
                Id = id
            });
        }

        public async Task VerifyAsync(long id, bool isVerified, string verifiedBy, string? notes)
        {
            const string sql = @"
                UPDATE OCR_TRAINING_DATA SET
                    IS_VERIFIED = :IsVerifiedNum,
                    VERIFIED_BY = :VerifiedBy,
                    VERIFIED_AT = :VerifiedAt,
                    TRAINING_STATUS = :TrainingStatus,
                    NOTES = :Notes,
                    UPDATED_AT = :UpdatedAt
                WHERE ID = :Id";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new
            {
                IsVerifiedNum = isVerified ? 1 : 0,
                VerifiedBy = verifiedBy,
                VerifiedAt = DateTime.UtcNow,
                TrainingStatus = isVerified ? "Verified" : "Rejected",
                Notes = notes,
                UpdatedAt = DateTime.UtcNow,
                Id = id
            });
        }

        public async Task DeleteAsync(long id)
        {
            const string sql = "DELETE FROM OCR_TRAINING_DATA WHERE ID = :Id";
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<int> GetCountByStatusAsync(string status)
        {
            const string sql = "SELECT COUNT(*) FROM OCR_TRAINING_DATA WHERE TRAINING_STATUS = :Status";
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, new { Status = status });
        }
    }
}
