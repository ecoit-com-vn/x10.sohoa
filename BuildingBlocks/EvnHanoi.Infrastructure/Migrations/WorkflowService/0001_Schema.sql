-- Consolidated Workflow Service Schema

CREATE TABLE WorkflowDefinitions (
    Id             VARCHAR2(36)   NOT NULL PRIMARY KEY,
    Name           VARCHAR2(255)  NOT NULL,
    Description    VARCHAR2(1000) NULL,
    Version        VARCHAR2(50)   DEFAULT '1.0' NOT NULL,
    ForceActivate  NUMBER(1)      DEFAULT 0 NOT NULL,
    CreatedAt      TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    UpdatedAt      TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    IsActive       NUMBER(1)      DEFAULT 1 NOT NULL
);

CREATE TABLE WorkflowSteps (
    Id                    VARCHAR2(36)   NOT NULL PRIMARY KEY,
    WorkflowDefinitionId  VARCHAR2(36)   NOT NULL,
    StepName              VARCHAR2(255)  NOT NULL,
    "Order"               NUMBER         NOT NULL,
    RequiredRole          VARCHAR2(100)  NOT NULL,
    ActionType            VARCHAR2(100)  NOT NULL,
    CONSTRAINT fk_wfstep_wfdef FOREIGN KEY (WorkflowDefinitionId) REFERENCES WorkflowDefinitions(Id) ON DELETE CASCADE
);

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
);
