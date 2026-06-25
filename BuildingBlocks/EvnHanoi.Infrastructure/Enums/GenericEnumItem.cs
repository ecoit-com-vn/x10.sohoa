namespace EvnHanoi.Infrastructure.Enums;

/// <summary>
/// Một phần tử danh mục cố định: Id (số), Code (mã tra cứu/index), Name (tên hiển thị).
/// </summary>
public sealed record GenericEnumItem(int Id, string Code, string Name);
