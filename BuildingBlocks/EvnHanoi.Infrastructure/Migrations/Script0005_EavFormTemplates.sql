-- Script0005: Tạo bảng EavFormTemplates để quản lý biểu mẫu thuộc tính EAV động
-- Ngày tạo: 2026-05-29

CREATE TABLE EavFormTemplates (
    Id           VARCHAR2(36)  NOT NULL PRIMARY KEY,
    Name         VARCHAR2(255) NOT NULL,
    Description  VARCHAR2(1000) NULL,
    "Schema"     CLOB          NOT NULL,
    Version      NUMBER        DEFAULT 1 NOT NULL,
    IsActive     NUMBER(1)     DEFAULT 1 NOT NULL,
    CreatedAt    TIMESTAMP     DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CreatedBy    VARCHAR2(50)  NULL
);
