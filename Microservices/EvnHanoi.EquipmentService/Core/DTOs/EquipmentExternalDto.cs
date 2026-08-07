using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EvnHanoi.EquipmentService.Core.DTOs;

public class EquipmentExternalDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("maTB")]
    public string MaTB { get; set; } = string.Empty;

    [JsonPropertyName("tenTB")]
    public string TenTB { get; set; } = string.Empty;

    [JsonPropertyName("maLoaiTB")]
    public string MaLoaiTB { get; set; } = string.Empty;

    [JsonPropertyName("tenLoaiTB")]
    public string TenLoaiTB { get; set; } = string.Empty;

    [JsonPropertyName("maTBA")]
    public string MaTBA { get; set; } = string.Empty;

    [JsonPropertyName("tenTBA")]
    public string TenTBA { get; set; } = string.Empty;

    [JsonPropertyName("maDonVi")]
    public string MaDonVi { get; set; } = string.Empty;

    [JsonPropertyName("namSanXuat")]
    public int NamSanXuat { get; set; }

    [JsonPropertyName("maQRCode")]
    public string MaQRCode { get; set; } = string.Empty;

    [JsonPropertyName("trangThai")]
    public string TrangThai { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;
}

/// <summary>
/// Danh sách thiết bị kèm "items" — dữ liệu form động (FORM_VALUES) của thiết bị,
/// ví dụ các trường ảnh đính kèm do front-end tự định nghĩa theo mẫu EAV của từng loại thiết bị.
/// </summary>
public class EquipmentDetailListDto
{
    /// <summary>Id nội bộ dùng để join dữ liệu, không trả ra ngoài.</summary>
    [JsonIgnore]
    public Guid Id { get; set; }

    [JsonPropertyName("maTB")]
    public string MaTB { get; set; } = string.Empty;

    [JsonPropertyName("tenTB")]
    public string TenTB { get; set; } = string.Empty;

    [JsonPropertyName("maLoaiTB")]
    public string MaLoaiTB { get; set; } = string.Empty;

    [JsonPropertyName("tenLoaiTB")]
    public string TenLoaiTB { get; set; } = string.Empty;

    [JsonPropertyName("maTBA")]
    public string MaTBA { get; set; } = string.Empty;

    [JsonPropertyName("tenTBA")]
    public string TenTBA { get; set; } = string.Empty;

    [JsonPropertyName("maDonVi")]
    public string MaDonVi { get; set; } = string.Empty;

    [JsonPropertyName("namSanXuat")]
    public int NamSanXuat { get; set; }

    [JsonPropertyName("trangThai")]
    public string TrangThai { get; set; } = string.Empty;

    /// <summary>Dữ liệu form động thô (JSON) đọc từ EQUIPMENTS.FORM_VALUES — dùng để dựng Items, không trả ra ngoài.</summary>
    [JsonIgnore]
    public string? FormValues { get; set; }

    /// <summary>Cấu trúc biểu mẫu EAV (JSON) của loại thiết bị — dùng để tách Items theo từng trường, không trả ra ngoài.</summary>
    [JsonIgnore]
    public string? FormSchema { get; set; }

    [JsonPropertyName("items")]
    public TechnicalParametersDto Items { get; set; } = new();
}

/// <summary>
/// Thông số kỹ thuật thiết bị lưới điện cao áp — mỗi property tương ứng 1 label EAV cố định.
/// Giá trị được gán từ FormValues của thiết bị dựa theo label trùng khớp trong FormSchema
/// (xem EquipmentExternalController.KnownTechnicalParameterLabels / BuildTechnicalParameters).
/// Thêm property mới tại đây khi cần trả thêm 1 thông số kỹ thuật cố định.
/// </summary>
public class TechnicalParametersDto
{
    /// <summary>Hãng SX</summary>
    [JsonPropertyName("hangSx")]
    public string? HangSx { get; set; }

    /// <summary>Nước SX</summary>
    [JsonPropertyName("nuocSx")]
    public string? NuocSx { get; set; }

    /// <summary>Kiểu</summary>
    [JsonPropertyName("kieu")]
    public string? Kieu { get; set; }

    /// <summary>Kiểu (Type)</summary>
    [JsonPropertyName("kieuType")]
    public string? KieuType { get; set; }

    /// <summary>Kiểu cách điện</summary>
    [JsonPropertyName("kieuCachDien")]
    public string? KieuCachDien { get; set; }

    /// <summary>Loại máy</summary>
    [JsonPropertyName("loaiMay")]
    public string? LoaiMay { get; set; }

    /// <summary>Nấc phân áp</summary>
    [JsonPropertyName("nacPhanAp")]
    public string? NacPhanAp { get; set; }

    /// <summary>Tần số (Hz)</summary>
    [JsonPropertyName("tanSoHz")]
    public string? TanSoHz { get; set; }

    /// <summary>Tần số</summary>
    [JsonPropertyName("tanSo")]
    public string? TanSo { get; set; }

    /// <summary>Công suất (kVA)</summary>
    [JsonPropertyName("congSuatKva")]
    public string? CongSuatKva { get; set; }

    /// <summary>Công suất</summary>
    [JsonPropertyName("congSuat")]
    public string? CongSuat { get; set; }

    /// <summary>Công suất (W)</summary>
    [JsonPropertyName("congSuatW")]
    public string? CongSuatW { get; set; }

    /// <summary>Công suất cắt</summary>
    [JsonPropertyName("congSuatCat")]
    public string? CongSuatCat { get; set; }

    /// <summary>Tổn hao không tải (KW)</summary>
    [JsonPropertyName("tonHaoKhongTaiKw")]
    public string? TonHaoKhongTaiKw { get; set; }

    /// <summary>Loại dầu</summary>
    [JsonPropertyName("loaiDau")]
    public string? LoaiDau { get; set; }

    /// <summary>Loại sứ cách điện</summary>
    [JsonPropertyName("loaiSuCachDien")]
    public string? LoaiSuCachDien { get; set; }

    /// <summary>Trọng lượng dầu (kg)</summary>
    [JsonPropertyName("trongLuongDauKg")]
    public string? TrongLuongDauKg { get; set; }

    /// <summary>Kiểu làm mát</summary>
    [JsonPropertyName("kieuLamMat")]
    public string? KieuLamMat { get; set; }

    /// <summary>Tiêu chuẩn áp dụng</summary>
    [JsonPropertyName("tieuChuanApDung")]
    public string? TieuChuanApDung { get; set; }

    /// <summary>Độ tăng nhiệt độ cực đại lớp dầu trên cùng</summary>
    [JsonPropertyName("doTangNhietDoCucDaiLopDauTrenCung")]
    public string? DoTangNhietDoCucDaiLopDauTrenCung { get; set; }

    /// <summary>Độ tăng nhiệt độ cực đại cuộn dây</summary>
    [JsonPropertyName("doTangNhietDoCucDaiCuonDay")]
    public string? DoTangNhietDoCucDaiCuonDay { get; set; }

    /// <summary>Khả năng quá tải</summary>
    [JsonPropertyName("khaNangQuaTai")]
    public string? KhaNangQuaTai { get; set; }

    /// <summary>Kích thước D, R, C (m)</summary>
    [JsonPropertyName("kichThuocDRCM")]
    public string? KichThuocDRCM { get; set; }

    /// <summary>Điện áp định mức</summary>
    [JsonPropertyName("dienApDinhMuc")]
    public string? DienApDinhMuc { get; set; }

    /// <summary>Điện áp định mức (kV)</summary>
    [JsonPropertyName("dienApDinhMucKv")]
    public string? DienApDinhMucKv { get; set; }

    /// <summary>Điện áp danh định (KV)</summary>
    [JsonPropertyName("dienApDanhDinhKv")]
    public string? DienApDanhDinhKv { get; set; }

    /// <summary>Điện áp (V)</summary>
    [JsonPropertyName("dienApV")]
    public string? DienApV { get; set; }

    /// <summary>Chủng loại</summary>
    [JsonPropertyName("chungLoai")]
    public string? ChungLoai { get; set; }

    /// <summary>Dòng điện xung (3s)</summary>
    [JsonPropertyName("dongDienXung3s")]
    public string? DongDienXung3s { get; set; }

    /// <summary>Số pha</summary>
    [JsonPropertyName("soPha")]
    public string? SoPha { get; set; }

    /// <summary>Số lưỡi tiếp địa</summary>
    [JsonPropertyName("soLuoiTiepDia")]
    public string? SoLuoiTiepDia { get; set; }

    /// <summary>Phân loại</summary>
    [JsonPropertyName("phanLoai")]
    public string? PhanLoai { get; set; }

    /// <summary>Loại dao</summary>
    [JsonPropertyName("loaiDao")]
    public string? LoaiDao { get; set; }

    /// <summary>Dòng điện định mức (A)</summary>
    [JsonPropertyName("dongDienDinhMucA")]
    public string? DongDienDinhMucA { get; set; }

    /// <summary>Dòng điện cắt định mức (A)</summary>
    [JsonPropertyName("dongDienCatDinhMucA")]
    public string? DongDienCatDinhMucA { get; set; }

    /// <summary>Dòng điện ngắn mạch định mức (A)</summary>
    [JsonPropertyName("dongDienNganMachDinhMucA")]
    public string? DongDienNganMachDinhMucA { get; set; }

    /// <summary>Môi trường cách điện</summary>
    [JsonPropertyName("moiTruongCachDien")]
    public string? MoiTruongCachDien { get; set; }

    /// <summary>Điện áp chịu đựng ở tần số công nghiệp (kV)</summary>
    [JsonPropertyName("dienApChiuDungOTanSoCongNghiepKv")]
    public string? DienApChiuDungOTanSoCongNghiepKv { get; set; }

    /// <summary>Dòng định mức cuộn bảo vệ</summary>
    [JsonPropertyName("dongDinhMucCuonBaoVe")]
    public string? DongDinhMucCuonBaoVe { get; set; }

    /// <summary>Dòng định mức cuộn đo lường</summary>
    [JsonPropertyName("dongDinhMucCuonDoLuong")]
    public string? DongDinhMucCuonDoLuong { get; set; }

    /// <summary>Dòng điện phía sơ cấp</summary>
    [JsonPropertyName("dongDienPhiaSoCap")]
    public string? DongDienPhiaSoCap { get; set; }

    /// <summary>Tổ đấu dây</summary>
    [JsonPropertyName("toDauDay")]
    public string? ToDauDay { get; set; }

    /// <summary>Loại chống sét</summary>
    [JsonPropertyName("loaiChongSet")]
    public string? LoaiChongSet { get; set; }

    /// <summary>Cấp chống sét</summary>
    [JsonPropertyName("capChongSet")]
    public string? CapChongSet { get; set; }

    /// <summary>Điện áp làm việc liên tục</summary>
    [JsonPropertyName("dienApLamViecLienTuc")]
    public string? DienApLamViecLienTuc { get; set; }

    /// <summary>Hạt nổ chống sét</summary>
    [JsonPropertyName("hatNoChongSet")]
    public string? HatNoChongSet { get; set; }

    /// <summary>Vật liệu vỏ ngoài</summary>
    [JsonPropertyName("vatLieuVoNgoai")]
    public string? VatLieuVoNgoai { get; set; }

    /// <summary>Kiểu Tụ</summary>
    [JsonPropertyName("kieuTu")]
    public string? KieuTu { get; set; }

    /// <summary>Dòng điện làm việc max</summary>
    [JsonPropertyName("dongDienLamViecMax")]
    public string? DongDienLamViecMax { get; set; }

    /// <summary>Điện dung tụ</summary>
    [JsonPropertyName("dienDungTu")]
    public string? DienDungTu { get; set; }

    /// <summary>Kiểu GIS (Type)</summary>
    [JsonPropertyName("kieuGisType")]
    public string? KieuGisType { get; set; }

    /// <summary>Udm (kV) (Rate voltage)</summary>
    [JsonPropertyName("udmKvRateVoltage")]
    public string? UdmKvRateVoltage { get; set; }

    /// <summary>Idm - Ngăn (A)</summary>
    [JsonPropertyName("idmNganA")]
    public string? IdmNganA { get; set; }

    /// <summary>Idm - Thanh cái (A)</summary>
    [JsonPropertyName("idmThanhCaiA")]
    public string? IdmThanhCaiA { get; set; }

    /// <summary>Idm - Thanh liên lạc (A)</summary>
    [JsonPropertyName("idmThanhLienLacA")]
    public string? IdmThanhLienLacA { get; set; }

    /// <summary>Inm định mức (kA)</summary>
    [JsonPropertyName("inmDinhMucKa")]
    public string? InmDinhMucKa { get; set; }

    /// <summary>Thời gian ngắn mạch định mức (s)</summary>
    [JsonPropertyName("thoiGianNganMachDinhMucS")]
    public string? ThoiGianNganMachDinhMucS { get; set; }

    /// <summary>Dòng điện đỉnh định mức (kA)</summary>
    [JsonPropertyName("dongDienDinhDinhMucKa")]
    public string? DongDienDinhDinhMucKa { get; set; }

    /// <summary>Áp suất khí cao (bar)</summary>
    [JsonPropertyName("apSuatKhiCaoBar")]
    public string? ApSuatKhiCaoBar { get; set; }

    /// <summary>Serial</summary>
    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    /// <summary>Năm sản xuất</summary>
    [JsonPropertyName("namSanXuat")]
    public string? NamSanXuat { get; set; }

    /// <summary>Dòng tải cực đại (A)</summary>
    [JsonPropertyName("dongTaiCucDaiA")]
    public string? DongTaiCucDaiA { get; set; }

    /// <summary>Dòng khởi động (A)</summary>
    [JsonPropertyName("dongKhoiDongA")]
    public string? DongKhoiDongA { get; set; }

    /// <summary>Tụt khí SF6</summary>
    [JsonPropertyName("tutKhiSf6")]
    public string? TutKhiSf6 { get; set; }

    /// <summary>Tải định mức (VA)</summary>
    [JsonPropertyName("taiDinhMucVa")]
    public string? TaiDinhMucVa { get; set; }

    /// <summary>Tải định mức</summary>
    [JsonPropertyName("taiDinhMuc")]
    public string? TaiDinhMuc { get; set; }

    /// <summary>Cấp cách điện</summary>
    [JsonPropertyName("capCachDien")]
    public string? CapCachDien { get; set; }

    /// <summary>Định mức/Chịu đựng NM tần số công nghiệp</summary>
    [JsonPropertyName("dinhMucChiuDungNmTanSoCongNghiep")]
    public string? DinhMucChiuDungNmTanSoCongNghiep { get; set; }

    /// <summary>Định mức chịu đựng xung sét (kV)</summary>
    [JsonPropertyName("dinhMucChiuDungXungSetKv")]
    public string? DinhMucChiuDungXungSetKv { get; set; }

    /// <summary>Cấp chính xác các cuộn dây</summary>
    [JsonPropertyName("capChinhXacCacCuonDay")]
    public string? CapChinhXacCacCuonDay { get; set; }

    /// <summary>Dải đo (%)</summary>
    [JsonPropertyName("daiDo")]
    public string? DaiDo { get; set; }

    /// <summary>Dải đo (%) (Measuring range)</summary>
    [JsonPropertyName("daiDoMeasuringRange")]
    public string? DaiDoMeasuringRange { get; set; }

    /// <summary>Cấp chính xác (1)</summary>
    [JsonPropertyName("capChinhXac1")]
    public string? CapChinhXac1 { get; set; }

    /// <summary>Cấp chính xác (2)</summary>
    [JsonPropertyName("capChinhXac2")]
    public string? CapChinhXac2 { get; set; }

    /// <summary>Cấp chính xác (3)</summary>
    [JsonPropertyName("capChinhXac3")]
    public string? CapChinhXac3 { get; set; }

    /// <summary>Cấp chính xác (4)</summary>
    [JsonPropertyName("capChinhXac4")]
    public string? CapChinhXac4 { get; set; }

    /// <summary>Cấp chính xác (5)</summary>
    [JsonPropertyName("capChinhXac5")]
    public string? CapChinhXac5 { get; set; }

    /// <summary>Tỉ số biến (1)</summary>
    [JsonPropertyName("tiSoBien1")]
    public string? TiSoBien1 { get; set; }

    /// <summary>Tỉ số biến (2)</summary>
    [JsonPropertyName("tiSoBien2")]
    public string? TiSoBien2 { get; set; }

    /// <summary>Tỉ số biến (3)</summary>
    [JsonPropertyName("tiSoBien3")]
    public string? TiSoBien3 { get; set; }

    /// <summary>Tỉ số biến (4)</summary>
    [JsonPropertyName("tiSoBien4")]
    public string? TiSoBien4 { get; set; }

    /// <summary>Tỉ số biến (5)</summary>
    [JsonPropertyName("tiSoBien5")]
    public string? TiSoBien5 { get; set; }

    /// <summary>Công suất định mức (1) (VA)</summary>
    [JsonPropertyName("congSuatDinhMuc1Va")]
    public string? CongSuatDinhMuc1Va { get; set; }

    /// <summary>Công suất định mức (2) (VA)</summary>
    [JsonPropertyName("congSuatDinhMuc2Va")]
    public string? CongSuatDinhMuc2Va { get; set; }

    /// <summary>Công suất định mức (3) (VA)</summary>
    [JsonPropertyName("congSuatDinhMuc3Va")]
    public string? CongSuatDinhMuc3Va { get; set; }

    /// <summary>Công suất định mức (4) (VA)</summary>
    [JsonPropertyName("congSuatDinhMuc4Va")]
    public string? CongSuatDinhMuc4Va { get; set; }

    /// <summary>Công suất định mức (5) (VA)</summary>
    [JsonPropertyName("congSuatDinhMuc5Va")]
    public string? CongSuatDinhMuc5Va { get; set; }

    /// <summary>Cấp cách điện định mức</summary>
    [JsonPropertyName("capCachDienDinhMuc")]
    public string? CapCachDienDinhMuc { get; set; }

    /// <summary>Tỉ số biến</summary>
    [JsonPropertyName("tiSoBien")]
    public string? TiSoBien { get; set; }

    /// <summary>Kiểu HGIS</summary>
    [JsonPropertyName("kieuHgis")]
    public string? KieuHgis { get; set; }

    /// <summary>Kiểu truyền động</summary>
    [JsonPropertyName("kieuTruyenDong")]
    public string? KieuTruyenDong { get; set; }

    /// <summary>Kiểu truyền động lưỡi dao</summary>
    [JsonPropertyName("kieuTruyenDongLuoiDao")]
    public string? KieuTruyenDongLuoiDao { get; set; }

    /// <summary>Dòng điện ổn định nhiệt khi ngắn mạch (kA)</summary>
    [JsonPropertyName("dongDienOnDinhNhietKhiNganMachKa")]
    public string? DongDienOnDinhNhietKhiNganMachKa { get; set; }

    /// <summary>Dòng điện ổn định động khi ngắn mạch (kA)</summary>
    [JsonPropertyName("dongDienOnDinhDongKhiNganMachKa")]
    public string? DongDienOnDinhDongKhiNganMachKa { get; set; }
}
