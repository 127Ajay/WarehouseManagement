-- Platform module schema. Tables arrive with US-001 (tenant, plan, warehouse, client)
-- and US-006 (feature_setting).

CREATE SCHEMA IF NOT EXISTS platform;
GRANT USAGE ON SCHEMA platform TO wms_app, wms_platform_jobs;
