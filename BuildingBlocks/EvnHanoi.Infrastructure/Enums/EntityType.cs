using System;
using System.Collections.Generic;
using System.Linq;

namespace EvnHanoi.Infrastructure.Enums;

/// <summary>
/// Danh mục EntityType (GenericEnum: Id, Code, Name).
/// Dùng thống nhất cho WorkflowDefinition.EntityType và WorkflowInstance.EntityType.
/// </summary>
public static class EntityType
{
    public static readonly GenericEnumItem Dossier = new(1, "Dossier", "Quy trình số hóa hồ sơ");
    public static readonly GenericEnumItem BorrowRecord = new(2, "BorrowRecord", "Quy trình mượn/trả hồ sơ kỹ thuật");
    public static readonly GenericEnumItem DossierDigitization = new(3, "DossierDigitization", "Quy trình số hóa hồ sơ (Digitization)");

    private static readonly GenericEnumItem[] All = { Dossier, BorrowRecord, DossierDigitization };

    private static readonly Dictionary<string, GenericEnumItem> ByCode =
        All.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<int, GenericEnumItem> ById =
        All.ToDictionary(x => x.Id);

    public static IReadOnlyList<GenericEnumItem> GetAll() => All;

    public static GenericEnumItem? TryGetByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return ByCode.TryGetValue(code.Trim(), out var item) ? item : null;
    }

    public static GenericEnumItem RequireByCode(string code) =>
        TryGetByCode(code) ?? throw new KeyNotFoundException($"EntityType không hợp lệ: '{code}'.");

    public static GenericEnumItem? TryGetById(int id)
    {
        return ById.TryGetValue(id, out var item) ? item : null;
    }

    public static GenericEnumItem RequireById(int id) =>
        TryGetById(id) ?? throw new KeyNotFoundException($"EntityType không hợp lệ với ID: '{id}'.");
}
