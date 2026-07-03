using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Guard lớp 2 — chống thao tác chéo kind_id giữa shell controller digitization và hồ sơ mới.
/// </summary>
public class DossierKindGuard
{
    private readonly IDossierRepository _dossierRepository;

    public DossierKindGuard(IDossierRepository dossierRepository)
    {
        _dossierRepository = dossierRepository ?? throw new ArgumentNullException(nameof(dossierRepository));
    }

    public async Task EnsureAsync(Guid dossierId, int expectedKindId)
    {
        var kindId = await _dossierRepository.GetKindIdAsync(dossierId);
        if (kindId is null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {dossierId}");

        if (kindId.Value != expectedKindId)
        {
            var expected = DossierKind.RequireById(expectedKindId);
            throw new InvalidOperationException(
                $"Hồ sơ không thuộc loại '{expected.Name}'. Thao tác bị từ chối.");
        }
    }
}
