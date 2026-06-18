namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>
/// Thông tin người dùng rút gọn để hiển thị trên danh sách (id / username / fullname).
/// </summary>
public class CreatorInfoDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Row mapping cho Dapper multi-map (alias cột SQL khớp property).
/// </summary>
internal class CreatorInfoRow
{
    public string CreatorId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    public CreatorInfoDto? ToDto()
    {
        if (string.IsNullOrWhiteSpace(CreatorId))
        {
            return null;
        }

        return new CreatorInfoDto
        {
            Id = CreatorId,
            Username = Username ?? string.Empty,
            Name = FullName ?? string.Empty
        };
    }
}
