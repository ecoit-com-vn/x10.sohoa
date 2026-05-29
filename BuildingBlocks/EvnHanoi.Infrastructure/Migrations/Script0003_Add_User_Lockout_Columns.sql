-- Script0003_Add_User_Lockout_Columns.sql

ALTER TABLE APP_USER ADD (
    AccessFailedCount NUMBER(5) DEFAULT 0 NOT NULL,
    LockoutEnd TIMESTAMP NULL,
    LockoutEnabled NUMBER(1) DEFAULT 1 NOT NULL
);
