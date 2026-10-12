# Design: Transfer pair integrity

- **Date:** 2026-10-12
- **Status:** Approved for planning
- **Scope:** `Budgie.Modules.Finances`

## Problem

A transfer is stored as two (or three) rows linked by a shared `LinkedTransactionId`
group id. Three defects follow from the fact that no code treats that group as a unit:

1. **Destination is resolved arbitrarily.** `BuildResponsesAsync` queries every row
   sharing the group id — including the transfer's own outgoing leg — and takes
   `First()` of an unordered result. Roughly half the time the destination shown is
   the source account itself ("BCP Soles → BCP Soles").
2. **Updating a transfer corrupts it.** `UpdateTransactionCommandHandler` rebuilds a
   single row and does not pass `linkedTransactionId`, so the link is dropped, the
   other leg keeps the old amount/date, and the two accounts' balances desynchronise.
   The UI therefore has no safe way to modify a transfer.
3. **Deleting a transfer orphans the other leg.** `DeleteTransactionCommandHandler`
   only deletes children reached through `ParentTransactionId`, which a transfer pair
   does not use.

A fourth, related gap: `TransferCommand` has no `ExternalUid`, so retrying a bulk
import duplicates transfers instead of being a no-op. This was hit while importing
~5 months of bank notification emails.

## Invariants (new, explicit)

A *transfer group* is the set of transactions that share `LinkedTransactionId` for a
given user. A well-formed group contains:

| Leg | Type | Account |
|-----|------|---------|
| outgoing | `Transfer` (2) | origin |
| incoming | `Income` (0) | destination |
| fee (optional, only when `Fee > 0`) | `Expense` (1) | origin |

All legs share `Date`, `Amount`, `Currency`, `ExchangeRate` and `Notes`.

The existing linking scheme is **kept**: `LinkedTransactionId` stays a group id that
is not the `Id` of any row. No data migration is required and the 41 groups currently
in production remain valid. The invariant moves from an accident of
`TransferCommandHandler` into documented, tested behaviour.

## Design

### 1. Destination resolution (read)

`TransactionRepository.BuildResponsesAsync`:

- `AccountNameRow` gains `Id`.
- The query returns every row in the group, but the destination is the leg whose
  `Id` differs from the row being projected.
- If the group has only the current leg, `TransferDestinationAccountId` and
  `TransferDestinationAccountName` are `null` rather than echoing the source.

### 2. Update as a unit

`UpdateTransactionCommandHandler`, when the target row has `Type = Transfer`:

1. Load the whole group (`GetTransferGroupAsync`).
2. Validate:
   - requested `Type` must remain `Transfer`;
   - `Installments` must be 1;
   - the group must contain exactly one outgoing and one incoming leg (malformed
     groups are rejected, not repaired silently).
3. Revert the old impact on the origin and the previous destination.
4. Apply the new `Amount`, `Currency`, `ExchangeRate`, `Date` and `Notes` to both legs.
5. If the destination account changed, move the incoming leg and apply the impact on
   the new destination (and revert on the old one). Same for a changed origin.
6. The fee leg is left untouched: its amount does not scale with `Amount` and is not
   editable through this path, because `UpdateTransactionCommand` has no `Fee` field.
   This is a deliberate, documented limitation rather than a new field.

### 3. Delete as a unit

`DeleteTransactionCommandHandler`, when the target row has `Type = Transfer`, deletes
every leg in the group and reverts the impact on every distinct account involved
(origin, previous destination, and the destination when it moved).

### 4. Idempotent creation

- `TransferCommand` gains `string? ExternalUid`.
- `TransferCommandHandler` checks `GetIdByExternalUidAsync` first and returns the
  existing id without writing when it matches.
- The endpoint returns the `Id` of the outgoing leg instead of the group id. The
  frontend discards the response body, so this is not a breaking change.

### 5. Repository changes

- `GetByLinkedTransactionIdAsync(Guid groupId, Guid userId, CancellationToken)` —
  returns all legs of a group.
- `UpdateAsync` gains the ability to change `AccountId`, required to move a leg
  between accounts.

## Testing

Unit tests (`Budgie.Modules.Finances.UnitTests`):

- destination resolution picks the other leg, and returns null for a one-legged group;
- update propagates amount/date/notes to both legs and keeps the link;
- update moves the income leg when the destination changes, adjusting both balances;
- update moves the outgoing leg when the origin changes;
- update rejects a non-`Transfer` type, `Installments > 1`, and a malformed group;
- delete removes the whole group and reverts every affected balance;
- create with an existing `ExternalUid` is a no-op that returns the existing id.

Integration tests (`Budgie.IntegrationTests`, PostgreSQL): the same paths through the
HTTP endpoints.

## Non-goals

- Changing the linking scheme to cross-referencing ids.
- Backfilling `ExternalUid` onto the 41 existing transfers.
- Making the fee editable.
- A snapshot/restore fix for groups already broken by the old update path.
