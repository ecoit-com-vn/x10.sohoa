using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.NotificationService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.NotificationService.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/dossiers")]
public class DossierController : ControllerBase
{
    private readonly IDbConnection _dbConnection;

    public DossierController(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (_dbConnection.State != ConnectionState.Open) _dbConnection.Open();
        
        var countSql = "SELECT COUNT(*) FROM Dossiers";
        var offset = (page - 1) * pageSize;
        
        var sql = @"
            SELECT * FROM (
                SELECT d.Id, d.EquipmentId, d.Title, d.Description, d.Status, d.UnitId, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy,
                       ROW_NUMBER() OVER (ORDER BY d.CreatedAt DESC) AS RN
                FROM Dossiers d
            ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";
            
        var parameters = new DynamicParameters();
        parameters.Add("Offset", offset);
        parameters.Add("OffsetPlusSize", offset + pageSize);
        
        var totalCount = await _dbConnection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await _dbConnection.QueryAsync<Dossier>(sql, parameters);
        
        return Ok(new { items, totalCount, page, pageSize });
    }
}
