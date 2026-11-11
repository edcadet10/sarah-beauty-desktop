# Prepare and review a cancellation

Cancellation outcomes depend on the actual agreement. The app supplies no default cancellation fee or refund promise.

1. Open **Cancellation cases**. Search for the actual booking, enter the decision date, and select **Prepare cancellation case**.
2. Record the request date, decision and reason, actual agreement review, and evidence. Select the relevant issued terms when available. Keep the client-facing credit-note reason separate from internal agreement details.
3. Under **Cancellation application reversals**, select the original applications and amounts to undo. Only applications to this booking can be reversed by its case.
4. Under **Cancellation charge reductions**, select each original invoice charge and quantity to reduce. Paid amounts must be unapplied before their charges can be reduced. Any unadjusted invoice obligation remains visible.
5. Under **Cancellation value decisions**, select the actual receipt or funded credit. Enter the reviewed retained, refunded, and transferred portions. An actual refund needs its account, method, evidence, included tax, and explicit tax review. Review a separate refunded-tax split if needed.
6. Select an optional different active destination booking for transferred credit. Transfer reserves previously received value; invoice application is a separate explicit decision. Held or suspense value keeps its source treatment until reviewed reclassification.
7. Select **Save cancellation draft for review**. Review the proposed actions, journal, remaining invoice obligations, and before/after source balances. Changing the effective date recalculates these checks during review.
8. Confirm that you reviewed the agreement, effects, and remaining obligations. Enter your local owner passphrase and select **Approve combined cancellation**. Changed sources or unsaved decisions require another saved review.

Saving and previewing a draft leaves actual booked work, cash, credit, and invoice balances unchanged. Approval records all selected effects and the cancellation together. It records actual refund evidence; it does not send a bank payment or contact the customer.

Selecting another case or preparing a new case asks before discarding unsaved fields. Date-specific availability and later dated commitments remain separate. Shared receipts show other-booking applications so their value stays identifiable.

## Review an approved decision

Select an entry under **Approved cancellation history**. It retains the original reviewed terms, before/after source balances, actions, approver, and time. Expand a linked cancellation credit note to create or reopen its saved PDF. Sending remains a manual owner action.

A later refund or use of the transferred credit changes current availability and keeps the earlier decision intact. Use the original receipt and funded credit when recording a later credit refund. The source balances captured in an approved case describe that decision's date and watermark. To inspect newer balances, prepare a case for the actual review date and open its captured sources; reading them does not approve another decision.

## Create a fictional rehearsal

Use a new empty directory:

```powershell
dotnet run --project tools/SarahBeauty.Demo/SarahBeauty.Demo.csproj -c Release -- --output .artifacts/cancellation-rehearsal --cancellation
```

The optional scenario adds a fictional no-trial booking, $100 service plus $10 test tax, and an applied $110 bank receipt dated January 10, 2027. These are software test records. Use January 11, 2027 for its cancellation review. No outcome is approved automatically.
