using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>Shell hồ sơ số hóa (kind_id=1) — quyền DOSSIER_DIGITIZATION_*.</summary>
[Authorize]
[ApiController]
[Route("api/v1/dossier-digitization/dossiers")]
public partial class DossierDigitizationController : DossierControllerBase
{
    protected override int ExpectedKindId => DossierKind.Digitization.Id;

    public DossierDigitizationController(
        IDossierService dossierService,
        IDossierDocumentService dossierDocumentService,
        IDocumentDigitizationService documentDigitizationService,
        DossierKindGuard kindGuard,
        IAuditPublisher auditPublisher,
        AuditServiceMetadata auditServiceMetadata)
        : base(dossierService, dossierDocumentService, documentDigitizationService, kindGuard, auditPublisher, auditServiceMetadata)
    {
    }
}
