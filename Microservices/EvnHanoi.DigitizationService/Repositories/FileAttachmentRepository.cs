using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.DigitizationService.Models;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.DigitizationService.Repositories
{
    public class FileAttachmentRepository : IFileAttachmentRepository
    {
        private readonly string _connectionString;

        public FileAttachmentRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        private IDbConnection CreateConnection()
        {
            return new OracleConnection(_connectionString);
        }

        public async Task<int> CreateAsync(FileAttachment fileAttachment)
        {
            var sql = @"
                INSERT INTO FILE_ATTACHMENT (
                    FILE_NAME, FILE_PATH, CONTENT_TYPE, FILE_SIZE, UPLOADED_AT, UPLOADED_BY, STATUS
                ) VALUES (
                    :FileName, :FilePath, :ContentType, :FileSize, :UploadedAt, :UploadedBy, :Status
                ) RETURNING ID INTO :Id";

            using var connection = CreateConnection();
            var parameters = new DynamicParameters(fileAttachment);
            parameters.Add("Id", dbType: DbType.Int32, direction: ParameterDirection.Output);
            
            await connection.ExecuteAsync(sql, parameters);
            
            return parameters.Get<int>("Id");
        }

        public async Task UpdateStatusAsync(int id, string status)
        {
            var sql = "UPDATE FILE_ATTACHMENT SET STATUS = :Status WHERE ID = :Id";
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new { Id = id, Status = status });
        }
    }
}
