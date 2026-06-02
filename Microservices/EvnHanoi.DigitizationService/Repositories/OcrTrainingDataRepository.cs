using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Models.Dto;

namespace EvnHanoi.DigitizationService.Repositories
{
    public class OcrTrainingDataRepository : IOcrTrainingDataRepository
    {
        private readonly IDbConnection _connection;

        public OcrTrainingDataRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<long> CreateAsync(OcrTrainingData data)
        {
            var sql = $@"
                INSERT INTO OCR_TRAINING_DATA (
                    FILE_NAME, FILE_PATH, BUCKET_NAME, CONTENT_TYPE, FILE_SIZE,
                    DOCUMENT_TYPE, LABEL_TEXT, QUALITY_SCORE, TRAINING_STATUS,
                    IS_VERIFIED, NOTES, UPLOADED_AT, UPLOADED_BY, CREATED_AT, UPDATED_AT
                ) VALUES (
                    :{nameof(OcrTrainingData.FileName)}, :{nameof(OcrTrainingData.FilePath)}, :{nameof(OcrTrainingData.BucketName)}, :{nameof(OcrTrainingData.ContentType)}, :{nameof(OcrTrainingData.FileSize)},
                    :{nameof(OcrTrainingData.DocumentType)}, :{nameof(OcrTrainingData.LabelText)}, :{nameof(OcrTrainingData.QualityScore)}, :{nameof(OcrTrainingData.TrainingStatus)},
                    :IsVerifiedNum, :{nameof(OcrTrainingData.Notes)}, :{nameof(OcrTrainingData.UploadedAt)}, :{nameof(OcrTrainingData.UploadedBy)}, :{nameof(OcrTrainingData.CreatedAt)}, :{nameof(OcrTrainingData.UpdatedAt)}
                ) RETURNING ID INTO :Id";

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

            await _connection.ExecuteAsync(sql, parameters);
            return parameters.Get<long>("Id");
        }

        public async Task<OcrTrainingData?> GetByIdAsync(long id)
        {
            var sql = $@"
                SELECT ID AS {nameof(OcrTrainingData.Id)}, FILE_NAME AS {nameof(OcrTrainingData.FileName)}, FILE_PATH AS {nameof(OcrTrainingData.FilePath)},
                       BUCKET_NAME AS {nameof(OcrTrainingData.BucketName)}, CONTENT_TYPE AS {nameof(OcrTrainingData.ContentType)},
                       FILE_SIZE AS {nameof(OcrTrainingData.FileSize)}, DOCUMENT_TYPE AS {nameof(OcrTrainingData.DocumentType)},
                       LABEL_TEXT AS {nameof(OcrTrainingData.LabelText)}, QUALITY_SCORE AS {nameof(OcrTrainingData.QualityScore)},
                       TRAINING_STATUS AS {nameof(OcrTrainingData.TrainingStatus)},
                       CASE IS_VERIFIED WHEN 1 THEN 1 ELSE 0 END AS {nameof(OcrTrainingData.IsVerified)},
                       VERIFIED_BY AS {nameof(OcrTrainingData.VerifiedBy)}, VERIFIED_AT AS {nameof(OcrTrainingData.VerifiedAt)},
                       NOTES AS {nameof(OcrTrainingData.Notes)}, UPLOADED_AT AS {nameof(OcrTrainingData.UploadedAt)},
                       UPLOADED_BY AS {nameof(OcrTrainingData.UploadedBy)}, CREATED_AT AS {nameof(OcrTrainingData.CreatedAt)},
                       UPDATED_AT AS {nameof(OcrTrainingData.UpdatedAt)}
                FROM OCR_TRAINING_DATA WHERE ID = :Id";

            return await _connection.QuerySingleOrDefaultAsync<OcrTrainingData>(sql, new { Id = id });
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
                    SELECT ID AS {nameof(OcrTrainingData.Id)}, FILE_NAME AS {nameof(OcrTrainingData.FileName)}, CONTENT_TYPE AS {nameof(OcrTrainingData.ContentType)},
                           FILE_SIZE AS {nameof(OcrTrainingData.FileSize)}, DOCUMENT_TYPE AS {nameof(OcrTrainingData.DocumentType)},
                           TRAINING_STATUS AS {nameof(OcrTrainingData.TrainingStatus)},
                           CASE IS_VERIFIED WHEN 1 THEN 1 ELSE 0 END AS {nameof(OcrTrainingData.IsVerified)},
                           QUALITY_SCORE AS {nameof(OcrTrainingData.QualityScore)},
                           UPLOADED_BY AS {nameof(OcrTrainingData.UploadedBy)}, UPLOADED_AT AS {nameof(OcrTrainingData.UploadedAt)},
                           ROW_NUMBER() OVER (ORDER BY UPLOADED_AT DESC) AS RN
                    FROM OCR_TRAINING_DATA {whereClause}
                ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";

            parameters.Add("Offset", offset);
            parameters.Add("OffsetPlusSize", offset + pageSize);

            var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
            var items = (await _connection.QueryAsync<OcrTrainingDataSummaryDto>(dataSql, parameters)).ToList();

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
            var sql = $@"
                UPDATE OCR_TRAINING_DATA SET
                    DOCUMENT_TYPE = :{nameof(OcrTrainingData.DocumentType)},
                    LABEL_TEXT = :{nameof(OcrTrainingData.LabelText)},
                    QUALITY_SCORE = :{nameof(OcrTrainingData.QualityScore)},
                    TRAINING_STATUS = :{nameof(OcrTrainingData.TrainingStatus)},
                    IS_VERIFIED = :IsVerifiedNum,
                    VERIFIED_BY = :{nameof(OcrTrainingData.VerifiedBy)},
                    VERIFIED_AT = :{nameof(OcrTrainingData.VerifiedAt)},
                    NOTES = :{nameof(OcrTrainingData.Notes)},
                    UPDATED_AT = :{nameof(OcrTrainingData.UpdatedAt)}
                WHERE ID = :{nameof(OcrTrainingData.Id)}";

            await _connection.ExecuteAsync(sql, new
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
            var sql = $@"
                UPDATE OCR_TRAINING_DATA SET
                    LABEL_TEXT = :LabelText, DOCUMENT_TYPE = :DocumentType,
                    TRAINING_STATUS = :TrainingStatus, QUALITY_SCORE = :QualityScore,
                    NOTES = :Notes, UPDATED_AT = :UpdatedAt
                WHERE ID = :Id";

            await _connection.ExecuteAsync(sql, new
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
            var sql = $@"
                UPDATE OCR_TRAINING_DATA SET
                    IS_VERIFIED = :IsVerifiedNum,
                    VERIFIED_BY = :VerifiedBy,
                    VERIFIED_AT = :VerifiedAt,
                    TRAINING_STATUS = :TrainingStatus,
                    NOTES = :Notes,
                    UPDATED_AT = :UpdatedAt
                WHERE ID = :Id";

            await _connection.ExecuteAsync(sql, new
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
            var sql = "DELETE FROM OCR_TRAINING_DATA WHERE ID = :Id";
            await _connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<int> GetCountByStatusAsync(string status)
        {
            var sql = "SELECT COUNT(*) FROM OCR_TRAINING_DATA WHERE TRAINING_STATUS = :Status";
            return await _connection.ExecuteScalarAsync<int>(sql, new { Status = status });
        }
    }
}
