-- Migration: Add CreatedBy and UpdatedBy to WorkflowDefinitions
ALTER TABLE WorkflowDefinitions ADD CreatedBy VARCHAR2(100) DEFAULT 'System' NOT NULL;
ALTER TABLE WorkflowDefinitions ADD UpdatedBy VARCHAR2(100) DEFAULT 'System' NOT NULL;
