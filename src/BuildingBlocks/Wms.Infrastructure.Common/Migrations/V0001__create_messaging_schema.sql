-- Core messaging tables: transactional outbox and consumer inbox (LLD 2.7).

CREATE SCHEMA IF NOT EXISTS messaging;
GRANT USAGE ON SCHEMA messaging TO wms_app, wms_platform_jobs;

CREATE TABLE messaging.outbox_message (
  id           uuid         PRIMARY KEY,
  tenant_id    uuid,                         -- null for platform-level events
  type         varchar(100) NOT NULL,
  payload      jsonb        NOT NULL,
  occurred_at  timestamptz  NOT NULL,
  published_at timestamptz,
  attempts     integer      NOT NULL DEFAULT 0,
  last_error   text
);

-- Dispatcher reads oldest unpublished first.
CREATE INDEX ix_outbox_unpublished ON messaging.outbox_message (occurred_at)
  WHERE published_at IS NULL;

-- The runtime role only sees its own tenant's rows. The dispatcher runs as
-- wms_platform_jobs (BYPASSRLS) and is the only role allowed to mark rows published.
ALTER TABLE messaging.outbox_message ENABLE ROW LEVEL SECURITY;
ALTER TABLE messaging.outbox_message FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON messaging.outbox_message
  USING      (tenant_id IS NOT DISTINCT FROM NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id IS NOT DISTINCT FROM NULLIF(current_setting('app.tenant_id', true), '')::uuid);

REVOKE UPDATE, DELETE ON messaging.outbox_message FROM wms_app;
GRANT SELECT, UPDATE, DELETE ON messaging.outbox_message TO wms_platform_jobs;

-- Consumer idempotency: one row per (consumer, event) handled.
CREATE TABLE messaging.inbox_message (
  consumer     varchar(100) NOT NULL,
  event_id     uuid         NOT NULL,
  processed_at timestamptz  NOT NULL DEFAULT now(),
  PRIMARY KEY (consumer, event_id)
);
