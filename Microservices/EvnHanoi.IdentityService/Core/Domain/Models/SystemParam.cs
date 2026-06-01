using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class SystemParam
{
    public string ParamKey { get; set; } = string.Empty;
    public string ParamValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DataType { get; set; } = "String";
}
