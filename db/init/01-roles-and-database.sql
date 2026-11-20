-- Runs once when the local PostgreSQL container is first created.
-- Dev-only passwords. Real environments get credentials from AWS Secrets Manager.

-- Owns schemas and runs migrations.
CREATE ROLE wms_owner LOGIN PASSWORD 'wms_owner_dev';

-- Runtime role used by all hosts. Subject to row-level security.
CREATE ROLE wms_app LOGIN PASSWORD 'wms_app_dev' NOBYPASSRLS;

-- Used only by a few audited Worker jobs (tenant export, cross-tenant maintenance).
CREATE ROLE wms_platform_jobs LOGIN PASSWORD 'wms_jobs_dev' BYPASSRLS;

CREATE DATABASE wms OWNER wms_owner;

\connect wms

-- Tables created later by wms_owner are readable and writable by the runtime role.
-- Append-only tables (ledger, audit) revoke UPDATE/DELETE in their own migrations.
ALTER DEFAULT PRIVILEGES FOR ROLE wms_owner
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO wms_app;
ALTER DEFAULT PRIVILEGES FOR ROLE wms_owner
  GRANT USAGE, SELECT ON SEQUENCES TO wms_app;
ALTER DEFAULT PRIVILEGES FOR ROLE wms_owner
  GRANT SELECT ON TABLES TO wms_platform_jobs;

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO wms_owner;
