-- Migration 0009: Redesign DOSSIERS table + create DOSSIER_SETS and DOSSIER_EQUIPMENTS
-- Drop old seed data first
DELETE FROM Dossiers;

-- Drop old Dossiers table (recreate with new schema)
DROP TABLE DOSSIER_VERSIONS;
DROP TABLE Dossiers;

-- Create DOSSIER_SETS table (Bộ/Gói hồ sơ lớn)
CREATE TABLE DOSSIER_SETS (
    Id              VARCHAR2(36)   NOT NULL PRIMARY KEY,
    Code            VARCHAR2(100)  NOT NULL UNIQUE,
    Name            VARCHAR2(255)  NOT NULL,
    UnitId          NUMBER         NULL,
    CreatedBy       VARCHAR2(100)  NULL,
    CreatedDate     TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    ModifiedBy      VARCHAR2(100)  NULL,
    ModifiedDate    TIMESTAMP      NULL,
    IsDeleted       NUMBER(1)      DEFAULT 0 NOT NULL
);

-- Create DOSSIERS table (redesigned)
CREATE TABLE DOSSIERS (
    Id                  VARCHAR2(36)   NOT NULL PRIMARY KEY,
    GridTypeId          NUMBER         NULL,
    InfrastructureId    VARCHAR2(36)   NULL,
    DossierSetId        VARCHAR2(36)   NULL,
    DossierTypeId       VARCHAR2(36)   NOT NULL,
    FormDataJson        CLOB           NULL,
    Status              VARCHAR2(50)   DEFAULT 'Draft' NOT NULL,
    WorkflowInstanceId  VARCHAR2(36)   NULL,
    WorkflowStatusName  VARCHAR2(100)  NULL,
    RowVersion          NUMBER         DEFAULT 1 NOT NULL,
    CreatorId           VARCHAR2(36)   NULL,
    CreatorUsername     VARCHAR2(100)  NULL,
    CreatorName         VARCHAR2(255)  NULL,
    CreatedBy           VARCHAR2(100)  NULL,
    CreatedDate         TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    ModifiedBy          VARCHAR2(100)  NULL,
    ModifiedDate        TIMESTAMP      NULL,
    IsDeleted           NUMBER(1)      DEFAULT 0 NOT NULL,
    CONSTRAINT fk_dossier_set    FOREIGN KEY (DossierSetId)    REFERENCES DOSSIER_SETS(Id) ON DELETE SET NULL,
    CONSTRAINT fk_dossier_type   FOREIGN KEY (DossierTypeId)   REFERENCES DOSSIER_TYPES(Id),
    CONSTRAINT fk_dossier_infra  FOREIGN KEY (InfrastructureId) REFERENCES INFRASTRUCTURE(ID) ON DELETE SET NULL
);

-- Create DOSSIER_EQUIPMENTS junction table (M:N Dossier <-> Equipment)
CREATE TABLE DOSSIER_EQUIPMENTS (
    DossierId    VARCHAR2(36)  NOT NULL,
    EquipmentId  VARCHAR2(36)  NOT NULL,
    CONSTRAINT pk_dossier_equip PRIMARY KEY (DossierId, EquipmentId),
    CONSTRAINT fk_de_dossier    FOREIGN KEY (DossierId)   REFERENCES DOSSIERS(Id) ON DELETE CASCADE,
    CONSTRAINT fk_de_equip      FOREIGN KEY (EquipmentId) REFERENCES Equipments(Id) ON DELETE CASCADE
);

-- Recreate DOSSIER_VERSIONS with new schema (snapshot of FormDataJson per version)
CREATE TABLE DOSSIER_VERSIONS (
    Id              VARCHAR2(36)   NOT NULL PRIMARY KEY,
    DossierId       VARCHAR2(36)   NOT NULL,
    VersionNumber   NUMBER         NOT NULL,
    FormDataJson    CLOB           NULL,
    ChangeNote      VARCHAR2(1000) NULL,
    CreatedBy       VARCHAR2(100)  NULL,
    CreatedDate     TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    CONSTRAINT fk_dossver_dossier FOREIGN KEY (DossierId) REFERENCES DOSSIERS(Id) ON DELETE CASCADE
);
