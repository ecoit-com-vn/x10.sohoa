-- Script0006: Tạo các bảng cho thiết bị, thuộc tính EAV động, hồ sơ và tài liệu đính kèm
-- Ngày tạo: 2026-05-29

BEGIN
    -- 1. Bảng EQUIPMENTTYPES
    EXECUTE IMMEDIATE '
        CREATE TABLE EquipmentTypes (
            Id           VARCHAR2(36)   NOT NULL PRIMARY KEY,
            Name         VARCHAR2(255)  NOT NULL,
            Code         VARCHAR2(100)  NOT NULL UNIQUE,
            Description  VARCHAR2(1000) NULL,
            CreatedAt    TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
            UpdatedAt    TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table EquipmentTypes created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    -- 2. Bảng ATTRIBUTEDEFINITIONS
    EXECUTE IMMEDIATE '
        CREATE TABLE AttributeDefinitions (
            Id                VARCHAR2(36)   NOT NULL PRIMARY KEY,
            EquipmentTypeId   VARCHAR2(36)   NOT NULL,
            Name              VARCHAR2(255)  NOT NULL,
            Code              VARCHAR2(100)  NOT NULL,
            DataType          VARCHAR2(50)   NOT NULL,
            IsRequired        NUMBER(1)      DEFAULT 0 NOT NULL,
            CONSTRAINT fk_attrdef_eqtype FOREIGN KEY (EquipmentTypeId) REFERENCES EquipmentTypes(Id) ON DELETE CASCADE
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table AttributeDefinitions created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    -- 3. Bảng EQUIPMENTS
    EXECUTE IMMEDIATE '
        CREATE TABLE Equipments (
            Id               VARCHAR2(36)   NOT NULL PRIMARY KEY,
            EquipmentTypeId  VARCHAR2(36)   NOT NULL,
            Name             VARCHAR2(255)  NOT NULL,
            Code             VARCHAR2(100)  NOT NULL UNIQUE,
            SerialNumber     VARCHAR2(100)  NULL,
            CreatedAt        TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
            CreatedBy        VARCHAR2(100)  NULL,
            RowVersion       NUMBER         DEFAULT 1 NOT NULL,
            CONSTRAINT fk_equip_eqtype FOREIGN KEY (EquipmentTypeId) REFERENCES EquipmentTypes(Id)
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table Equipments created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    -- 4. Bảng ATTRIBUTEVALUES
    EXECUTE IMMEDIATE '
        CREATE TABLE AttributeValues (
            Id                     VARCHAR2(36)   NOT NULL PRIMARY KEY,
            EquipmentId            VARCHAR2(36)   NOT NULL,
            AttributeDefinitionId  VARCHAR2(36)   NOT NULL,
            Value                  CLOB           NULL,
            CONSTRAINT fk_attrval_equip FOREIGN KEY (EquipmentId) REFERENCES Equipments(Id) ON DELETE CASCADE,
            CONSTRAINT fk_attrval_def FOREIGN KEY (AttributeDefinitionId) REFERENCES AttributeDefinitions(Id) ON DELETE CASCADE
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table AttributeValues created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    -- 5. Bảng DOSSIERS
    EXECUTE IMMEDIATE '
        CREATE TABLE Dossiers (
            Id           VARCHAR2(36)   NOT NULL PRIMARY KEY,
            EquipmentId  VARCHAR2(36)   NULL,
            Title        VARCHAR2(255)  NOT NULL,
            Description  VARCHAR2(1000) NULL,
            Status       VARCHAR2(50)   DEFAULT ''Draft'' NOT NULL,
            Version      NUMBER         DEFAULT 1 NOT NULL,
            CreatedAt    TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
            CreatedBy    VARCHAR2(100)  NULL,
            UpdatedAt    TIMESTAMP      NULL,
            UpdatedBy    VARCHAR2(100)  NULL,
            RowVersion   NUMBER         DEFAULT 1 NOT NULL,
            CONSTRAINT fk_dossier_equip FOREIGN KEY (EquipmentId) REFERENCES Equipments(Id) ON DELETE SET NULL
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table Dossiers created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    -- 6. Bảng DOSSIERVERSIONS
    EXECUTE IMMEDIATE '
        CREATE TABLE DossierVersions (
            Id             VARCHAR2(36)   NOT NULL PRIMARY KEY,
            DossierId      VARCHAR2(36)   NOT NULL,
            VersionNumber  NUMBER         NOT NULL,
            Title          VARCHAR2(255)  NOT NULL,
            Description    VARCHAR2(1000) NULL,
            Status         VARCHAR2(50)   NOT NULL,
            ChangeLog      VARCHAR2(1000) NULL,
            CreatedAt      TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
            CreatedBy      VARCHAR2(100)  NULL,
            CONSTRAINT fk_dossver_dossier FOREIGN KEY (DossierId) REFERENCES Dossiers(Id) ON DELETE CASCADE
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table DossierVersions created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    -- 7. Bảng FILE_ATTACHMENT
    EXECUTE IMMEDIATE '
        CREATE TABLE FILE_ATTACHMENT (
            ID            NUMBER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            FILE_NAME     VARCHAR2(500)  NOT NULL,
            FILE_PATH     VARCHAR2(2000) NOT NULL,
            CONTENT_TYPE  VARCHAR2(100)  NOT NULL,
            FILE_SIZE     NUMBER         NOT NULL,
            UPLOADED_AT   TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
            UPLOADED_BY   VARCHAR2(255)  NOT NULL,
            STATUS        VARCHAR2(50)   DEFAULT ''Uploaded'' NOT NULL
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table FILE_ATTACHMENT created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    -- 8. Bảng DIGITIZATION_TASK
    EXECUTE IMMEDIATE '
        CREATE TABLE DIGITIZATION_TASK (
            ID                    VARCHAR2(36)   NOT NULL PRIMARY KEY,
            DOSSIER_ID            VARCHAR2(50)   NOT NULL,
            WORKFLOW_STEP_ID      VARCHAR2(36)   NOT NULL,
            ASSIGNED_TO_USER_ID   VARCHAR2(50)   NOT NULL,
            STATUS                VARCHAR2(50)   NOT NULL,
            CREATED_AT            TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
            COMPLETED_AT          TIMESTAMP      NULL,
            NOTES                 VARCHAR2(2000) NULL
        )
    ';
    DBMS_OUTPUT.PUT_LINE('Table DIGITIZATION_TASK created.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/
