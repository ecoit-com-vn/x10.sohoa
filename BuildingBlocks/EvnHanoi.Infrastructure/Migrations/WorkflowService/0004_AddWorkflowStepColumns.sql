-- Migration: Add AllowEdit and RequireSignature to WorkflowSteps
ALTER TABLE WorkflowSteps ADD AllowEdit NUMBER(1) DEFAULT 0 NOT NULL;
ALTER TABLE WorkflowSteps ADD RequireSignature NUMBER(1) DEFAULT 0 NOT NULL;
