using System.Data;
using Dapper;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.Infrastructure.Database;

/// <summary>
/// Bind chuỗi vào cột Oracle CLOB qua Dapper (tránh lỗi OracleParameter.Size &gt; 4000).
/// </summary>
public sealed class OracleClobParameter : SqlMapper.ICustomQueryParameter
{
    private readonly string? _value;

    public OracleClobParameter(string? value) => _value = value;

    public void AddParameter(IDbCommand command, string name)
    {
        command.Parameters.Add(new OracleParameter(name, OracleDbType.Clob)
        {
            Value = string.IsNullOrEmpty(_value) ? DBNull.Value : _value
        });
    }
}

public static class OracleClob
{
    public static OracleClobParameter Param(string? value) => new(value);
}
