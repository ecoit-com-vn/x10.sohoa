namespace EvnHanoi.SyncService.Models.Pmis;

/// <summary>Khung response chung của mọi API danh sách PMIS: {total, skip, take, items[]}.</summary>
public class PmisListResponse<T>
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public List<T> Items { get; set; } = [];
}
