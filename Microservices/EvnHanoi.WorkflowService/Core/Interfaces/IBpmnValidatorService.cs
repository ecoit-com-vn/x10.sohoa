using System.Collections.Generic;

namespace EvnHanoi.WorkflowService.Core.Interfaces
{
    public interface IBpmnValidatorService
    {
        List<string> Validate(string? xmlString);
    }
}
