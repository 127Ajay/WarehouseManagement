## What and why

<!-- Story or enabler ID (e.g. US-001) and a short summary. -->

## Definition of Done (PRD section 10)

- [ ] Code reviewed; CI green
- [ ] Unit tests on domain and application layers (80%+ for new code)
- [ ] Integration test for each new API endpoint
- [ ] Tenant isolation test added if new tenant data is stored (RLS policy + EF filter)
- [ ] Feature flag and permission wired; module off means 403 and hidden UI
- [ ] Ledger or audit events written where the BRD requires them
- [ ] Acceptance criteria verified
- [ ] API docs updated
- [ ] New packages checked for licence (no MediatR, AutoMapper, MassTransit v9, FluentAssertions v8+)

## How to verify

<!-- Commands or requests a reviewer can run. -->
