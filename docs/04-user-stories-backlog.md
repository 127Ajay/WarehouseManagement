# WMS SaaS: Product Backlog, Epics and User Stories

| Field | Value |
|---|---|
| Version | 1.0 |
| Date | 2026-11-21 |
| Phase | 2. Product Owner / BA, step 3 of 3 |
| Sources | `02-prd.md`, `03-functional-spec.md` |
| Scope | Release 1.0 fully refined (Sprints 1 to 12). Release 1.1 and 1.2 listed for roadmap, refined later. |

> **Solo build note:** the sprint numbers in this file assume a 7-person team. The active plan is the solo re-plan in `08-tech-lead-sprint-01-plan.md` (about 20 points per sprint, 20 sprints). US-066, US-082, and the email part of US-081 moved to Release 1.1.

## How to read this

- **US-xxx** = user story. **EN-xx** = technical enabler (no direct user value, needed by stories). **SP-xx** = spike (time-boxed investigation).
- **Points** use Fibonacci (1, 2, 3, 5, 8). 8 is the max; anything bigger was split.
- **Refs** link to the Functional Spec (FR-xxx).
- **Flag / Perm** name the feature flag and permission the story must wire up (DoD item).
- Every story inherits the PRD Definition of Done. Acceptance criteria below cover only story-specific behaviour.
- Capacity assumption: 30 to 35 points per sprint.

## Sprint summary

| Sprint | Theme | Points |
|---|---|---|
| 1 | Platform foundation and login | 33 |
| 2 | Access control, config, item master | 34 |
| 3 | Locations, Edge Agent, handheld shell | 33 |
| 4 | Inventory core and ledger | 33 |
| 5 | Integration API and receiving | 33 |
| 6 | GRN, webhooks, QC, putaway, inventory control | 34 |
| 7 | Orders, allocation, discrete picking | 32 |
| 8 | Batch picking, short picks, offline, packing | 32 |
| 9 | Shipping, e-way bill, Integration Monitor, SFTP | 32 |
| 10 | Returns, cycle counting, replenishment | 33 |
| 11 | Traceability, recall, reports, dashboards | 34 |
| 12 | Hardening, notifications, onboarding, UAT | 31 |
| **Total** | | **394** |

---

## Sprint 1: Platform foundation and login (33)

**Sprint goal:** The platform runs in AWS. A tenant can be created, a user can log in with MFA, and a module flag can be toggled. Handheld latency risk is measured.

#### EN-01 Solution skeleton, CI/CD and environments (8)
Epic E01. Refs: NFR-100, NFR-103.
- AC1. Given a commit to `main`, when CI runs, then it builds, runs unit tests, runs architecture tests (module boundary rules), and fails on any violation.
- AC2. Given a green build, when the pipeline completes, then it deploys automatically to Dev; deploy to Test needs one approval.
- AC3. Given the infrastructure code, when applied to a fresh AWS account, then Dev and Test environments are created with no manual steps.
- AC4. Given the solution, when opened, then it contains the module template (Domain, Application, Infrastructure, API) agreed with the Architect, and one sample module using it.

#### SP-01 Spike: handheld scan latency on warehouse Wi-Fi (3)
Epic E19. Refs: NFR-010, PRD risk R-03.
- AC1. Given a MAUI test app on a Zebra device, when it calls a test API in ap-south-1 from a real warehouse Wi-Fi (or simulated 50 ms RTT, 2% loss), then p50, p95, p99 round-trip times for 1,000 scans are recorded.
- AC2. Given the results, when reviewed, then a written recommendation states whether 300 ms p95 is achievable and what is needed (payload size, connection reuse, regional edge).

#### US-001 Create a tenant (5)
As a Platform Super Admin, I want to create a tenant with a plan, so that a new customer can start using the system.
Refs: FR-PLT-01, 02. Perm: `Tenant.Manage` (platform).
- AC1. Given valid code, name, subdomain, plan, region, when I create the tenant, then it is Active, default role templates and config are seeded, and the first Tenant Admin receives an invite email.
- AC2. Given a subdomain already in use or not DNS-safe, when I submit, then I see "Subdomain unavailable" and nothing is created.
- AC3. Given an Active tenant, when I set it to Suspended, then its users cannot log in and its API calls return 403 `TENANT_SUSPENDED`.

#### US-002 Resolve tenant from subdomain (3)
As a user, I want to reach my company's workspace through its own subdomain, so that I only see my company's data.
Refs: FR-PLT-06.
- AC1. Given `acme.app-domain`, when I open it, then the Acme login page shows Acme's name.
- AC2. Given a token issued for tenant A, when it is used on tenant B's subdomain or API, then the request is rejected with 401 and a security event is logged.
- AC3. Given an unknown subdomain, when opened, then a generic "Workspace not found" page shows, with no tenant names revealed.

#### US-003 Invite users (3)
As a Tenant Admin, I want to invite users by email, so that my staff can get accounts.
Refs: FR-IAM-01. Perm: `User.Manage`.
- AC1. Given a valid email not already used in my tenant, when I invite, then the user is created as Invited and gets an email with a link valid for 72 hours.
- AC2. Given the link is expired, when the user opens it, then they see "Invite expired, ask your admin to resend".
- AC3. Given an Invited user, when I resend, then the old link stops working.

#### US-004 Web login with password policy (3)
As a web user, I want to log in securely, so that my account cannot be easily misused.
Refs: FR-IAM-02, FR-IAM-07.
- AC1. Given a password under 12 characters or on the breached-password list, when I set it, then it is rejected with the reason.
- AC2. Given 5 failed attempts, when I try a 6th time within 15 minutes, then the account is locked and the attempt is logged.
- AC3. Given I am idle for 30 minutes, when I click anything, then I am sent to the login page.

#### US-005 TOTP MFA (3)
As a Tenant Admin, I want MFA on privileged accounts, so that a stolen password is not enough to log in.
Refs: FR-IAM-03.
- AC1. Given my role is Tenant Admin, Warehouse Manager, or Inventory Controller, when I first log in, then I must enrol an authenticator app before continuing.
- AC2. Given MFA is enrolled, when I log in with a correct password and wrong code, then access is denied and the failure is logged.
- AC3. Given tenant config `security.mfaAllWebUsers = true`, when any web user logs in, then MFA enrolment is required.

#### US-006 Feature flag framework (5)
As a Tenant Admin, I want to switch modules on and off within my plan, so that we only use what we need.
Refs: FR-PLT-10 to 13. Perm: `Feature.Manage`.
- AC1. Given a module not in my plan, when I view features, then it shows as "Not in plan" and cannot be switched on.
- AC2. Given I switch off `module.returns`, when a user opens the app, then Returns menus are hidden and Returns APIs return 403 `MODULE_DISABLED` within 60 seconds.
- AC3. Given a flag supports client override, when I switch it off for Client X only, then it stays on for other clients.
- AC4. Given open work exists for a module, when I try to switch it off, then I see a list of the blocking items and the change is refused.
- AC5. Given any flag change, when saved, then it appears in the security audit log.

---

## Sprint 2: Access control, config, item master (34)

**Sprint goal:** Admins can set up warehouses, clients, scoped roles, config, and import items.

#### US-007 Manage warehouses and clients (3)
As a Tenant Admin, I want to create warehouses and clients, so that I can model my operation.
Refs: FR-PLT-04, 05. Perm: `Warehouse.Manage`, `Client.Manage`.
- AC1. Given my plan allows 5 warehouses and I have 5, when I add a 6th, then I see "Plan limit reached".
- AC2. Given a client, when I link it to warehouses, then only those warehouses accept its documents.
- AC3. Given the tenant is in-house type, when I open any screen, then the client selector is hidden and the default client is used.

#### US-016 Upload tenant logo (1)
As a Tenant Admin, I want our logo on screens and documents, so that the product looks like ours.
Refs: FR-PLT-03.
- AC1. Given a PNG or SVG up to 500 KB, when I upload, then it shows in the web header and on printed GRNs and packing slips.
- AC2. Given a larger file or other format, when I upload, then it is rejected with the reason.

#### US-008 Role templates and custom roles (5)
As a Tenant Admin, I want ready-made roles that I can copy and adjust, so that I can set up access quickly.
Refs: FR-IAM-08, Appendix A. Perm: `Role.Manage`.
- AC1. Given a new tenant, when I open Roles, then all 14 templates exist and are read-only.
- AC2. Given a template, when I clone it, then I get an editable copy with the same permissions.
- AC3. Given a module is switched off, when I edit a role, then that module's permissions are not shown.

#### US-009 Scoped role assignment (5)
As a Tenant Admin, I want to give a role only for certain warehouses, clients, or zones, so that people only touch what they are responsible for.
Refs: FR-IAM-09, FR-GEN-07.
- AC1. Given a user has Picker for Warehouse BOM1 Zone A, when they request pick tasks, then only Zone A tasks in BOM1 are returned.
- AC2. Given a user has Report Viewer for Client X only, when they run Stock on hand, then no other client's rows appear, including in exports.
- AC3. Given a user calls an API for a warehouse outside scope, when processed, then the response is 403 and nothing is leaked in the error.

#### US-010 Separation of duties (2)
As a Warehouse Manager, I want the system to stop self-approval, so that controls hold up in an audit.
Refs: FR-IAM-10.
- AC1. Given I raised an adjustment, when I try to approve it, then I get "You cannot approve your own request".
- AC2. Given I try to change my own roles, when I save, then it is refused.

#### US-011 Security audit log (3)
As a Tenant Admin, I want a log of security events, so that I can investigate issues.
Refs: FR-IAM-12. Perm: `Audit.View`.
- AC1. Given logins, failures, role changes, device changes, flag and config changes occur, when I open the log, then each shows time, user, event, details, IP or device.
- AC2. Given the log, when anyone tries to edit or delete entries via UI or API, then no such operation exists.
- AC3. Given filters by date, user, event type, when applied, then results return in under 3 seconds for 1 year of data.

#### US-012 Config resolution engine (5)
As a Tenant Admin, I want settings at tenant, warehouse, client, and SKU level, so that each client's rules apply automatically.
Refs: FR-CFG-01, 02, 04.
- AC1. Given `inbound.qcRequired` is false at tenant and true for Client X, when Client X stock is received, then QC is required; for Client Y it is not.
- AC2. Given a SKU-level value exists, when resolved, then it overrides client, warehouse, and tenant values.
- AC3. Given an ASN started receiving under old rules, when config changes mid-way, then that ASN keeps the old rule.
- AC4. Given a value outside allowed values, when saved, then it is rejected.

#### US-013 Config screen (2)
As a Tenant Admin, I want to see which value applies and where it comes from, so that I can understand behaviour.
Refs: FR-CFG-03, 05. Perm: `Config.Manage`.
- AC1. Given a key, when I view it for a client, then I see the effective value and its source level.
- AC2. Given I change a value, when saved, then the audit log shows old value, new value, user, time.

#### US-014 Item master with UOM and tracking (5)
As an Inventory Controller, I want to set up items with pack sizes and tracking rules, so that receiving and picking work correctly.
Refs: FR-ITM-01, 05. Perm: `Item.Manage`.
- AC1. Given an item with base EA, Case = 12 EA, Pallet = 40 Case, when saved, then conversions are available in all qty fields.
- AC2. Given Expiry tracked = true and no shelf life days, when saved, then it is rejected.
- AC3. Given an item with stock, when I try to change a tracking flag, then it is refused with "Item has stock".
- AC4. Given a barcode already used for another item of the same client, when saved, then it is rejected.
- AC5. Given I search by any barcode, when submitted, then the matching item and UOM are found.

#### US-015 Item Excel import (3)
As a Tenant Admin, I want to import items from Excel, so that onboarding a client takes hours, not days.
Refs: FR-ITM-02, 03. Perm: `Item.Import`.
- AC1. Given the template with 5,000 valid rows, when imported, then all items are created within 60 seconds.
- AC2. Given "validate only" mode, when run, then nothing is saved and a result file lists errors per row.
- AC3. Given some invalid rows in import mode, when run, then no items are created and the error file is returned (master data import is all-or-nothing).

---

## Sprint 3: Locations, Edge Agent, handheld shell (33)

**Sprint goal:** A warehouse layout can be built and labelled in under an hour. Operators can log in on a Zebra handheld.

#### US-017 Create and edit locations (3)
As an Inventory Controller, I want to define locations with type, capacity, and mixing rules, so that stock goes to the right place.
Refs: FR-LOC-01. Perm: `Location.Manage`.
- AC1. Given type Quarantine, when created, then allowed statuses default to Quarantine and QC Hold.
- AC2. Given a duplicate code in the same warehouse, when saved, then it is rejected.

#### US-018 Bulk create locations by pattern (5)
As a Tenant Admin, I want to generate thousands of locations from a pattern, so that setup is fast.
Refs: FR-LOC-02.
- AC1. Given pattern `A-{01..20}-{01..10}-{A..E}`, when I preview, then I see 1,000 and the first 20 codes.
- AC2. Given I confirm, when processed, then 1,000 locations are created with the chosen type and attributes in under 30 seconds.
- AC3. Given a pattern that would exceed 20,000 or collide with existing codes, when previewed, then the problem is shown and commit is disabled.

#### US-019 Location import, export, bulk edit (3)
As an Inventory Controller, I want to edit many locations at once, so that layout changes are quick.
Refs: FR-LOC-03, 04.
- AC1. Given a filtered set of 300 locations, when I set velocity class = A, then all 300 update.
- AC2. Given an export, when re-imported unchanged, then nothing changes and no errors occur.

#### US-020 Block and unblock a location (1)
As a Warehouse Manager, I want to block a damaged rack, so that nobody uses it.
Refs: FR-LOC-06. Perm: `Location.Block`.
- AC1. Given a blocked location with stock, when putaway or allocation runs, then it is skipped, and the stock is still visible in enquiry.
- AC2. Given block without a reason, when saved, then it is rejected.

#### US-021 Edge Agent registration and connection (5)
As a Tenant Admin, I want to install the Edge Agent at each warehouse, so that local printers work with the cloud.
Refs: FR-HHD-20, 24.
- AC1. Given a one-time token from the web app, when the agent is installed with it, then it shows Online for that warehouse within 30 seconds.
- AC2. Given the agent uses only outbound connections, when the site firewall blocks all inbound ports, then it still works.
- AC3. Given the agent is offline for over 2 minutes, when detected, then the web shows Offline and an alert is raised.

#### US-022 Printer setup and location label printing (3)
As an Inventory Controller, I want to print location labels in bulk, so that the floor is labelled before go-live.
Refs: FR-HHD-21, 22, FR-LOC-05. Perm: `Label.Print`.
- AC1. Given a ZPL printer registered on the agent, when I print 500 location labels, then they print in pick-sequence order with code as barcode.
- AC2. Given the internet drops after the job reached the agent, when printing, then all labels still print and job status syncs later.

#### US-023 Handheld app shell and device registration (5)
As a Warehouse Manager, I want to register handhelds to my warehouse, so that only approved devices are used.
Refs: FR-IAM-05, 06, FR-HHD-06, 07. Perm: `Device.Manage`.
- AC1. Given a one-time QR from the web, when scanned by the new device, then the device is Active for that warehouse.
- AC2. Given I revoke a device, when 60 seconds pass, then its session ends and logins are refused.
- AC3. Given a user logs in, when the home screen loads, then only menus for their permissions in this warehouse appear.
- AC4. Given any screen, when inspected, then all text comes from resource files (no hard-coded strings).

#### US-024 Badge and PIN login (3)
As a Picker, I want to log in by scanning my badge and entering a PIN, so that I can start work in seconds.
Refs: FR-IAM-04, 07.
- AC1. Given a valid badge and PIN, when entered, then I reach the home screen in under 5 seconds.
- AC2. Given 5 wrong PINs, when I try again, then the badge is locked until a supervisor resets it.
- AC3. Given 10 minutes idle, when I touch the screen, then I must enter my PIN; after 12 hours I am logged out.

#### US-025 Scanner integration and GS1 parsing (5)
As an operator, I want one scan to fill item, batch, and expiry, so that I don't type.
Refs: FR-HHD-01, 03, 04, 05.
- AC1. Given a GS1-128 barcode with AIs 01, 10, 17, when scanned on any item screen, then GTIN, batch, and expiry fields are filled.
- AC2. Given a Zebra (DataWedge) or Honeywell device, when the trigger is pressed, then the scan reaches the focused field without the soft keyboard.
- AC3. Given an invalid scan, when processed, then the screen flashes red, the device vibrates, and a plain message shows.
- AC4. Given manual typing in an item field without `Mobile.ManualEntry`, when attempted, then it is blocked.

---

## Sprint 4: Inventory core and ledger (33)

**Sprint goal:** Opening stock with batches is loaded, visible on web and handheld, and every change is in the ledger.

#### EN-02 Stock record and immutable ledger (8)
Epic E07. Refs: FR-INV-10, 11, 12, FR-GEN-06.
- AC1. Given any stock change, when committed, then exactly one ledger event with all required fields is written in the same transaction.
- AC2. Given a failure after the stock update, when the transaction rolls back, then neither the stock change nor the event exists.
- AC3. Given two concurrent changes to the same stock record, when processed, then one succeeds and the other fails with "Stock changed, refresh".
- AC4. Given the ledger, when any API or repository tries update or delete, then no such path exists (architecture test).

#### US-026 LPN creation and labels (3)
As a Receiver, I want to print LPN labels, so that every pallet and carton can be tracked.
Refs: FR-INV-01, 02. Perm: `LPN.Manage`.
- AC1. Given I request 50 pallet LPNs, when printed, then 50 unique codes with tenant prefix print and are stored as Open.
- AC2. Given an LPN code is scanned that does not exist, when used in receiving, then it is rejected.

#### US-027 Opening stock import (5)
As a Tenant Admin, I want to load current stock from Excel at go-live, so that the system matches the floor on day one.
Refs: FR-INV-25. Perm: `Inventory.OpeningBalance`.
- AC1. Given the warehouse is in Go-live mode and a valid file, when imported, then stock records and `OPENING_BALANCE` events are created per row.
- AC2. Given Go-live mode is off, when I try to import, then it is refused.
- AC3. Given rows with a missing batch for a batch-tracked item, an expired date, or a duplicate serial, when validated, then the file is rejected with row errors.

#### US-028 Stock enquiry on web and handheld (5)
As an Inventory Controller, I want to look up stock by any key, so that I can answer questions fast.
Refs: FR-INV-20. Perm: `Inventory.View`.
- AC1. Given I scan a location on the handheld, when the result loads, then I see each SKU, batch, expiry, LPN, status, on hand, allocated, available.
- AC2. Given I search by batch on web, when submitted, then all locations holding it across my warehouses are listed.
- AC3. Given handheld enquiry, when measured, then p95 response is under 300 ms in the test environment.

#### US-029 Move stock on handheld (5)
As a Putaway Operator, I want to move an LPN or loose items between locations, so that stock is where the system says it is.
Refs: FR-INV-03, 21, FR-INV-04. Perm: `Inventory.Move`.
- AC1. Given I scan an LPN and a target location, when confirmed, then all LPN contents move and one `MOVE` event per stock record is written.
- AC2. Given I move part of an LPN into a new LPN, when confirmed, then an `LPN_SPLIT` event records both LPN codes.
- AC3. Given a target that breaks mixing or capacity rules, when scanned, then it is rejected (see US-032).

#### US-031 Reason codes (2)
As a Tenant Admin, I want my own reason codes, so that reports use our language.
Refs: FR-INV-28.
- AC1. Given a new tenant, when I open reason codes, then defaults exist per operation type.
- AC2. Given a reason code in use, when I deactivate it, then it is hidden from new transactions but kept on old ones.

#### US-032 Location rule validation with override (3)
As an Inventory Controller, I want the system to enforce capacity and mixing rules, so that locations stay clean.
Refs: FR-LOC-07. Perm: `Inventory.OverrideLocationRules`.
- AC1. Given a location with mixed batch = no holding batch B1, when I put batch B2 there, then it is rejected with the rule named.
- AC2. Given I have override permission, when I override with a reason, then the move succeeds and the reason is on the ledger event.

#### US-044 Change stock status (2)
As a QC Inspector, I want to change stock status with a reason, so that blocked stock is not shipped.
Refs: FR-INV-22. Perm: `Inventory.ChangeStatus`, `QC.Release`.
- AC1. Given Available stock, when I set it to Damaged with a reason, then a `STATUS_CHANGE` event is written and it is no longer allocatable.
- AC2. Given QC Hold stock, when a user without `QC.Release` tries to make it Available, then it is refused.

---

## Sprint 5: Integration API and receiving (33)

**Sprint goal:** A client system can send items and ASNs by API, and receivers can receive them on the handheld.

#### EN-03 Integration API foundation (8)
Epic E10. Refs: FR-INT-01, 02, 04, 05, 06, 07. Perm: `ApiClient.Manage`.
- AC1. Given a Tenant Admin creates an API client for Client X with scope `asns:write`, when it calls the token endpoint, then it gets a token valid only for Client X and that scope.
- AC2. Given the same `Idempotency-Key` sent twice within 24 hours, when processed, then the second call returns the first response and creates nothing.
- AC3. Given 60 requests in one second against a 50/s limit, when processed, then excess calls get 429 with `Retry-After`.
- AC4. Given any error, when returned, then the body has `code`, `message`, `field`, `traceId`.
- AC5. Given a build, when published, then the OpenAPI spec for `/api/v1` is generated and available.

#### US-033 Items upsert and stock read API (3)
As a client's IT team, I want to push items and read stock by API, so that our systems stay in sync.
Refs: FR-ITM-04, FR-INT-03. Flag: `module.integrationApi`.
- AC1. Given 500 items in one call, when processed, then per-record results are returned and valid ones are upserted by client + SKU.
- AC2. Given a stock request by SKU, when called, then on hand, allocated, available by warehouse, batch, and status are returned for that client only.

#### US-034 Create ASN by API, Excel, or web (5)
As a client or Inbound Supervisor, I want to send expected receipts in advance, so that the dock can plan.
Refs: FR-INB-01, 02. Perm: `ASN.Manage`.
- AC1. Given a valid ASN by API, when posted, then it is created as Expected with an internal number and the client reference stored.
- AC2. Given the same client reference again for the same client, when posted, then it is rejected as duplicate.
- AC3. Given an ASN in Receiving, when edit or cancel is attempted, then it is refused.

#### US-035 Inbound board (3)
As an Inbound Supervisor, I want to see today's ASNs and assign receivers, so that the dock runs to plan.
Refs: FR-INB-03.
- AC1. Given ASNs for today, when I open the board, then they show by status with expected qty and arrival time.
- AC2. Given a truck arrives, when I record arrival at Dock 2, then the ASN moves to Arrived with time and dock.
- AC3. Given I assign a receiver, when they open receiving on the handheld, then the ASN is at the top of their list.

#### US-036 ASN receiving on handheld (8)
As a Receiver, I want to receive goods by scanning, so that stock is recorded accurately with batch and expiry.
Refs: FR-INB-04, 09, 10, FR-INV-27. Perm: `Inbound.Receive`.
- AC1. Given an ASN, when I scan an LPN, scan a GS1 item barcode, enter qty 10 cases, and confirm, then 120 EA (if case = 12) post to the dock location with batch and expiry, status Available or QC Hold per config, and a `RECEIPT` event.
- AC2. Given a batch-tracked item without batch, when I confirm, then it is blocked.
- AC3. Given expiry before mfg date or before today, when entered, then it is rejected.
- AC4. Given a serial-tracked item, when I receive 5 units, then I must scan 5 unique serials; a duplicate is rejected.
- AC5. Given the item has MRP per batch on, when receiving, then MRP is required.

#### US-037 Receiving checks: over-receipt, unexpected items, shelf life (3)
As an Inbound Supervisor, I want receiving rules enforced, so that bad stock does not enter.
Refs: FR-INB-06, 07, 08. Perm: `Inbound.OverrideTolerance`.
- AC1. Given tolerance 5% and expected 100, when the receiver enters a total of 106, then it is blocked unless a supervisor overrides with a reason.
- AC2. Given `inbound.allowUnexpectedItems = false`, when an item not on the ASN is scanned, then it is blocked.
- AC3. Given min shelf life 60% and the batch has 40% left, when received, then it is rejected showing "40% left, 60% required"; supervisor override possible with a reason.

#### US-038 Blind receiving (3)
As a Receiver, I want to receive without an ASN when allowed, so that unplanned deliveries are not stuck at the gate.
Refs: FR-INB-05. Perm: `Inbound.BlindReceive`.
- AC1. Given `inbound.allowBlindReceipt = true` for Client X, when I choose blind receive and Client X, then the same receive flow runs and an internal ASN is created in status Received.
- AC2. Given the setting is false, when I try, then Client X is not selectable.

---

## Sprint 6: GRN, webhooks, QC, putaway, inventory control (34)

**Sprint goal:** Received stock is inspected, put away to system-suggested locations, and controlled with approvals.

#### US-039 Close ASN and print GRN (3)
As an Inbound Supervisor, I want to close an ASN with a GRN, so that receipt is final and variances are on record.
Refs: FR-INB-11, 12. Perm: `Inbound.Close`.
- AC1. Given a Received ASN, when I close it, then a GRN PDF is created with logo, expected vs received per line, and variance.
- AC2. Given the ASN closes, when processed, then webhook `asn.received` is queued with received lines.

#### EN-04 Webhook delivery infrastructure (5)
Epic E10. Refs: FR-INT-10, 11, 12, 13. Perm: `Webhook.Manage`.
- AC1. Given an endpoint registered for Client X and event `asn.received`, when the event occurs, then a POST is sent with an HMAC-SHA256 signature header and an event ID.
- AC2. Given the endpoint returns 500, when retried, then retries follow 1 min, 5 min, 15 min, 1 h, then hourly up to 24 h, then dead-lettered.
- AC3. Given 300 stock changes for one SKU in a minute, when `stock.changed` is sent, then one batched event per SKU per 60 seconds is delivered.

#### US-040 QC inspection (5)
As a QC Inspector, I want to inspect QC Hold stock and record results, so that only good stock becomes available.
Refs: FR-INB-20, 21, 22. Flag: `module.qc`. Perm: `QC.Inspect`.
- AC1. Given QC Hold stock, when I record Pass, then it becomes Available with a `STATUS_CHANGE` event linked to the QC record.
- AC2. Given Partial, when I split 100 into 90 Pass and 10 Fail (reason: damaged), then 90 Available and 10 Damaged.
- AC3. Given I attach 3 photos on the handheld, when saved, then they are stored on the QC record; a 4th is refused.

#### US-041 Putaway rules (3)
As an Inventory Controller, I want to define where each kind of stock should go, so that putaway is consistent.
Refs: FR-INB-30. Perm: `PutawayRule.Manage`.
- AC1. Given rules ordered 1 to N, when stock is evaluated, then the first matching rule applies.
- AC2. Given a rule for Client X pallets to Zone R then Zone S, when saved, then it is used for Client X pallet putaway.

#### US-042 Directed putaway on handheld (5)
As a Putaway Operator, I want the system to tell me where to put stock, so that I don't have to decide.
Refs: FR-INB-31, 33, 35. Perm: `Putaway.Execute`.
- AC1. Given SKU A batch B1 exists in location R-01 with space and consolidation on, when I scan the LPN at the dock, then R-01 is suggested.
- AC2. Given no consolidation option, when suggested, then the empty valid location with the lowest pick sequence in the rule's zones is shown.
- AC3. Given I scan the suggested location and confirm, when done, then a `PUTAWAY` event is written.
- AC4. Given QC Hold stock, when suggested, then only locations allowing QC Hold are considered.

#### US-043 Putaway override, exceptions, pending list (3)
As an Inbound Supervisor, I want to see stuck putaway and allow justified overrides, so that docks stay clear.
Refs: FR-INB-32, 34, 36. Perm: `Putaway.Override`.
- AC1. Given no valid location exists, when suggestion runs, then the item appears in the exception list with the reason.
- AC2. Given an operator with override permission scans another valid location with a reason, when confirmed, then putaway completes and the reason is logged.
- AC3. Given stock on the dock for more than 240 minutes, when I open Putaway pending, then it is highlighted.

#### US-045 Adjustments with approval (5)
As an Inventory Controller, I want adjustments above a threshold to need approval, so that write-offs are controlled.
Refs: FR-INV-23, 24, FR-IAM-11. Perm: `Inventory.Adjust`, `Inventory.ApproveAdjustment`.
- AC1. Given an adjustment value below the threshold, when submitted, then stock changes immediately with an `ADJUSTMENT` event.
- AC2. Given value above threshold, when submitted, then status is Pending approval and stock does not change.
- AC3. Given no item cost exists for the client, when submitted, then the qty threshold is used instead.
- AC4. Given another approver approves, when saved, then stock changes and the event records requester and approver.

---

## Sprint 7: Orders, allocation, discrete picking (32)

**Sprint goal:** Orders arrive by API, are allocated FEFO with shelf life rules, and are picked on handhelds.

#### US-046 Order intake (5)
As a client system or Outbound Supervisor, I want to create orders by API, Excel, or web, so that work flows into the warehouse.
Refs: FR-OUT-01, 02. Perm: `Order.Manage`. Flag: `module.outbound`.
- AC1. Given a valid B2C order by API, when posted, then it is New with internal number and client ref stored.
- AC2. Given a duplicate client order ref for the same client, when posted, then the original order is returned (idempotent), not a new one.
- AC3. Given an inactive SKU or a 5-digit pincode, when posted, then it is rejected with line-level errors.

#### US-047 Cancel and hold orders (3)
As an Outbound Supervisor, I want to cancel or hold orders, so that I can respond to client requests.
Refs: FR-OUT-03, 04. Perm: `Order.Hold`.
- AC1. Given an Allocated order, when cancelled via API, then allocations are released and webhook `order.cancelled` fires.
- AC2. Given an order in Picking with 3 lines picked, when cancelled, then putback tasks are created for picked stock.
- AC3. Given a Packed or Shipped order, when cancel is attempted, then it is refused.
- AC4. Given I place an order On Hold with a reason, when allocation runs, then it is skipped.

#### US-048 Allocation engine (8)
As an Outbound Supervisor, I want orders allocated by FEFO and shelf life rules, so that customers get the right stock.
Refs: FR-OUT-10 to 13, 15. Perm: `Order.Allocate`.
- AC1. Given batches expiring in 30, 60, 90 days and FEFO, when an order is allocated, then the 30-day batch is used first.
- AC2. Given order line min shelf life 70% and the 30-day batch has 20% left, when allocated, then that batch is skipped.
- AC3. Given orders with priority 1 and 3, when allocated together with limited stock, then priority 1 is served first.
- AC4. Given a location in Counting or Blocked status, when allocating, then its stock is not used.
- AC5. Given allocation completes, when checked, then `AllocatedQty` is set on specific stock records and `ALLOCATE` events exist.
- AC6. Given 5,000 order lines, when allocated in one run in the performance environment, then it finishes in under 60 seconds.

#### US-049 UOM-optimized allocation (3)
As a Warehouse Manager, I want full pallets and cases picked from reserve, so that pickers don't break pallets needlessly.
Refs: FR-OUT-14.
- AC1. Given an order for 480 EA and a full pallet LPN of 480 EA, when allocated, then the whole LPN is allocated.
- AC2. Given an order for 130 EA with case = 12, when allocated, then 10 cases come from reserve and 10 EA from the pick face.
- AC3. Given `outbound.strictRotation = true` and the full pallet has a later expiry than loose stock, when allocated, then rotation wins.

#### US-050 Partial allocation, backorder, de-allocate (3)
As an Outbound Supervisor, I want clear outcomes when stock is short, so that I can tell the client.
Refs: FR-OUT-16, 17.
- AC1. Given partial allocation allowed and stock for 6 of 10, when allocated, then 6 are reserved and the order is Partially allocated.
- AC2. Given partial not allowed, when stock is short, then nothing is reserved, the order is Backordered, and `order.backordered` fires.
- AC3. Given I de-allocate an order, when confirmed, then reservations are released with `DEALLOCATE` events.

#### US-051 Release and pick list generation (5)
As an Outbound Supervisor, I want to release orders into pick lists and assign them, so that pickers have work.
Refs: FR-OUT-20, 21, 23, 24. Perm: `Order.Release`, `Pick.Assign`.
- AC1. Given allocated orders, when released in discrete mode, then one pick list per order is created with tasks in pick sequence order.
- AC2. Given I assign a pick list to a picker, when they open picking, then it is first in their list.
- AC3. Given unassigned lists, when a picker taps "Next", then they get the oldest highest-priority list in their zone scope.

#### US-052 Discrete picking on handheld (5)
As a Picker, I want to be guided location by location, so that I pick the right stock quickly.
Refs: FR-OUT-25. Perm: `Pick.Execute`.
- AC1. Given a pick task, when I scan the location, item, confirm qty, and scan a tote, then stock moves to the tote with a `PICK` event and allocation stays attached.
- AC2. Given I scan a wrong location or wrong item, when processed, then it is rejected with error feedback.
- AC3. Given a full-pallet task, when I scan the LPN, then the whole LPN is picked in one step.
- AC4. Given all tasks done, when the list completes, then the order moves to Picked.

---

## Sprint 8: Batch picking, short picks, offline, packing (32)

**Sprint goal:** Pickers handle batches and shortages, keep working through Wi-Fi drops, and packers verify every unit.

#### US-053 Batch picking with sort (5)
As an Outbound Supervisor, I want to group small orders into one pick run, so that pickers walk less.
Refs: FR-OUT-22. Flag: `module.batchPicking`.
- AC1. Given 20 allocated B2C orders, when I create a batch pick list, then tasks are merged per location and sorted by pick sequence.
- AC2. Given more than `outbound.batchPick.maxOrders`, when I try, then it is refused.
- AC3. Given the batch is picked, when the packer scans an item at the sort station, then the screen shows which order it belongs to.

#### US-054 Batch substitution during picking (2)
As a Picker, I want to pick a different eligible batch when the allocated one is hard to reach, if allowed, so that I don't get stuck.
Refs: FR-OUT-26.
- AC1. Given substitution off, when I scan another batch, then it is blocked.
- AC2. Given substitution on and the scanned batch is eligible under rotation and shelf life rules, when confirmed, then allocation swaps and the swap is logged.

#### US-055 Short pick handling (5)
As a Picker, I want to report a shortage, so that the order still moves and the location gets checked.
Refs: FR-OUT-27.
- AC1. Given a task for 10 and only 6 found, when I confirm 6 with a reason, then by config the system reallocates, backorders, or cancels the 4.
- AC2. Given a short pick, when confirmed, then an ad hoc count task is created for that location.
- AC3. Given a short pick, when reported, then it appears on the Short pick report.

#### US-056 Handheld offline scan queue (8)
As a Picker, I want to keep confirming tasks during short Wi-Fi drops, so that my work doesn't stop.
Refs: FR-HHD-10 to 14.
- AC1. Given a downloaded pick list and no network, when I confirm picks, then they queue locally (encrypted) and a banner shows "Offline: N scans waiting".
- AC2. Given the network returns, when sync runs, then queued events are sent in order and the banner clears.
- AC3. Given 5 minutes or 200 queued events, when reached, then the app blocks with "Reconnect to continue".
- AC4. Given a queued pick conflicts (stock moved by someone else), when synced, then the task is flagged "Needs review" for the supervisor and nothing is lost.
- AC5. Given no network, when I try to start a new receipt, then it is refused with "Network needed".

#### US-057 Pack station with scan verification (5)
As a Packer, I want to scan every item into a carton, so that customers get exactly what they ordered.
Refs: FR-OUT-30, 31, 32, 33, 35. Perm: `Pack.Execute`.
- AC1. Given I scan a tote, when the order loads, then I see expected items and qty.
- AC2. Given scan verify on, when I scan an item not on the order or over qty, then it is blocked with error sound.
- AC3. Given I select a carton type and enter weight, when I close the carton, then a carton LPN is created and a `PACK` event is written.
- AC4. Given all lines packed, when done, then the order is Packed and a shipment is created.

#### US-058 Carton labels, SSCC, packing slip (3)
As a Packer, I want labels and a packing slip printed automatically, so that cartons are ready to ship.
Refs: FR-OUT-32, 34, FR-HHD-25.
- AC1. Given `packing.generateSSCC = true` and a GS1 prefix, when a carton closes, then a valid SSCC (check digit correct) label prints.
- AC2. Given the order completes, when packed, then the packing slip PDF prints on the station's document printer.

#### US-059 Daily expiry job (2)
As an Inventory Controller, I want expired stock blocked automatically, so that it is never shipped.
Refs: FR-INV-26.
- AC1. Given stock with expiry yesterday, when the 00:30 job runs, then it becomes Expired with an `EXPIRY_STATUS_CHANGE` event.
- AC2. Given it was allocated, when the job runs, then the allocation is released and the order is flagged.

#### Buffer for Sprint 7 spillover (2)
Reserved capacity. Not a story. Allocation (US-048) is the highest-risk item in Release 1.0.

---

## Sprint 9: Shipping, e-way bill, Integration Monitor, SFTP (32)

**Sprint goal:** Orders are booked with Shiprocket, manifested, dispatched with e-way bills, and every integration message is visible.

#### US-060 Shiprocket booking (8)
As a Packer, I want the AWB and label generated when packing finishes, so that cartons ship without extra steps.
Refs: FR-OUT-41, FR-INT-40, 41, 42. Flag: `connector.shiprocket`.
- AC1. Given Client X uses Shiprocket with valid credentials in the vault, when an order is packed, then a shipment is created in Shiprocket and AWB and label are stored and printed.
- AC2. Given Shiprocket returns an error, when booking fails, then the shipment shows "Booking failed", the message is in the Integration Monitor, and a retry is possible.
- AC3. Given credentials, when viewed in the UI or logs, then the secret is never shown.

#### US-061 Manual carrier and AWB entry (1)
As a Dispatcher, I want to enter carrier and AWB for non-integrated carriers, so that every shipment is tracked.
Refs: FR-OUT-42.
- AC1. Given a shipment with no carrier integration, when I enter carrier and AWB, then it is saved and usable on the manifest.

#### US-062 Manifest (3)
As a Dispatcher, I want to build a manifest per carrier, so that handover is documented.
Refs: FR-OUT-43. Perm: `Ship.Manifest`.
- AC1. Given packed shipments for Delhivery, when I scan cartons into a new manifest, then each carton is added once; a duplicate scan is rejected.
- AC2. Given the manifest, when printed, then the PDF lists AWBs, carton counts, and weights.

#### US-063 Dispatch confirmation (5)
As a Dispatcher, I want to confirm dispatch, so that stock is deducted and the client is told.
Refs: FR-OUT-44. Perm: `Ship.Dispatch`.
- AC1. Given all manifest cartons are scanned and vehicle, driver, seal are entered, when confirmed, then stock is deducted with `SHIP` events, LPNs are Shipped, orders are Shipped.
- AC2. Given dispatch completes, when processed, then `order.shipped` webhook fires with AWBs, batches, and serials.
- AC3. Given one carton is not scanned, when I confirm, then it is blocked listing the missing carton.

#### US-064 E-way bill export and number entry (5)
As a Dispatcher, I want e-way bill data generated from the shipment, so that compliant dispatch is quick.
Refs: FR-OUT-45, 46. Flag: `module.ewayBill`. Perm: `EwayBill.Manage`.
- AC1. Given shipments with invoice value at or above the threshold, when I export, then a JSON file in the government bulk-upload format is produced with GSTINs, HSN, invoice details, vehicle no.
- AC2. Given I enter or import e-way bill numbers, when saved, then they attach to the shipments.
- AC3. Given `shipping.ewayBill.required = true` and a qualifying shipment has no e-way bill number, when dispatch is confirmed, then it is blocked.

#### US-065 Integration Monitor (5)
As a Tenant Admin, I want to see and fix failed messages myself, so that I don't wait for developers.
Refs: FR-INT-30 to 34. Perm: `Integration.View`, `Integration.Retry`, `Integration.EditPayload`.
- AC1. Given API, webhook, SFTP, and connector traffic, when I open the monitor, then every message shows time, direction, channel, type, client, reference, status, error.
- AC2. Given a failed order message, when I edit the payload and replay, then it is processed and both original and edited payloads are kept.
- AC3. Given more than 10 failures for one client in 15 minutes, when detected, then an alert is raised.

#### US-066 SFTP file channel (5)
As a client's IT team, I want to drop ASN and order CSVs on SFTP, so that we can integrate without an API.
Refs: FR-INT-21, 22. Flag: `module.sftp`.
- AC1. Given a valid order CSV in the inbound folder, when polled within 2 minutes, then orders are created and the file moves to `/processed`.
- AC2. Given a file with 3 bad rows, when processed, then valid rows are created and an error file is written next to the file in `/error`.
- AC3. Given orders ship, when dispatched, then a shipment confirmation CSV appears in the outbound folder.

---

## Sprint 10: Returns, cycle counting, replenishment (33)

**Sprint goal:** Returns are received and graded, counts run blind with recounts and approval, and pick faces get replenished.

#### US-067 Expected returns (3)
As a Returns Operator, I want to know which returns are coming, so that I can receive them against the right order.
Refs: FR-RET-01, FR-INT-43. Flag: `module.returns`.
- AC1. Given an RMA via API, when posted, then an Expected return linked to the original order is created.
- AC2. Given Shiprocket reports "RTO initiated", when the status is received, then an Expected return of type RTO is created.

#### US-068 Receive returns on handheld (5)
As a Returns Operator, I want to scan an AWB and the items, so that returns are recorded fast.
Refs: FR-RET-02, 03. Perm: `Return.Receive`.
- AC1. Given I scan an AWB, when the return loads, then items, batches, and serials from the original shipment are pre-filled.
- AC2. Given I confirm items into a returns LPN, when done, then stock is created with status Returned and a `RETURN_RECEIPT` event.
- AC3. Given no reference is found, when I receive as Unidentified with client and photo, then it can be linked to an order later.

#### US-069 Grade returns (5)
As a Returns Operator, I want to grade each item, so that good stock is resold and bad stock is removed.
Refs: FR-RET-04, 05, 06. Perm: `Return.Grade`, `Return.ApproveDisposal`.
- AC1. Given grade Restock, when saved, then status becomes Available and a putaway task is created.
- AC2. Given an item below min shelf life or expired, when I choose Restock, then it is refused.
- AC3. Given Dispose above threshold, when saved, then it waits for approval and posts `WRITE_OFF` only when approved.
- AC4. Given grading completes, when saved, then `return.graded` fires.

#### US-070 Count plans and tasks (5)
As an Inventory Controller, I want to plan counts by zone, SKU, or recent movement, so that I keep stock accurate without a full shutdown.
Refs: FR-CNT-01, 02, 03, 08. Perm: `Count.Plan`.
- AC1. Given scope Zone A, when I create a plan, then one task per location in Zone A is created.
- AC2. Given a count task starts, when allocation or putaway runs, then that location is skipped; already released picks there can still complete.
- AC3. Given the task completes or is cancelled, when saved, then the location returns to Active.

#### US-071 Blind count with recount (5)
As a Counter, I want to count without seeing system qty, so that counts are honest.
Refs: FR-CNT-04, 05. Perm: `Count.Execute`.
- AC1. Given blind counting, when I open a task, then system qty is not shown anywhere.
- AC2. Given I count 95 where system had 100 and threshold is 2%, when submitted, then a recount task is created for a different counter.
- AC3. Given a location is empty, when I tap "Empty", then a zero count is recorded for all stock there.

#### US-072 Count review and approval (3)
As an Inventory Controller, I want to approve count variances, so that adjustments are controlled.
Refs: FR-CNT-06, 07, 09. Perm: `Count.Approve`.
- AC1. Given lines in Review, when I approve one, then a `COUNT_ADJUSTMENT` event posts.
- AC2. Given I was the counter, when I try to approve, then it is refused.
- AC3. Given the plan completes, when viewed, then inventory record accuracy uses the first count result.

#### US-073 Manual replenishment (5)
As an Outbound Supervisor, I want to create replenishment tasks for pick faces below min, so that pickers don't run short.
Refs: FR-RPL-01 to 05. Flag: `module.replenishment`. Perm: `Replenish.Plan`, `Replenish.Execute`.
- AC1. Given pick face min 50, max 200, current 30, case 12, when I create a task, then qty = 170 rounded up to 180 from the FEFO reserve location.
- AC2. Given a replenishment task, when performed on the handheld, then a `REPLENISH` event is written.
- AC3. Given replen and pick tasks in the same zone, when a picker asks for next work, then replenishment comes first.

#### US-074 Replenishment on short pick (2)
As a Picker, I want a replenishment created when a pick face runs out, so that the next picks succeed.
Refs: FR-OUT-28.
- AC1. Given `replenishment.onShortPick = true` and reserve stock exists, when a pick face short pick is confirmed, then a replenishment task is created.

---

## Sprint 11: Traceability, recall, reports, dashboards (34)

**Sprint goal:** A batch is traced and recalled in seconds, and managers run the floor from dashboards.

#### EN-05 Reporting store and report framework (8)
Epic E14. Refs: FR-RPT-01 to 04.
- AC1. Given a ledger or document change, when 5 minutes pass, then it is visible in reports; each report shows "Data as of HH:MM".
- AC2. Given any report, when opened, then filters, sorting, column show/hide, and saved views work.
- AC3. Given an export over 100,000 rows, when requested, then it runs in background and a download link valid 7 days is sent in-app.
- AC4. Given user scope, when any report runs, then rows outside the user's warehouses and clients never appear.

#### US-075 Traceability queries (5)
As an Inventory Controller, I want backward, forward, LPN, and serial traces, so that I can answer any "where did this go" question.
Refs: FR-TRC-01 to 06. Flag: `module.traceability`. Perm: `Trace.View`.
- AC1. Given a batch, when I run a forward trace, then every shipment, customer, ship-to, AWB, qty, returns, and current stock are listed.
- AC2. Given an SSCC from a shipped carton, when I run a backward trace, then receipt, supplier, QC result, and every move are shown in time order.
- AC3. Given a batch with 2 years of history across 5 warehouses, when traced in the performance environment, then results return in under 5 seconds.
- AC4. Given a result, when exported, then Excel and PDF are produced.

#### US-076 Batch recall (5)
As an Inventory Controller, I want to block a batch everywhere in one action, so that recalled goods never ship.
Refs: FR-TRC-07 to 10. Flag: `module.recall`. Perm: `Recall.Request`, `Recall.Approve`.
- AC1. Given I request a recall for SKU A batch B1 in all warehouses, when a second user approves, then all matching stock becomes Recalled with `RECALL_BLOCK` events.
- AC2. Given that stock was allocated, when approved, then allocations release, orders go On Hold with reason Recall, open pick tasks are cancelled.
- AC3. Given approval, when processed, then `batch.recalled` fires and the recall report shows shipped qty by customer and held qty by location.
- AC4. Given a lift is approved by a second user, when processed, then stock returns to its previous status.

#### US-077 Operations dashboard (5)
As a Warehouse Manager, I want a live view of inbound, outbound, and exceptions, so that I fix problems before cutoff.
Refs: FR-RPT-10, 11, 12, Functional Spec 14.3. Perm: `Dashboard.View`.
- AC1. Given the dashboard, when open, then it refreshes every 60 seconds and loads in under 3 seconds.
- AC2. Given orders not Packed with cutoff within 120 minutes, when shown, then they appear in "At cutoff risk".
- AC3. Given any tile, when clicked, then the filtered list behind it opens.
- AC4. Given today's KPIs, when calculated, then on-time ship %, fill rate, and pick accuracy use the BRD formulas.

#### US-078 Inventory health dashboard (3)
As an Inventory Controller, I want stock status, expiry, utilization, and accuracy at a glance, so that I spot risks early.
Refs: Functional Spec 14.3.
- AC1. Given the dashboard, when open, then it shows stock by status, near-expiry buckets, location utilization %, 30-day accuracy, pending approvals.

#### US-079 Inventory and traceability reports (3)
As an Inventory Controller, I want standard stock reports, so that I don't build spreadsheets.
Refs: Functional Spec 14.2.
- AC1. Given Stock on hand, Stock movement ledger, Expiry, and Adjustments reports, when run with their filters, then columns match the spec.

#### US-080 Inbound, outbound, returns, count, and audit reports (5)
As a Warehouse Manager, I want the remaining standard reports, so that every process is visible.
Refs: Functional Spec 14.2.
- AC1. Given GRN, ASN variance, Putaway pending, Order status, Short pick, Manifest, Returns register, Count variance, and User audit log reports, when run, then columns and filters match the spec.
- AC2. Given a standard report over 12 months of pilot-scale data, when run, then it returns in under 10 seconds.

---

## Sprint 12: Hardening, notifications, onboarding, UAT (31)

**Sprint goal:** Release 1.0 meets its exit criteria and the pilot 3PL signs UAT.

#### US-081 Notifications (3)
As a user, I want alerts for things that need my action, so that nothing waits silently.
Refs: FR-NTF-01, 02, 03.
- AC1. Given an adjustment pending my approval, when created, then I get an in-app notification and email per my preference.
- AC2. Given the near-expiry digest, when 07:00 warehouse time arrives, then subscribed users get one email listing stock expiring in 30 days.

#### US-082 Onboarding setup wizard (5)
As a Tenant Admin, I want a guided setup, so that a new warehouse is live in under 5 days.
Refs: BR-017, BR-001.
- AC1. Given a new warehouse, when I open the wizard, then it walks through: warehouse details, clients, users, locations, printers and Edge Agent, items, opening stock, go-live.
- AC2. Given each step, when completed, then progress is saved and I can resume later.
- AC3. Given go-live, when confirmed, then Go-live mode switches off and opening stock import is locked.

#### EN-06 Tenant isolation test suite (3)
Epic E19. Refs: NFR-042.
- AC1. Given every API endpoint, when called with tenant A's token for tenant B's IDs, then all return 403 or 404 and no data.
- AC2. Given the suite, when run in CI, then any failure blocks release.

#### EN-07 Ledger consistency check (2)
Epic E07. Refs: FR-INV-13.
- AC1. Given the nightly job, when balances recomputed from the ledger differ from stock records, then a Critical alert lists the mismatches.

#### EN-08 Performance test at 3x pilot peak (5)
Epic E19. Refs: NFR-010 to 016.
- AC1. Given a load profile of 3x pilot peak (60,000 lines a day rate, 200 handheld users), when run for 1 hour, then handheld p95 is under 300 ms and error rate under 0.1%.
- AC2. Given results, when below target, then issues are logged as Critical defects.

#### EN-09 Security fixes from penetration test (5)
Epic E19. Refs: NFR-045.
- AC1. Given the external pen test report, when the sprint ends, then all Critical and High findings are fixed and retested.

#### EN-10 Pilot UAT support and defect buffer (8)
Epic E19.
- AC1. Given UAT scripts covering receive, putaway, pick, pack, ship, return, count, trace, recall, when run by the pilot team, then all pass or have agreed workarounds.
- AC2. Given UAT completes, when signed by the pilot warehouse manager, then Release 1.0 exit criteria (PRD section 8) are met.

---

## Release 1.1 Backlog (Sprints 13 to 16, to be refined)

| Epic | Story titles |
|---|---|
| E15 3PL Billing | Rate card setup (storage per pallet per day, per inbound line, per outbound line, per carton, VAS); Charge calculation from ledger (including back-dated pilot period); Monthly client charge report and export |
| E16 Client Portal | Portal users and login; Client stock view; Client order and shipment status; Client reports and near-expiry view |
| E09 Outbound | Wave planning and release by carrier cutoff; Multi-order shipment consolidation |
| E12 Counting and Replenishment | Automatic min/max replenishment; ABC classification and count frequency |
| E03 Configuration | Config versioning, effective dates, preview; Custom fields on items, ASNs, orders |
| E06 Edge Agent | Weighing scale integration with weight tolerance check |
| E08 Inbound | Velocity-based putaway rules |
| E10 Integration | Tally Prime connector; Delhivery direct connector |
| E14 Reporting | Remaining 7 reports (aging, location utilization, picker productivity, LPN history standalone, client charges, ASN aging, returns aging); Scheduled email reports |
| E17 Localization | Hindi on handheld |
| E02 Identity | Platform support access with tenant consent (24 h, audited) |

## Release 1.2 Backlog (Sprints 17 to 20, to be refined)

| Epic | Story titles |
|---|---|
| E18 Enterprise | OIDC SSO (Azure AD, Google, Okta); Dedicated database tier and tenant migration; Shopify connector |
| E14 Reporting | Productivity dashboard; Multi-warehouse comparison dashboard |
| E10 Integration | Public extension API (beyond integration resources) |

---

## Traceability check

| Release 1.0 Must item (PRD 5.1) | Stories |
|---|---|
| Tenancy, subdomain, logo | US-001, 002, 016 |
| Feature flags | US-006 |
| Tenant isolation | US-002, 009, EN-06 |
| Users, roles, scope, SoD, MFA, audit | US-003, 004, 005, 008, 009, 010, 011 |
| Handheld login, devices | US-023, 024 |
| Config inheritance | US-012, 013 |
| Item master and import | US-014, 015, 033 |
| Locations | US-017 to 020, 032 |
| Labels, Edge Agent printing | US-021, 022, 026, 058 |
| LPN, ledger, statuses, opening stock | EN-02, US-026, 027, 044, 059, EN-07 |
| Inbound (ASN, blind, checks, QC, putaway) | US-034 to 043 |
| Moves, adjustments | US-029, 045 |
| Orders, allocation, picking | US-046 to 055 |
| Offline queue | US-056 |
| Packing, labels | US-057, 058 |
| Shipping, Shiprocket, manifest, e-way bill | US-060 to 064 |
| Integration API, webhooks, files, monitor | EN-03, EN-04, US-033, 065, 066 |
| Returns | US-067 to 069 |
| Counting, replenishment | US-070 to 074 |
| Traceability, recall | US-075, 076 |
| Dashboards, reports | EN-05, US-077 to 080 |
| Notifications, onboarding wizard (Should) | US-081, 082 |
