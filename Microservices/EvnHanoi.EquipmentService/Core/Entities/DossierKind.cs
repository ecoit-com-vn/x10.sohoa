namespace EvnHanoi.EquipmentService.Core.Entities;

/// <summary>
/// Phân loại hồ sơ — khớp bảng DOSSIER_KINDS.
/// </summary>
public static class DossierKind
{
    public sealed class KindItem
    {
        public int Id { get; }
        public string Code { get; }
        public string Name { get; }

        public KindItem(int id, string code, string name)
        {
            Id = id;
            Code = code;
            Name = name;
        }
    }

    public static readonly KindItem Digitization = new(1, "Digitization", "Hồ sơ số hóa");
    public static readonly KindItem New = new(2, "New", "Hồ sơ mới");

    public static KindItem? TryGetById(int? id) => id switch
    {
        1 => Digitization,
        2 => New,
        _ => null
    };

    public static KindItem RequireById(int id) =>
        TryGetById(id) ?? throw new KeyNotFoundException($"DossierKind không hợp lệ: {id}.");
}
