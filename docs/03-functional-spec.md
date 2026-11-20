# WMS SaaS: Functional Specification (Release 1.0)

| Field | Value |
|---|---|
| Version | 1.0 |
| Date | 2026-11-21 |
| Phase | 2. Product Owner / BA, step 2 of 3 |
| Sources | `01-business-requirements.md`, `02-prd.md` |
| Scope | Release 1.0 (Pilot). Release 1.1 and 1.2 items are specified before their sprints start. |

---

## 0. Conventions

### 0.1 Requirement IDs
`FR-<MODULE>-<nn>`. Every FR traces to a BR or NFR in the BRD. Modules: PLT, IAM, CFG, ITM, LOC, HHD, INV, INB, OUT, INT, RET, CNT, RPL, TRC, RPT, NTF.

### 0.2 Common rules for all entities
| ID | Rule |
|---|---|
| FR-GEN-01 | Every business record carries `TenantId`. Warehouse-level records also carry `WarehouseId`. Client-owned records carry `ClientId`. |
| FR-GEN-02 | Every record has `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`, and a row version for optimistic concurrency. |
| FR-GEN-03 | Master data (items, locations, clients, users) is never hard deleted. It is deactivated. Deactivation is blocked while stock or open documents reference it. |
| FR-GEN-04 | Timestamps are stored in UTC and shown in the warehouse time zone. Tenant-level screens use the tenant time zone. |
| FR-GEN-05 | Quantities are stored in the item's base UOM as `decimal(18,4)`. UI shows the UOM the user picked. |
| FR-GEN-06 | Concurrent edits to the same stock record fail with a clear "stock changed, refresh" message. No last-write-wins on stock. |
| FR-GEN-07 | Every screen and API checks the module feature flag first, then the permission, then data scope (warehouse, client, zone). |

### 0.3 Document numbers
Generated per warehouse, gap-tolerant, unique within tenant.

| Document | Format | Example |
|---|---|---|
| ASN (internal) | `ASN-{WH}-{YYMM}-{seq6}` | ASN-BOM1-2612-000123 |
| GRN | `GRN-{WH}-{YYMM}-{seq6}` | |
| Outbound order (internal) | `ORD-{WH}-{YYMM}-{seq6}`; client reference kept separately | |
| Pick list | `PCK-{WH}-{YYMMDD}-{seq4}` | |
| Shipment | `SHP-{WH}-{YYMM}-{seq6}` | |
| Manifest | `MAN-{WH}-{YYMMDD}-{seq3}` | |
| Return | `RET-{WH}-{YYMM}-{seq6}` | |
| Count plan | `CNT-{WH}-{YYMM}-{seq4}` | |
| Adjustment | `ADJ-{WH}-{YYMM}-{seq6}` | |
| LPN | `{TenantPrefix}{10-digit seq}` | KX0000012345 |
| SSCC | GS1 18-digit using tenant's GS1 company prefix (config) | |

---

## 1. Platform and Tenancy (E01)

### 1.1 Entities
- **Tenant:** Code, Name, Subdomain, Logo, Plan, Status, Default time zone, Default currency, Region, GS1 company prefix (optional), LPN prefix.
- **Plan:** Name, enabled modules (ceiling), limits (warehouses, users, order lines per month).
- **Warehouse:** Code (max 6 chars), Name, Address, GSTIN, Time zone, Status.
- **Client:** Code, Name, GSTIN, Contact, Status, Allowed warehouses.

### 1.2 Tenant lifecycle
`Trial > Active > Suspended > Terminated`
- Suspended: users cannot log in; integrations return 403; data kept.
- Terminated: read-only export available for 90 days, then deletion (NFR-061).

### 1.3 Requirements
| ID | Requirement | Trace |
|---|---|---|
| FR-PLT-01 | Platform admin creates a tenant with code, name, subdomain, plan, region. Subdomain must be unique and DNS-safe. | BR-010 |
| FR-PLT-02 | Tenant creation provisions: default role templates, default config, first Tenant Admin user (invite email). | BR-017 |
| FR-PLT-03 | Tenant Admin uploads a logo (PNG/SVG, max 500 KB). Shown on web header and printed documents. | BR-018 |
| FR-PLT-04 | Tenant Admin creates warehouses up to the plan limit. | BR-012 |
| FR-PLT-05 | Tenant Admin creates clients and links them to warehouses. An in-house tenant gets one default client created automatically and the client selector is hidden. | BR-010 |
| FR-PLT-06 | Requests to `{subdomain}.app-domain` resolve the tenant. Requests with a mismatched tenant token are rejected and logged as a security event. | NFR-042 |

### 1.4 Feature flags
| ID | Requirement | Trace |
|---|---|---|
| FR-PLT-10 | Effective flag = Plan ceiling AND Tenant setting AND (Client override, where the module supports it). | BR-013 |
| FR-PLT-11 | Turning a module off hides its menus, removes its permissions from role screens, and makes its APIs return 403 with code `MODULE_DISABLED`. Data is kept. | BR-102 |
| FR-PLT-12 | Turning a module off is blocked if it has open work (e.g. open pick tasks), with a list of what blocks it. | |
| FR-PLT-13 | All flag changes are audited. | BR-117 |

Flag catalogue: Appendix B.

---

## 2. Identity and Access (E02)

### 2.1 Entities
- **User:** Name, Email (unique per tenant), Employee code, Badge barcode (unique per tenant), PIN hash, Status (Invited, Active, Locked, Inactive), Preferred language, Default warehouse.
- **Role:** Name, Type (Template or Custom), Permissions list.
- **Role assignment:** User, Role, Scope: warehouses (one, many, all), clients (one, many, all), zones (optional).
- **Device:** Device ID, Model, Warehouse, Status (Active, Revoked), Last seen.

### 2.2 Requirements
| ID | Requirement | Trace |
|---|---|---|
| FR-IAM-01 | Tenant Admin invites users by email. Invite link expires in 72 hours. | |
| FR-IAM-02 | Password policy: min 12 chars, checked against a breached-password list, lockout after 5 failures for 15 minutes. | NFR-040 |
| FR-IAM-03 | TOTP MFA mandatory for Tenant Admin, Warehouse Manager, Inventory Controller. Optional for other web roles (tenant setting can make it mandatory for all). | BR-110 |
| FR-IAM-04 | Handheld login: scan badge, enter 4 to 6 digit PIN. 5 wrong PINs lock the badge until a supervisor resets it. | BR-111 |
| FR-IAM-05 | Handheld only accepts logins on Active devices registered to the user's warehouse. | BR-115 |
| FR-IAM-06 | Device registration: supervisor scans a one-time QR from the web app on the new device. Revoking a device ends its sessions within 60 seconds. | BR-115 |
| FR-IAM-07 | Session timeouts: web idle 30 min; handheld lock after 10 min idle (PIN to unlock), forced logout at 12 hours. | BR-114 |
| FR-IAM-08 | Role templates (read-only) are seeded per Appendix A. Tenant Admin can clone and edit to create custom roles. | BR-101 |
| FR-IAM-09 | A user's effective permissions = union of all role assignments, each limited to its own scope. | BR-100 |
| FR-IAM-10 | Separation of duties: a user cannot approve their own adjustment, count, or role change. Enforced server-side. | BR-103 |
| FR-IAM-11 | Adjustment approval threshold (value in tenant currency) is config `inventory.adjustment.approvalThreshold`. Above it, a second user with `Inventory.ApproveAdjustment` must approve. | BR-103 |
| FR-IAM-12 | Security audit log records: login success/failure, logout, MFA changes, role and permission changes, device registration and revoke, flag changes, config changes. Read-only, filterable, exportable. | BR-117 |

---

## 3. Configuration Engine (E03)

| ID | Requirement | Trace |
|---|---|---|
| FR-CFG-01 | Each config key has: key, data type, default, allowed levels, allowed values, description. Catalogue in Appendix C. | BR-080 |
| FR-CFG-02 | Resolution order: SKU > Client > Warehouse > Tenant > Platform default. First level with a value wins. | BR-081 |
| FR-CFG-03 | Config screen shows the effective value and which level it came from. | BR-081 |
| FR-CFG-04 | Changes apply immediately to new transactions. Documents already in progress keep the rules they started with (e.g. an ASN being received keeps its QC rule). | |
| FR-CFG-05 | Every change is audited with old value, new value, user, time. Versioning and preview are Release 1.1. | BR-087 |

---

## 4. Item Master (E04)

### 4.1 Item fields
| Field | Rules |
|---|---|
| Client | Required. Items are owned by a client. |
| SKU code | Required, unique per client, max 40 chars |
| Description | Required, max 200 chars |
| Barcodes | Zero or more. Each unique per client. Types: EAN-13, GTIN-14, UPC, custom. Each barcode maps to a UOM. |
| UOM hierarchy | Base UOM (e.g. EA) plus up to 3 levels (e.g. Inner = 10 EA, Case = 6 Inner, Pallet = 40 Case). Each level: length, width, height (cm), gross weight (kg). |
| Tracking flags | Batch tracked, Expiry tracked, Mfg date tracked, Serial tracked (each yes/no) |
| Shelf life days | Required if Expiry tracked |
| Storage class | Ambient (default). Others reserved for later releases. |
| HSN code | Optional, for GST and e-way bill |
| MRP per batch | Optional flag. If on, MRP is captured at receipt with the batch. |
| Status | Active, Inactive |

### 4.2 Requirements
| ID | Requirement | Trace |
|---|---|---|
| FR-ITM-01 | Create, edit, deactivate items on web. Tracking flags cannot change while the item has stock. | BR-021 |
| FR-ITM-02 | Excel import with downloadable template. Max 50,000 rows. Mode: "validate only" or "import". Errors returned per row in a downloadable file. | BR-017 |
| FR-ITM-03 | Import of 5,000 items completes in under 60 seconds. | NFR-016 |
| FR-ITM-04 | Items can also be created and updated via Integration API (upsert by client + SKU code). | BR-120 |
| FR-ITM-05 | Item search by SKU, description, or any barcode, across clients the user can see. | |

---

## 5. Location Management (E05)

### 5.1 Location fields
| Field | Rules |
|---|---|
| Code | Unique per warehouse, max 20 chars. Built from hierarchy segments (e.g. `A-01-03-B`). |
| Zone, Aisle, Bay, Level, Bin | Zone required, the rest optional |
| Type | Receiving, Staging, QC, Storage-Rack, Storage-Shelf, Storage-Floor, PickFace, PackStation, Dispatch, Returns, Quarantine, Damaged |
| Capacity | Max weight (kg), max volume (m3), max pallets. Blank = unlimited. |
| Mixing rules | Allow mixed SKU, Allow mixed batch, Allow mixed client (each yes/no). Default: no, no, no. |
| Allowed statuses | Subset of stock statuses. Defaults by type (e.g. Quarantine type allows only Quarantine, QC Hold). |
| Pick sequence | Integer. Drives walk order in picking. |
| Status | Active, Blocked, Counting |
| Check digits | 2 digits printed on label, reserved for voice picking later |

### 5.2 Requirements
| ID | Requirement | Trace |
|---|---|---|
| FR-LOC-01 | Create and edit single locations on web. | BR-070 |
| FR-LOC-02 | Bulk create by pattern. Each segment supports numeric ranges with zero padding (`{01..20}`) and letter ranges (`{A..E}`). Preview shows count and first 20 codes before commit. Max 20,000 per action. | BR-073 |
| FR-LOC-03 | Bulk edit attributes for a filtered set of locations. | BR-073 |
| FR-LOC-04 | Excel import and export. | BR-073 |
| FR-LOC-05 | Print location labels (barcode = location code) via Edge Agent, single or bulk, using a label template. | BR-073 |
| FR-LOC-06 | Block a location with a reason. Blocked locations are skipped by putaway and allocation. Stock inside stays visible. | BR-072 |
| FR-LOC-07 | Putaway into a location is rejected if it breaks capacity, mixing rules, or allowed statuses. Override needs `Inventory.OverrideLocationRules` and a reason. | BR-072 |

---

## 6. Handheld App and Edge Agent (E06)

### 6.1 Handheld UX rules
| ID | Requirement | Trace |
|---|---|---|
| FR-HHD-01 | Every input field accepts scanner input. Typing is a fallback, needing a permission for item and location fields (`Mobile.ManualEntry`). | NFR-090 |
| FR-HHD-02 | One task per screen. Large buttons (min 48 dp). Readable at arm's length (min 18 sp for key values). | NFR-090 |
| FR-HHD-03 | Feedback: success = green flash + short beep; error = red screen + long vibration + error sound + plain-language message. | NFR-090 |
| FR-HHD-04 | GS1-128 / GS1 DataMatrix parsing. Supported AIs: 00 (SSCC), 01 (GTIN), 10 (batch), 11 (mfg date), 17 (expiry), 21 (serial), 37 (count). One scan fills all matching fields. | BR-024 |
| FR-HHD-05 | Scanner integration with Zebra DataWedge and Honeywell scan engine. Camera scanning as fallback. | BR-060 |
| FR-HHD-06 | Home screen shows only menus the user has permission for in the current warehouse. | BR-102 |
| FR-HHD-07 | All UI text comes from resource files (i18n-ready). English only in 1.0. | BR-116 |

### 6.2 Offline tolerance
| ID | Requirement | Trace |
|---|---|---|
| FR-HHD-10 | When the network drops, the app keeps working for confirmations on tasks already downloaded: pick confirm, putaway confirm, count entry, move confirm. | BR-061 |
| FR-HHD-11 | Starting new work (new receipt, new task fetch) needs the network. | BR-061 |
| FR-HHD-12 | Queued events are stored encrypted on the device and synced in order when the network returns. Max 5 minutes or 200 events; after that the app blocks with "Reconnect to continue". | BR-061 |
| FR-HHD-13 | If a synced event conflicts (stock already moved), the task is flagged "Needs review" for a supervisor. Nothing is silently dropped. | BR-061 |
| FR-HHD-14 | A banner shows "Offline: N scans waiting" while offline. | |

### 6.3 Edge Agent
| ID | Requirement | Trace |
|---|---|---|
| FR-HHD-20 | Windows service installed per warehouse. Registers with a one-time token. Keeps an outbound secure connection to the cloud (no inbound firewall ports). | BR-131 |
| FR-HHD-21 | Admin registers printers in the web app (name, IP, type ZPL or TSPL, warehouse, default for: location labels, LPN labels, shipping labels, packing slips). | BR-130 |
| FR-HHD-22 | Print jobs go cloud > Edge Agent > printer. If the internet drops, jobs already received are printed; jobs status syncs later. | BR-131 |
| FR-HHD-23 | Handheld and web can choose a printer, or use the station default. | |
| FR-HHD-24 | Web shows Edge Agent status (Online, Offline, Last seen) and printer status. Offline for more than 2 minutes triggers an alert. | NFR-081 |
| FR-HHD-25 | Packing slips (A4/A5 PDF) can also print through the Edge Agent to a normal office printer. | BR-043 |

---

## 7. Inventory Core (E07)

### 7.1 Stock record
One stock record = unique combination of:
`Warehouse, Location, LPN (nullable), Client, SKU, Batch (nullable), Expiry (nullable), Mfg date (nullable), Serial (nullable), Status`
holding `OnHandQty` and `AllocatedQty`. `AvailableQty = OnHandQty - AllocatedQty` (only for status Available).

**Clarification of BR-022:** "Allocated" is a reserved quantity on a stock record, not a separate stock status. Stock statuses are: **Available, QC Hold, Quarantine, Damaged, Expired, Returned, Recalled**. (Recalled added for E13.)

### 7.2 LPN
| ID | Requirement | Trace |
|---|---|---|
| FR-INV-01 | An LPN is a container (pallet or carton). Fields: LPN code, type (Pallet, Carton), parent LPN (for cartons on a pallet), current location, status (Open, Closed, Shipped, Consumed). | BR-020 |
| FR-INV-02 | LPN labels printed on demand (single or a block of N pre-printed codes). | BR-020 |
| FR-INV-03 | Moving an LPN moves all its contents in one scan. | BR-020 |
| FR-INV-04 | Splits (take part of an LPN into a new LPN) and merges are ledger events with both LPN codes recorded. | BR-091 |
| FR-INV-05 | Loose stock (no LPN) is allowed in shelf and pick-face locations. | |

### 7.3 Inventory ledger
| ID | Requirement | Trace |
|---|---|---|
| FR-INV-10 | Every stock change writes one ledger event, in the same transaction as the stock change. No stock change without a ledger event. | BR-090 |
| FR-INV-11 | Ledger event fields: Event ID, Type (Appendix D), Timestamp (UTC), User, Device, Warehouse, Client, SKU, Batch, Expiry, Serial, From location, To location, From LPN, To LPN, From status, To status, Qty (base UOM), Reason code, Source document type and number, Correlation ID. | BR-090 |
| FR-INV-12 | Ledger events are append-only. No update or delete through any API or screen. Corrections are new events. | BR-090 |
| FR-INV-13 | A nightly job recomputes balances from the ledger and compares to stock records. Any mismatch raises a Critical alert. | BR-090 |

### 7.4 Inventory operations
| ID | Requirement | Trace |
|---|---|---|
| FR-INV-20 | **Stock enquiry** (web and handheld): by SKU, barcode, batch, location, LPN, client. Shows on hand, allocated, available, status, expiry. | |
| FR-INV-21 | **Move** (handheld): scan source location or LPN, scan item (if loose), enter qty, scan target location. Target is validated per FR-LOC-07. | |
| FR-INV-22 | **Status change** (web and handheld): change status of a qty, with reason code. Changing to Available from QC Hold needs `QC.Release`. | BR-022 |
| FR-INV-23 | **Adjustment** (web): increase or decrease qty with mandatory reason code. Value = qty x item standard cost (config per client, optional; if no cost set, approval is based on qty threshold `inventory.adjustment.qtyApprovalThreshold`). | BR-103 |
| FR-INV-24 | Adjustment above threshold goes to status "Pending approval". Stock does not change until approved. Approver cannot be the requester. | BR-103 |
| FR-INV-25 | **Opening stock import** (Excel): location, LPN (optional), SKU, batch, expiry, mfg date, serial, qty, status. Posts `OPENING_BALANCE` events. Allowed only while the warehouse has the flag "Go-live mode" on. | BR-017 |
| FR-INV-26 | **Expiry job:** daily at 00:30 warehouse time, stock past expiry moves to status Expired (`EXPIRY_STATUS_CHANGE` event). Allocations on it are released and the order is flagged. | BR-023 |
| FR-INV-27 | Serial numbers are unique per tenant + client + SKU while in stock. Receiving a duplicate serial is blocked. | BR-021 |
| FR-INV-28 | Reason codes are tenant-configurable lists per operation (adjustment, status change, override, short pick). Seeded with defaults. | |

---

## 8. Inbound (E08)

### 8.1 ASN
**Header:** ASN number (internal), Client, Client reference, Supplier name, Expected date, Vehicle no (optional), Status.
**Lines:** SKU, Expected qty + UOM, Batch (optional), Expiry (optional), Serials (optional list), Line status.

**ASN states:** `Expected > Arrived > Receiving > Received > Closed`, plus `Cancelled`.
- Arrived: gate-in recorded (dock, time).
- Receiving: first scan done.
- Received: receiver marks complete.
- Closed: supervisor closes. Variance (short, over, unexpected item) is recorded.

### 8.2 Requirements
| ID | Requirement | Trace |
|---|---|---|
| FR-INB-01 | ASNs created via Integration API, Excel import, or manually on web. Duplicate client reference for the same client is rejected. | BR-030 |
| FR-INB-02 | ASN can be edited or cancelled only while Expected. | |
| FR-INB-03 | Inbound screen (web): ASNs by expected date and status; assign receivers; record arrival at a dock. | |
| FR-INB-04 | **ASN receiving (handheld):** select or scan ASN > scan or create LPN > scan item (GS1 fills batch/expiry) > enter batch, expiry, mfg date, serials, MRP if needed and not scanned > enter qty in chosen UOM > confirm. Stock posts to the dock location with event `RECEIPT`. | BR-030 |
| FR-INB-05 | **Blind receiving:** same flow without ASN. Client is chosen first. Creates an internal ASN with status Received for traceability. Needs config `inbound.allowBlindReceipt = true` for the client. | BR-030 |
| FR-INB-06 | Over-receipt beyond `inbound.overReceiptTolerancePct` is blocked. Supervisor override needs permission `Inbound.OverrideTolerance` and a reason. | BR-082 |
| FR-INB-07 | Receiving an item not on the ASN is blocked unless `inbound.allowUnexpectedItems = true`. | BR-082 |
| FR-INB-08 | **Minimum shelf life check:** if remaining shelf life % at receipt < `inbound.minShelfLifePct`, the line is rejected with message showing actual vs required. Supervisor override allowed with reason. | BR-082 |
| FR-INB-09 | Expiry must be after mfg date and after today. Batch is mandatory if the item is batch-tracked. | |
| FR-INB-10 | If QC is required (`inbound.qcRequired` resolved for SKU), received stock gets status QC Hold. Otherwise Available. | BR-031 |
| FR-INB-11 | A GRN is created per ASN on close. GRN printable as PDF with logo. Shows expected vs received per line and variance. | BR-030 |
| FR-INB-12 | On ASN close, webhook `asn.received` fires with received lines. | BR-121 |

### 8.3 QC
| ID | Requirement | Trace |
|---|---|---|
| FR-INB-20 | QC queue (web and handheld): all QC Hold stock by ASN and SKU. | BR-031 |
| FR-INB-21 | QC Inspector records: sample qty, result (Pass, Fail, Partial), defect reason, notes, optional photo (handheld camera, max 3, 2 MB each). | BR-031 |
| FR-INB-22 | Pass: status to Available. Fail: status to Quarantine (or Damaged, by reason code). Partial: inspector splits qty between outcomes. Each is a `STATUS_CHANGE` event linked to the QC record. | BR-031 |

### 8.4 Putaway
| ID | Requirement | Trace |
|---|---|---|
| FR-INB-30 | **Putaway rules** (web): ordered list per warehouse. Each rule matches on client, SKU, storage class, status, UOM (pallet or case), and gives an ordered list of target zones and location types. | BR-032 |
| FR-INB-31 | **Suggestion algorithm:** for the stock being put away, find the first matching rule. In its zones, filter locations that are Active, allowed for the status, pass mixing and capacity rules. Then rank: (1) location already holding same SKU + batch, if mixing allows consolidation and `putaway.consolidate = true`; (2) empty locations by lowest pick sequence. Return the top one. | BR-032 |
| FR-INB-32 | If no location fits, the suggestion is "No location found" with the reason, and the task goes to an exception list for the supervisor. | |
| FR-INB-33 | **Putaway on handheld:** scan LPN or item at dock > system shows suggested location > operator goes there > scans location > confirm. Event `PUTAWAY`. | BR-032 |
| FR-INB-34 | Operator can choose a different location with permission `Putaway.Override` and a reason. The chosen location must still pass FR-LOC-07. | BR-032 |
| FR-INB-35 | QC Hold stock is put away only to locations allowing QC Hold (or stays in QC area). | BR-031 |
| FR-INB-36 | Putaway pending list (web) shows stock sitting in dock or staging locations with age. Age over `putaway.alertAfterMinutes` (default 240) is highlighted. | Dock-to-stock KPI |

---

## 9. Outbound (E09)

### 9.1 Order
**Header:** Internal order no, Client, Client order ref, Order type (B2B, B2C), Priority (1 to 5), Ship-to name, address, pincode, phone, Required ship date, Carrier cutoff time, Carrier preference, Invoice no, Invoice value, Invoice date (from client system, used for e-way bill), Status, Hold reason.
**Lines:** SKU, Qty + UOM, Requested batch (optional), Min remaining shelf life % (optional, overrides config), Line status.

**Order states:**
`New > Allocated (or Partially allocated) > Released > Picking > Picked > Packing > Packed > Shipped`
Side states: `On Hold`, `Backordered`, `Cancelled`.

### 9.2 Intake
| ID | Requirement | Trace |
|---|---|---|
| FR-OUT-01 | Orders created via Integration API, Excel import, or manually on web. Duplicate client order ref for the same client is rejected (idempotent). | BR-040 |
| FR-OUT-02 | Validation on intake: client active, SKUs exist and active, qty > 0, ship-to pincode 6 digits for India. Invalid orders are rejected with line-level errors. | |
| FR-OUT-03 | Orders can be cancelled by API or web until Packed. Cancelling after Picking starts creates a putback task for picked stock. | |
| FR-OUT-04 | Supervisor can place an order On Hold and release it, with reason. | |

### 9.3 Allocation
| ID | Requirement | Trace |
|---|---|---|
| FR-OUT-10 | Allocation runs on demand (supervisor selects orders) or automatically on intake if `outbound.autoAllocate = true`. | BR-041 |
| FR-OUT-11 | Orders are processed by priority, then required ship date, then created time. | BR-041 |
| FR-OUT-12 | **Eligible stock:** same client, same SKU, status Available, location Active and not Counting, batch matches if requested, remaining shelf life % >= order line value or `outbound.minShelfLifePct`. | BR-083 |
| FR-OUT-13 | **Ordering of eligible stock:** by rotation rule (`inventory.rotation`: FEFO = earliest expiry first; FIFO = earliest receipt date first). Ties broken by pick sequence. | BR-023 |
| FR-OUT-14 | **UOM optimization:** if the qty needed is a full pallet and a full LPN pallet exists in eligible stock, allocate it whole. Else full cases from reserve. Remaining eaches from pick faces. Rotation rule still wins over UOM optimization if `outbound.strictRotation = true`. | BR-041 |
| FR-OUT-15 | Allocation reserves qty on specific stock records (`AllocatedQty`). Event `ALLOCATE` written (ledger, qty 0 movement, for audit). | BR-090 |
| FR-OUT-16 | If not enough stock: if `outbound.allowPartialAllocation = true`, allocate what is available and mark Partially allocated; else allocate nothing and mark Backordered. | BR-083 |
| FR-OUT-17 | Supervisor can de-allocate an order (releases reservations, event `DEALLOCATE`). | |
| FR-OUT-18 | Allocating 5,000 order lines completes in under 60 seconds (NFR-013 applies to waves in 1.1; 1.0 must meet it for batch allocation). | NFR-013 |

### 9.4 Release and picking
| ID | Requirement | Trace |
|---|---|---|
| FR-OUT-20 | Supervisor releases allocated orders. Release creates pick tasks, one per stock record allocation, grouped into a pick list. | BR-042 |
| FR-OUT-21 | **Discrete picking:** one pick list per order. | BR-042 |
| FR-OUT-22 | **Batch picking:** supervisor selects up to `outbound.batchPick.maxOrders` (default 20) orders into one pick list. Picker picks total qty per location, then sorts to orders at a sort/pack station by scanning tote or order label. | BR-042 |
| FR-OUT-23 | Pick tasks are sorted by location pick sequence. | BR-072 |
| FR-OUT-24 | Pick list assignment: supervisor assigns to a picker, or picker takes the next one from the queue (zone scope applies). | |
| FR-OUT-25 | **Pick on handheld:** shows location, SKU, description, batch, qty, UOM > scan location > scan item (or LPN for full pallet) > confirm qty > scan tote or target LPN. Event `PICK` moves stock from location to the tote/LPN, status stays Available, allocation stays attached. | BR-042 |
| FR-OUT-26 | Scanning a different batch than allocated is blocked, unless `picking.allowBatchSubstitution = true` and the scanned batch is eligible under FR-OUT-12/13 rules. Then allocation is swapped and logged. | BR-084 |
| FR-OUT-27 | **Short pick:** picker confirms a lower qty with reason. Then by `picking.shortPickAction`: (a) Reallocate from other eligible stock and add a new task; (b) Backorder the shortfall; (c) Cancel the shortfall line. The location is flagged for count (creates an ad hoc count task). | BR-084 |
| FR-OUT-28 | If the short location is a pick face and reserve stock exists, a replenishment task is created (`replenishment.onShortPick = true`). | BR-051 |

### 9.5 Packing
| ID | Requirement | Trace |
|---|---|---|
| FR-OUT-30 | Pack station screen (web, touch-friendly, with USB scanner): scan tote or order > list of expected items > scan each item into a carton. | BR-043 |
| FR-OUT-31 | If `packing.scanVerify = true`, every unit (or case barcode for case qty) must be scanned. Wrong item or extra qty is blocked with error sound. | BR-043 |
| FR-OUT-32 | Packer selects a carton type from a tenant list (code, inner dimensions, tare weight). Each carton gets an LPN (type Carton) and an SSCC if `packing.generateSSCC = true`. | BR-043 |
| FR-OUT-33 | Packer enters carton weight manually in 1.0 (scale integration is 1.1). | BR-085 |
| FR-OUT-34 | Close carton prints: carton label (SSCC/LPN) and, on order completion, packing slip. Event `PACK`. | BR-043 |
| FR-OUT-35 | When all lines are packed, order goes to Packed and a shipment is created. Pack mismatch reports count toward the pick accuracy KPI. | BR-043 |

### 9.6 Shipping
| ID | Requirement | Trace |
|---|---|---|
| FR-OUT-40 | A shipment holds one order and its cartons. (Consolidating many orders into one shipment is 1.1.) | BR-044 |
| FR-OUT-41 | **Carrier booking:** if the client uses Shiprocket (`shipping.carrierIntegration = SHIPROCKET`), the shipment is sent to Shiprocket on pack completion. The returned AWB and label (PDF/ZPL) are stored and printed. Failure goes to the Integration Monitor and the shipment shows "Booking failed". | BR-124 |
| FR-OUT-42 | Manual carrier: user enters carrier name and AWB number. | |
| FR-OUT-43 | **Manifest:** supervisor creates a manifest per carrier and dispatch, adding shipments by scanning cartons. Manifest PDF shows AWBs, carton counts, weights. | BR-044 |
| FR-OUT-44 | **Dispatch confirm:** enter vehicle no, driver name, seal no; all cartons must be scanned on the manifest. On confirm: stock deducted (`SHIP` event), LPNs set to Shipped, orders to Shipped, webhook `order.shipped` fires with AWBs, batches, serials. | BR-044 |
| FR-OUT-45 | **E-way bill export:** for shipments where invoice value >= `shipping.ewayBill.threshold` (default INR 50,000), generate a JSON file in the government bulk-upload format with GSTINs, HSN, invoice details, vehicle no. User uploads it to the government portal and enters the e-way bill number back (single or via Excel). | BR-126 |
| FR-OUT-46 | Dispatch is blocked if `shipping.ewayBill.required = true` and the e-way bill number is missing for a qualifying shipment. | BR-126 |

---

## 10. Integration Hub (E10)

### 10.1 Integration API
| ID | Requirement | Trace |
|---|---|---|
| FR-INT-01 | REST, JSON, versioned under `/api/v1`. OpenAPI spec published per release. | NFR-102 |
| FR-INT-02 | Auth: OAuth2 client credentials. Tenant Admin creates API clients, each scoped to one or more 3PL clients and a set of scopes (`items:write`, `asns:write`, `orders:write`, `stock:read`, `shipments:read`, `returns:write`). Secret shown once. | BR-113 |
| FR-INT-03 | Resources in 1.0: Items (upsert), ASNs (create, cancel, get), Orders (create, cancel, get), Returns (create RMA, get), Stock (get by SKU/batch/warehouse), Shipments (get). | BR-120 |
| FR-INT-04 | Every write accepts an `Idempotency-Key` header. Same key within 24 hours returns the original response. | BR-128 |
| FR-INT-05 | Bulk endpoints accept up to 500 records per call and return per-record results. | |
| FR-INT-06 | Rate limit per API client: default 50 requests/second, configurable per plan. 429 with `Retry-After` when exceeded. | NFR-044 |
| FR-INT-07 | Errors use a standard body: `code`, `message`, `field`, `traceId`. | |

### 10.2 Webhooks
| ID | Requirement | Trace |
|---|---|---|
| FR-INT-10 | Tenant Admin registers webhook endpoints per client with selected events (Appendix E) and a signing secret. | BR-121 |
| FR-INT-11 | Payload signed with HMAC-SHA256 in header `X-WMS-Signature`. Includes event ID for receiver dedupe. | BR-121 |
| FR-INT-12 | Delivery retries with exponential backoff for 24 hours (1 min, 5 min, 15 min, 1 h, then hourly). Then dead-lettered. | BR-128 |
| FR-INT-13 | Stock change webhook `stock.changed` is batched per SKU every 60 seconds to avoid floods. Full stock snapshot available via API for 15-minute reconciliation. | BR-127 |

### 10.3 Files
| ID | Requirement | Trace |
|---|---|---|
| FR-INT-20 | Excel/CSV import templates for: items, locations, opening stock, ASNs, orders, e-way bill numbers. Template downloadable from each import screen. | BR-122 |
| FR-INT-21 | SFTP: per client, an inbound folder for ASN and order CSVs (polled every 2 minutes) and an outbound folder for shipment confirmations. Processed files move to `/processed` or `/error` with an error file next to them. | BR-122 |
| FR-INT-22 | Import mode for transactional files (ASNs, orders): accept valid records, reject invalid ones with a per-row error file. | |

### 10.4 Integration Monitor
| ID | Requirement | Trace |
|---|---|---|
| FR-INT-30 | Every inbound and outbound message is logged: time, direction, channel (API, Webhook, SFTP, Connector), message type, client, business reference, status (Received, Processed, Failed, Retrying, Dead-lettered), error, payload. | BR-129 |
| FR-INT-31 | Filters by status, client, type, date, reference. | BR-129 |
| FR-INT-32 | Actions: Retry; Edit payload and replay (needs `Integration.EditPayload`, logs original and edited payload); Mark ignored with reason. | BR-129 |
| FR-INT-33 | Payloads kept 90 days. Metadata kept 7 years. | NFR-060 |
| FR-INT-34 | More than 10 failures for one client in 15 minutes triggers an alert. | NFR-081 |

### 10.5 Shiprocket connector
| ID | Requirement | Trace |
|---|---|---|
| FR-INT-40 | Per client config: Shiprocket credentials (stored in vault), pickup location code, default courier preference. | BR-124 |
| FR-INT-41 | On pack completion: create shipment, assign AWB, fetch label. | BR-124 |
| FR-INT-42 | On dispatch: request pickup / mark as manifested if the account requires it. | BR-124 |
| FR-INT-43 | Poll or receive tracking updates. Status "RTO initiated" creates an expected return (E11). | BR-050 |

---

## 11. Returns (E11)

**Return types:** Customer return (RMA from client via API or manual), RTO (from carrier status or manual).
**States:** `Expected > Receiving > Received > Graded > Closed`.

| ID | Requirement | Trace |
|---|---|---|
| FR-RET-01 | Expected returns created by API (RMA), from Shiprocket RTO status, or manually. Linked to the original order and shipment where known. | BR-050 |
| FR-RET-02 | **Receive return (handheld):** scan AWB, order no, or RMA no > scan items > record batch and serial (pre-filled from the original shipment, editable) > qty > put into a returns LPN at a Returns location. Status Returned. Event `RETURN_RECEIPT`. | BR-050 |
| FR-RET-03 | Unknown returns (no reference found) can be received to an "Unidentified" bucket with client and photo, and linked later. | |
| FR-RET-04 | **Grading:** per item, grade = Restock, Refurbish, Damaged, Dispose. Restock: status Available, putaway task created. Refurbish: status QC Hold. Damaged: status Damaged, moved to Damaged location. Dispose: needs approval (same rules as adjustment), then `WRITE_OFF` event. | BR-050 |
| FR-RET-05 | Restocked items keep original batch and expiry. Expired or below min shelf life cannot be graded Restock. | BR-023 |
| FR-RET-06 | Webhook `return.graded` fires per return with grades. | BR-121 |

---

## 12. Cycle Counting (E12) and Replenishment

### 12.1 Cycle counting
**Count plan states:** `Draft > Scheduled > In progress > Review > Completed`, plus `Cancelled`.

| ID | Requirement | Trace |
|---|---|---|
| FR-CNT-01 | Create count plan: scope by zone, aisle, location range, SKU, client, or "locations with movement since date". Type: Scheduled (date) or Ad hoc (immediate). | BR-052 |
| FR-CNT-02 | Plan generates one count task per location. Tasks assigned to counters or taken from a queue. | BR-052 |
| FR-CNT-03 | When a count task starts, the location status becomes Counting. Allocation and putaway skip it. Pick tasks already released for it can still complete; the count snapshot accounts for them by ledger time. | BR-052 |
| FR-CNT-04 | **Blind count (handheld)** when `counting.blind = true`: scan location > scan each LPN or item > enter qty, batch, expiry. System qty not shown. Count "empty location" with one button. | BR-086 |
| FR-CNT-05 | Variance per line = counted - system qty at count time. If abs variance % > `counting.recountThresholdPct` or abs value > `counting.recountThresholdValue`, a recount task is created for a different counter. | BR-086 |
| FR-CNT-06 | After the final count, lines with variance go to Review. Inventory Controller approves or rejects per line. Approved variance posts `COUNT_ADJUSTMENT` events. Counter cannot approve own count. | BR-103 |
| FR-CNT-07 | Approvals above the adjustment threshold need a second approver (same as FR-INV-24). | BR-103 |
| FR-CNT-08 | Location returns to Active when its task is completed or cancelled. | |
| FR-CNT-09 | Inventory record accuracy KPI = locations with zero variance / locations counted (first count result, before recount). | KPI |

### 12.2 Replenishment (manual in 1.0)
| ID | Requirement | Trace |
|---|---|---|
| FR-RPL-01 | Pick faces can have min and max qty per SKU (stored now, used for auto replenishment in 1.1). | BR-051 |
| FR-RPL-02 | Supervisor sees a "Below min" list for pick faces and creates replenishment tasks for selected rows. | BR-051 |
| FR-RPL-03 | Replenishment source chosen by rotation rule from reserve locations (Available, not Counting). Qty = max - current, rounded up to full cases. | BR-051 |
| FR-RPL-04 | Handheld replenishment: scan source location > scan item/LPN > qty > scan pick face. Event `REPLENISH`. | BR-051 |
| FR-RPL-05 | Replenishment tasks have higher priority than normal pick tasks in the queue for the same zone. | |

---

## 13. Traceability and Recall (E13)

| ID | Requirement | Trace |
|---|---|---|
| FR-TRC-01 | **Batch backward trace:** input client + SKU + batch, or a shipped SSCC/LPN/AWB. Output: receipts (ASN, GRN, supplier, date), QC results, every ledger event in time order, current stock by location. | BR-092 |
| FR-TRC-02 | **Batch forward trace:** input client + SKU + batch. Output: every shipment with order, customer, ship-to, date, AWB, qty; returns received; current stock by warehouse and status. | BR-093 |
| FR-TRC-03 | **LPN history:** every event for an LPN and its parent/child LPNs (splits and merges). | BR-091 |
| FR-TRC-04 | **Serial trace:** input serial. Output: receipt, moves, shipment, return. | BR-092 |
| FR-TRC-05 | Trace results for one batch across a tenant return in under 5 seconds (data up to 7 years, archived data may take up to 30 seconds). | BR-093, NFR-014 |
| FR-TRC-06 | Export trace result to Excel and PDF. | |
| FR-TRC-07 | **Recall:** Inventory Controller selects client + SKU + one or more batches, scope (all warehouses or selected), reason, reference. Requires second approver. | BR-094 |
| FR-TRC-08 | On approval: all matching stock changes to status Recalled (`RECALL_BLOCK` events), allocations released, affected orders On Hold with reason "Recall", open pick tasks for that stock cancelled, webhook `batch.recalled` fires. | BR-094 |
| FR-TRC-09 | Recall report: shipped qty by customer (forward trace), held qty by location, affected open orders. | BR-094 |
| FR-TRC-10 | Recall can be lifted (second approver). Stock returns to its previous status. | BR-094 |

---

## 14. Reporting and Dashboards (E14)

### 14.1 Common report behaviour
| ID | Requirement | Trace |
|---|---|---|
| FR-RPT-01 | Each report has filters, sortable columns, column show/hide, saved views per user. | BR-140 |
| FR-RPT-02 | Export Excel, CSV, PDF. Above 100,000 rows runs in background; user gets an in-app notification and download link valid 7 days. | BR-142 |
| FR-RPT-03 | Data scoped by user's warehouses, clients, zones. | BR-143 |
| FR-RPT-04 | Reports read from the reporting store, max 5 minutes lag. Each report shows "Data as of HH:MM". | BR-144 |

### 14.2 Release 1.0 reports
| Report | Key filters | Key columns |
|---|---|---|
| Stock on hand | Warehouse, client, SKU, batch, location, status, zone | Location, LPN, SKU, batch, expiry, status, on hand, allocated, available, UOM |
| Stock movement ledger | Date range, event type, SKU, batch, location, user | All ledger fields |
| Expiry | Client, days to expiry buckets (0 to 30, 31 to 60, 61 to 90, 90+), status | SKU, batch, expiry, days left, qty, locations |
| GRN | Date, client, ASN, supplier | GRN no, ASN, lines, expected, received, variance |
| ASN vs received variance | Date, client | ASN, SKU, expected, received, short, over, unexpected |
| Putaway pending | Warehouse, age | LPN, SKU, qty, dock location, age (minutes) |
| Order status | Date, client, status, type, priority | Order no, client ref, status, lines, allocated %, required ship date, cutoff risk |
| Short pick | Date, client, picker, location | Order, SKU, location, expected, picked, reason, action taken |
| Shipment manifest | Date, carrier, manifest | Manifest, shipments, AWBs, cartons, weight |
| Returns register | Date, client, type, grade | Return no, order, AWB, SKU, batch, qty, grade |
| Count variance | Count plan, date, zone | Location, SKU, batch, system, counted, variance, recount, approved by |
| Adjustments | Date, reason, user, status | ADJ no, SKU, batch, qty, value, reason, requested by, approved by |
| Batch trace | (see E13) | (see E13) |
| User audit log | Date, user, event type | Time, user, event, details, IP/device |

### 14.3 Dashboards (1.0)
**Operations dashboard** (refresh every 60 s, per warehouse, client filter):
- Inbound today: ASNs expected / arrived / receiving / closed
- Putaway pending count and oldest age
- Outbound by status: New, Allocated, Released, Picking, Packed, Shipped (today)
- Orders at cutoff risk: not Packed and cutoff within `dashboard.cutoffRiskMinutes` (default 120)
- Exceptions: short picks today, booking failures, putaway exceptions, QC Hold qty, "Needs review" handheld tasks
- KPIs today: on-time ship %, order fill rate, pick accuracy

**Inventory health dashboard:**
- Stock by status (qty and SKU count)
- Near-expiry buckets
- Location utilization % (occupied / active storage locations)
- Inventory record accuracy (last 30 days)
- Pending adjustments and count reviews

| ID | Requirement | Trace |
|---|---|---|
| FR-RPT-10 | Every dashboard tile links to the filtered report or list behind it. | |
| FR-RPT-11 | Dashboard data max 1 minute stale. Load under 3 seconds. | BR-144, NFR-015 |
| FR-RPT-12 | KPI formulas are exactly as in BRD 7.2. Targets read from config. | BR-140 |

---

## 15. Notifications (NTF)

| ID | Requirement | Trace |
|---|---|---|
| FR-NTF-01 | In-app notification centre (web) with unread count. | BR-132 |
| FR-NTF-02 | Email for selected events. Users choose per event: in-app, email, both, none (within what their role allows). | BR-132 |
| FR-NTF-03 | 1.0 events: adjustment pending approval, count review pending, recall raised, recall approved, integration failure threshold, Edge Agent offline, export ready, near-expiry daily digest, orders at cutoff risk. | BR-132 |

---

## Appendix A: Permission Catalogue (1.0)

| Module | Permissions |
|---|---|
| Platform | `Tenant.Manage`, `Warehouse.Manage`, `Client.Manage`, `Feature.Manage`, `Config.Manage` |
| Identity | `User.Manage`, `Role.Manage`, `Device.Manage`, `Audit.View` |
| Item | `Item.View`, `Item.Manage`, `Item.Import` |
| Location | `Location.View`, `Location.Manage`, `Location.Block`, `Label.Print` |
| Inventory | `Inventory.View`, `Inventory.Move`, `Inventory.ChangeStatus`, `Inventory.Adjust`, `Inventory.ApproveAdjustment`, `Inventory.OpeningBalance`, `Inventory.OverrideLocationRules`, `LPN.Manage` |
| Inbound | `ASN.View`, `ASN.Manage`, `Inbound.Receive`, `Inbound.BlindReceive`, `Inbound.OverrideTolerance`, `Inbound.Close`, `QC.Inspect`, `QC.Release`, `Putaway.Execute`, `Putaway.Override`, `PutawayRule.Manage` |
| Outbound | `Order.View`, `Order.Manage`, `Order.Hold`, `Order.Allocate`, `Order.Release`, `Pick.Execute`, `Pick.Assign`, `Pack.Execute`, `Ship.Manifest`, `Ship.Dispatch`, `EwayBill.Manage` |
| Returns | `Return.View`, `Return.Receive`, `Return.Grade`, `Return.ApproveDisposal` |
| Counting | `Count.Plan`, `Count.Execute`, `Count.Approve` |
| Replenishment | `Replenish.Plan`, `Replenish.Execute` |
| Traceability | `Trace.View`, `Recall.Request`, `Recall.Approve` |
| Integration | `Integration.View`, `Integration.Manage`, `Integration.Retry`, `Integration.EditPayload`, `ApiClient.Manage`, `Webhook.Manage` |
| Reporting | `Report.View`, `Report.Export`, `Dashboard.View` |
| Mobile | `Mobile.Login`, `Mobile.ManualEntry` |

**Role template mapping** (summary; full matrix is maintained in the Role seed file):

| Role | Key permissions |
|---|---|
| Tenant Admin | All Platform, Identity, Integration, Config; View on everything else |
| Warehouse Manager | All operational permissions in scope, all approvals |
| Inventory Controller | Inventory.*, Count.*, Trace.*, Recall.*, Report.* |
| Inbound Supervisor | ASN.*, Inbound.*, Putaway.*, QC.Release, Report.View |
| Outbound Supervisor | Order.*, Pick.Assign, Ship.*, EwayBill.Manage, Replenish.Plan, Report.View |
| QC Inspector | QC.Inspect, QC.Release, Inventory.View |
| Receiver | Mobile.Login, Inbound.Receive, Inventory.View |
| Putaway Operator | Mobile.Login, Putaway.Execute, Inventory.Move |
| Picker | Mobile.Login, Pick.Execute, Replenish.Execute |
| Packer | Pack.Execute, Order.View |
| Dispatcher | Ship.Manifest, Ship.Dispatch |
| Counter | Mobile.Login, Count.Execute |
| Returns Operator | Mobile.Login, Return.Receive, Return.Grade |
| Report Viewer | Report.View, Dashboard.View |

## Appendix B: Feature Flag Catalogue (1.0)

| Flag key | Default | Client override |
|---|---|---|
| `module.inbound` | On | No |
| `module.qc` | On | Yes |
| `module.outbound` | On | No |
| `module.batchPicking` | On | No |
| `module.returns` | On | Yes |
| `module.cycleCount` | On | No |
| `module.replenishment` | On | No |
| `module.traceability` | On | No |
| `module.recall` | On | Yes |
| `module.integrationApi` | On | Yes |
| `module.sftp` | Off | Yes |
| `connector.shiprocket` | Off | Yes |
| `module.ewayBill` | On (India) | Yes |
| `module.serialTracking` | On | Yes |
| `module.reports` | On | No |

Core modules (tenancy, identity, config, item, location, inventory) are always on and have no flag.

## Appendix C: Configuration Catalogue (1.0)

Levels: P = Platform, T = Tenant, W = Warehouse, C = Client, S = SKU.

| Key | Type | Default | Levels |
|---|---|---|---|
| `inventory.rotation` | FEFO / FIFO / LIFO | FEFO if expiry-tracked, else FIFO | T, W, C, S |
| `inventory.adjustment.approvalThreshold` | Money | 50000 | T, W |
| `inventory.adjustment.qtyApprovalThreshold` | Number | 100 | T, W, C |
| `inbound.allowBlindReceipt` | Bool | false | T, W, C |
| `inbound.allowUnexpectedItems` | Bool | false | T, W, C |
| `inbound.overReceiptTolerancePct` | % | 0 | T, W, C, S |
| `inbound.minShelfLifePct` | % | 60 | T, C, S |
| `inbound.qcRequired` | Bool | false | T, W, C, S |
| `putaway.consolidate` | Bool | true | T, W, C |
| `putaway.alertAfterMinutes` | Number | 240 | T, W |
| `outbound.autoAllocate` | Bool | false | T, W, C |
| `outbound.allowPartialAllocation` | Bool | true | T, W, C |
| `outbound.minShelfLifePct` | % | 0 | T, C, S |
| `outbound.strictRotation` | Bool | true | T, W, C |
| `outbound.batchPick.maxOrders` | Number | 20 | T, W |
| `picking.allowBatchSubstitution` | Bool | false | T, W, C |
| `picking.shortPickAction` | Reallocate / Backorder / Cancel | Reallocate | T, W, C |
| `replenishment.onShortPick` | Bool | true | T, W |
| `packing.scanVerify` | Bool | true | T, W, C |
| `packing.generateSSCC` | Bool | false | T, C |
| `shipping.carrierIntegration` | None / SHIPROCKET | None | C |
| `shipping.ewayBill.threshold` | Money | 50000 | T |
| `shipping.ewayBill.required` | Bool | true | T, W, C |
| `counting.blind` | Bool | true | T, W |
| `counting.recountThresholdPct` | % | 2 | T, W, C |
| `counting.recountThresholdValue` | Money | 5000 | T, W |
| `dashboard.cutoffRiskMinutes` | Number | 120 | T, W |
| `security.mfaAllWebUsers` | Bool | false | T |

## Appendix D: Ledger Event Types

`OPENING_BALANCE`, `RECEIPT`, `PUTAWAY`, `MOVE`, `LPN_SPLIT`, `LPN_MERGE`, `STATUS_CHANGE`, `EXPIRY_STATUS_CHANGE`, `ADJUSTMENT`, `COUNT_ADJUSTMENT`, `ALLOCATE`, `DEALLOCATE`, `PICK`, `PUTBACK`, `PACK`, `SHIP`, `REPLENISH`, `RETURN_RECEIPT`, `WRITE_OFF`, `RECALL_BLOCK`, `RECALL_RELEASE`.

## Appendix E: Webhook Events (1.0)

`item.upserted`, `asn.received`, `order.allocated`, `order.backordered`, `order.shipped`, `order.cancelled`, `stock.changed` (batched), `stock.adjusted`, `return.received`, `return.graded`, `batch.recalled`, `batch.recall_lifted`.

## Appendix F: Open Questions for Architect and Tech Lead

| # | Question |
|---|---|
| Q-01 | Ledger plus stock-record projection: same DB transaction (simple, consistent) or event-sourced with async projection? FR-INV-10 requires stock and ledger to be consistent. |
| Q-02 | Reporting store: read replica, separate analytical DB, or materialized views? Must meet 5-minute lag and 5-second trace. |
| Q-03 | Handheld offline queue storage and conflict detection design (FR-HHD-12, 13). |
| Q-04 | Edge Agent connection protocol (WebSocket, MQTT, or SignalR) and auth. |
| Q-05 | Feature flag store and cache invalidation (flag change visible within 60 s). |
| Q-06 | Document number sequences per warehouse at scale without hot rows. |
