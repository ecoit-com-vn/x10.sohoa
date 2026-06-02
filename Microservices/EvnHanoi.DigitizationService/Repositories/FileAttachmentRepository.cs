using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.DigitizationService.Models;

namespace EvnHanoi.DigitizationService.Repositories
{
    public class FileAttachmentRepository : IFileAttachmentRepository
    {
        private readonly IDbConnection _connection;

        public FileAttachmentRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<int> CreateAsync(FileAttachment fileAttachment)
        {
            var sql = $@"
                INSERT INTO FILE_ATTACHMENT (
                    FILE_NAME, FILE_PATH, CONTENT_TYPE, FILE_SIZE, UPLOADED_AT, UPLOADED_BY, STATUS
                ) VALUES (
                    :{nameof(FileAttachment.FileName)}, :{nameof(FileAttachment.FilePath)}, :{nameof(FileAttachment.ContentType)}, :{nameof(FileAttachment.FileSize)}, :{nameof(FileAttachment.UploadedAt)}, :{nameof(FileAttachment.UploadedBy)}, :{nameof(FileAttachment.Status)}
                ) RETURNING ID INTO :Id";

            var parameters = new DynamicParameters(fileAttachment);
            parameters.Add("Id", dbType: DbType.Int32, direction: ParameterDirection.Output);
            
            await _connection.ExecuteAsync(sql, parameters);
            
            return parameters.Get<int>("Id");
        }

        public async Task UpdateStatusAsync(int id, string status)
        {
            var sql = $"UPDATE FILE_ATTACHMENT SET STATUS = :Status WHERE ID = :Id";
            await _connection.ExecuteAsync(sql, new { Id = id, Status = status });
        }
    }
}
