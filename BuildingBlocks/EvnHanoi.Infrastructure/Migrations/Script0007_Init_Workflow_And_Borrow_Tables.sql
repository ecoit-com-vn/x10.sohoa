-- Script0007: Tạo các bảng quản lý Quy trình (BPMN), Các bước duyệt (Workflow Steps) và Phiếu Mượn trả hồ sơ
-- Ngày tạo: 2026-05-29

BEGIN
    -- 1. Bảng WORKFLOW_DEFINITIONS
    EXECUTE IMMEDIATE '
        CREATE TABLE WorkflowDefinitions (
            Id             VARCHAR2(36)   NOT NULL PRIMARY KEY,
            Name           VARCHAR2(255)  NOT NULL,
            Description    VARCHAR2(1000) NULL,
            Version        VARCHAR2(50)   DEFAULT ''1.0'' NOT NULL,
            ForceActivate  NUMBER(1)      DEFAULT 0 NOT NULL,
            CreatedAt      TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
            UpdatedAt      TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
            IsActive       NUMBER(1)      DEFAULT 1 NOT NULL
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table WorkflowDefinitions created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    -- 2. Bảng WORKFLOW_STEPS
    EXECUTE IMMEDIATE '
        CREATE TABLE WorkflowSteps (
            Id                    VARCHAR2(36)   NOT NULL PRIMARY KEY,
            WorkflowDefinitionId  VARCHAR2(36)   NOT NULL,
            StepName              VARCHAR2(255)  NOT NULL,
            "Order"               NUMBER         NOT NULL,
            RequiredRole          VARCHAR2(100)  NOT NULL,
            ActionType            VARCHAR2(100)  NOT NULL,
            CONSTRAINT fk_wfstep_wfdef FOREIGN KEY (WorkflowDefinitionId) REFERENCES WorkflowDefinitions(Id) ON DELETE CASCADE
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table WorkflowSteps created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    -- 3. Bảng BORROW_RECORDS (Tương thích với BorrowRecord model của WorkflowService)
    EXECUTE IMMEDIATE '
        CREATE TABLE BorrowRecords (
            Id            VARCHAR2(36)   NOT NULL PRIMARY KEY,
            DossierId     VARCHAR2(50)   NOT NULL,
            RequesterId   VARCHAR2(50)   NOT NULL,
            Reason        VARCHAR2(1000) NOT NULL,
            State         VARCHAR2(50)   NOT NULL,
            RequestDate   TIMESTAMP      NOT NULL,
            ApprovedDate  TIMESTAMP      NULL,
            BorrowedDate  TIMESTAMP      NULL,
            ReturnedDate  TIMESTAMP      NULL
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table BorrowRecords created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/
