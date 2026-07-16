 using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>Hồ sơ mới (kind_id=2) — giữ route và quyền DOSSIER_* hiện tại.</summary>
[Authorize]
[ApiController]
[Route("api/v1/dossiers")]
public partial class DossierController : DossierControllerBase
{
    protected override int ExpectedKindId => DossierKind.New.Id;

    public DossierController(
        IDossierService dossierService,
        IDossierDocumentService dossierDocumentService,
        IDocumentDigitizationService documentDigitizationService,
        DossierKindGuard kindGuard)
        : base(dossierService, dossierDocumentService, documentDigitizationService, kindGuard)
    {
    }
}
