using System;
using System.Collections.Generic;

namespace EvnHanoi.EquipmentService.Core.DTOs;

public class DossierImportResultDto
{
    public List<ImportRowResultDto> SuccessDossiers { get; set; } = new();
    public List<ImportRowResultDto> FailedDossiers { get; set; } = new();
}

public class ImportRowResultDto
{
    public int RowIndex { get; set; }
    public string? STT { get; set; }
    public string? DossierGroupName { get; set; }
    public string? GridTypeName { get; set; }
    public string? InfrastructureCode { get; set; }
    public string? StorageBoxCode { get; set; }
    public string? DossierTypeCode { get; set; }
    public string? EquipmentCodes { get; set; }
    public string? Note { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? CreatedDossierId { get; set; }
}
