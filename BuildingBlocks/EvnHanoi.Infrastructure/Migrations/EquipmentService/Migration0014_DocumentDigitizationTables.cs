using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// T4-P4 — Tiến trình OCR/Extraction theo worker DigitizationService (bảng riêng, không embed Document).
/// </summary>
public class Migration0014_DocumentDigitizationTables : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        void ExecuteNonQuery(string sql, params int[] ignoreErrorCodes)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                var ignored = false;
                foreach (var code in ignoreErrorCodes)
                {
                    if (ex.Message.Contains($"ORA-{code:D5}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-0{code}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-{code}", StringComparison.OrdinalIgnoreCase))
                    {
                        ignored = true;
                        break;
                    }
                }

                if (!ignored)
                    throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
            }
        }

        ExecuteNonQuery(@"
CREATE TABLE DOCUMENT_OCR_PROGRESS (
    ID VARCHAR2(36) PRIMARY KEY,
    DOCUMENT_ID VARCHAR2(36) NOT NULL,
    DOCUMENT_VERSION_ID VARCHAR2(36) NOT NULL,
    ACTION VARCHAR2(100),
    PHASE VARCHAR2(50) DEFAULT 'ocr' NOT NULL,
    CURRENT_PAGE NUMBER DEFAULT 0 NOT NULL,
    TOTAL_PAGES NUMBER DEFAULT 0 NOT NULL,
    PROGRESS NUMBER(3) DEFAULT 0 NOT NULL,
    STATUS VARCHAR2(50) DEFAULT 'Pending' NOT NULL,
    PROCESS_OPTION VARCHAR2(50),
    BUCKET_NAME VARCHAR2(255),
    FILE_PATH VARCHAR2(1000),
    FORM_JSON CLOB,
    ERROR_MESSAGE VARCHAR2(2000),
    CREATED_BY VARCHAR2(100),
    CREATED_DATE TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL,
    MODIFIED_BY VARCHAR2(100),
    MODIFIED_DATE TIMESTAMP,
    IS_DELETED NUMBER(1) DEFAULT 0 NOT NULL,
    CONSTRAINT FK_DOC_OCR_PROG_DOCUMENT FOREIGN KEY (DOCUMENT_ID) REFERENCES DOCUMENTS(ID),
    CONSTRAINT FK_DOC_OCR_PROG_VERSION FOREIGN KEY (DOCUMENT_VERSION_ID) REFERENCES DOCUMENT_VERSIONS(ID)
)", 955);

        ExecuteNonQuery("CREATE INDEX IDX_DOC_OCR_PROG_DOC_ID ON DOCUMENT_OCR_PROGRESS(DOCUMENT_ID)", 955, 1408);
        ExecuteNonQuery("CREATE INDEX IDX_DOC_OCR_PROG_VER_ID ON DOCUMENT_OCR_PROGRESS(DOCUMENT_VERSION_ID)", 955, 1408);
        ExecuteNonQuery("CREATE INDEX IDX_DOC_OCR_PROG_STATUS ON DOCUMENT_OCR_PROGRESS(STATUS)", 955, 1408);

        ExecuteNonQuery(@"
CREATE TABLE DOCUMENT_EXTRACTION_RESULTS (
    ID VARCHAR2(36) PRIMARY KEY,
    DOCUMENT_ID VARCHAR2(36) NOT NULL,
    DOCUMENT_VERSION_ID VARCHAR2(36) NOT NULL,
    OCR_PROGRESS_ID VARCHAR2(36),
    STATUS VARCHAR2(50) DEFAULT 'Pending' NOT NULL,
    RESULT_JSON CLOB,
    RESULT_FILE_PATH VARCHAR2(1000),
    BUCKET_NAME VARCHAR2(255),
    FORM_JSON CLOB,
    MERGED_DATA_JSON CLOB,
    ERROR_MESSAGE VARCHAR2(2000),
    CREATED_BY VARCHAR2(100),
    CREATED_DATE TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL,
    MODIFIED_BY VARCHAR2(100),
    MODIFIED_DATE TIMESTAMP,
    IS_DELETED NUMBER(1) DEFAULT 0 NOT NULL,
    CONSTRAINT FK_DOC_EXT_RES_DOCUMENT FOREIGN KEY (DOCUMENT_ID) REFERENCES DOCUMENTS(ID),
    CONSTRAINT FK_DOC_EXT_RES_VERSION FOREIGN KEY (DOCUMENT_VERSION_ID) REFERENCES DOCUMENT_VERSIONS(ID),
    CONSTRAINT FK_DOC_EXT_RES_OCR_PROG FOREIGN KEY (OCR_PROGRESS_ID) REFERENCES DOCUMENT_OCR_PROGRESS(ID)
)", 955);

        ExecuteNonQuery("CREATE INDEX IDX_DOC_EXT_RES_DOC_ID ON DOCUMENT_EXTRACTION_RESULTS(DOCUMENT_ID)", 955, 1408);
        ExecuteNonQuery("CREATE INDEX IDX_DOC_EXT_RES_VER_ID ON DOCUMENT_EXTRACTION_RESULTS(DOCUMENT_VERSION_ID)", 955, 1408);

        return string.Empty;
    }
}
