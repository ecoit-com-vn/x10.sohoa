using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>
/// Catalog item returned by the hierarchical MUC_LUC listing.
/// The hierarchy metadata is response-only and does not represent database columns.
/// </summary>
public sealed class CatalogHierarchyItemDto : Catalog
{
    public int Level { get; set; }
    public bool HasChildren { get; set; }
    public bool IsContextOnly { get; set; }
}

public sealed record CatalogHierarchyPage(
    IReadOnlyList<CatalogHierarchyItemDto> Items,
    int TotalCount,
    int TotalItemCount);
