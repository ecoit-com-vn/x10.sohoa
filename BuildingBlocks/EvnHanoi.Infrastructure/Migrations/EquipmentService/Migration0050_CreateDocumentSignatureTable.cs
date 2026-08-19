using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Lịch sử ký số tài liệu (tích hợp API "ký số" ngoài — EVN HRMS/CA). Mỗi lần người dùng thực hiện
/// ký số 1 tài liệu trong hồ sơ tạo 1 dòng ở đây, dù thành công hay thất bại:
///  - Thành công: DocumentVersionId trỏ tới phiên bản MỚI (file đã ký) vừa được tạo trong
///    DOCUMENT_VERSIONS (UploadSource = 5 — xem Core/Entities/Document.cs).
///  - Thất bại: DocumentVersionId NULL — không có phiên bản mới nào được tạo, ErrorMessage lưu lại
///    lỗi trả về từ API sign-pdf-base64-image (objectError) để tra soát.
///
/// LƯU Ý ĐẶT TÊN: đã có sẵn bảng DOCUMENT_SIGNATURES từ 0013_CreateDocumentTables.sql (VERSION_ID
/// NOT NULL, SIGNER_NAME, SIGN_DATE, ISSUER, IS_VALID) — schema đó không có chỗ cho ký thất bại
/// (chưa có version mới) và thiếu SerialNumber/Status/ErrorMessage/SignerUserId cần cho tích hợp
/// này. Bảng đó hiện KHÔNG được code nào dùng (entity DocumentSignature trong Document.cs là dead
/// code), nhưng để tránh phá vỡ ngầm (CREATE TABLE trùng tên sẽ bị bắt ORA-00955 và bỏ qua — các
/// cột mới sẽ KHÔNG BAO GIỜ được tạo), migration này dùng tên bảng riêng DOCUMENT_SIGN_HISTORY thay
/// vì tái sử dụng/ALTER bảng cũ.
/// </summary>
public class Migration0050_CreateDocumentSignatureTable : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = @"
                CREATE TABLE DOCUMENT_SIGN_HISTORY (
                    Id                  VARCHAR2(36)   NOT NULL,
                    DocumentId          VARCHAR2(36)   NOT NULL,
                    DocumentVersionId   VARCHAR2(36)   NULL,
                    SignerUserId        VARCHAR2(100)  NULL,
                    SignerName          VARCHAR2(200)  NULL,
                    SerialNumber        VARCHAR2(200)  NULL,
                    SignedAt            TIMESTAMP      NULL,
                    Status              VARCHAR2(20)   DEFAULT 'Failed' NOT NULL,
                    ErrorMessage        VARCHAR2(2000) NULL,
                    RowVersion          NUMBER         DEFAULT 1 NOT NULL,
                    CreatedBy           VARCHAR2(100)  NULL,
                    CreatedDate         TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
                    ModifiedBy          VARCHAR2(100)  NULL,
                    ModifiedDate        TIMESTAMP      NULL,
                    IsDeleted           NUMBER(1)      DEFAULT 0 NOT NULL,
                    CONSTRAINT PK_DOCUMENT_SIGN_HISTORY PRIMARY KEY (Id),
                    CONSTRAINT FK_DOCUMENT_SIGN_HISTORY_DOC FOREIGN KEY (DocumentId)
                        REFERENCES DOCUMENTS(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_DOCUMENT_SIGN_HISTORY_VER FOREIGN KEY (DocumentVersionId)
                        REFERENCES DOCUMENT_VERSIONS(Id),
                    CONSTRAINT CK_DOCUMENT_SIGN_HISTORY_STATUS CHECK (Status IN ('Success', 'Failed')),
                    CONSTRAINT CK_DOCUMENT_SIGN_HISTORY_DELETED CHECK (IsDeleted IN (0, 1))
                )";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Bảng đã tồn tại.
        }

        return string.Empty;
    }
}
