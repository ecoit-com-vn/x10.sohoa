using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/equipment/dossiers")]
public class DossierController : ControllerBase
{
    private readonly IDossierRepository _dossierRepository;

    public DossierController(IDossierRepository dossierRepository)
    {
        _dossierRepository = dossierRepository;
    }

    [HttpGet]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetAll()
    {
        var dossiers = await _dossierRepository.GetAllAsync();
        return Ok(dossiers);
    }
}
