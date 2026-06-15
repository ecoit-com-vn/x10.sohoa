-- Migration to alter EquipmentTypes and create GridTypes table

CREATE TABLE GridTypes (
    Id NUMBER NOT NULL PRIMARY KEY,
    Name VARCHAR2(100) NOT NULL
);

INSERT INTO GridTypes (Id, Name) VALUES (1, 'Cao áp');
INSERT INTO GridTypes (Id, Name) VALUES (2, 'Trung áp');

-- Alter table EquipmentTypes to add new fields
ALTER TABLE EquipmentTypes ADD (
    GridTypeId NUMBER DEFAULT 1 NOT NULL,
    SortOrder NUMBER NULL,
    IsActive NUMBER(1) DEFAULT 1 NOT NULL,
    CreatorId VARCHAR2(36) NULL,
    CreatedBy VARCHAR2(100) NULL,
    ModifiedBy VARCHAR2(100) NULL,
    IsDeleted NUMBER(1) DEFAULT 0 NOT NULL
);

-- Add foreign key constraints
ALTER TABLE EquipmentTypes ADD CONSTRAINT fk_eqtype_grid FOREIGN KEY (GridTypeId) REFERENCES GridTypes(Id);
ALTER TABLE EquipmentTypes ADD CONSTRAINT fk_eqtype_creator FOREIGN KEY (CreatorId) REFERENCES APP_USER(Id) ON DELETE SET NULL;
