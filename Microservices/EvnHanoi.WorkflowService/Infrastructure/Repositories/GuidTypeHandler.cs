using System;
using System.Data;
using Dapper;

namespace EvnHanoi.WorkflowService.Infrastructure.Repositories
{
    public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter parameter, Guid value)
        {
            parameter.Value = value.ToString();
        }

        public override Guid Parse(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return Guid.Empty;
            }
            if (value is string s && Guid.TryParse(s, out var guid))
            {
                return guid;
            }
            return Guid.Parse(value.ToString()!);
        }
    }
}
