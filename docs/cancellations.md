# Prepare and review a cancellation

Cancellation outcomes depend on the actual agreement. The app supplies no default cancellation fee or refund promise.

1. Open **Cancellation cases**. Search for the actual booking, enter the decision date, and select **Prepare cancellation case**.
2. Record the request date, decision and reason, actual agreement review, and evidence. Select the relevant issued terms when available. Keep the client-facing credit-note reason separate from internal agreement details.
3. Under **Cancellation application reversals**, select the original applications and amounts to undo. Only applications to this booking can be reversed by its case.
4. Under **Cancellation charge reductions**, select each original invoice charge and quantity to reduce. Paid amounts must be unapplied before their charges can be reduced. Any unadjusted invoice obligation remains visible.
5. Under **Cancellation value decisions**, select the actual receipt or funded credit. Enter the reviewed retained, refunded, and transferred portions. An actual refund needs its account, method, evidence, included tax, and explicit tax review. Review a separate refunded-tax split if needed.
6. Select an optional different active destination booking for transferred credit. Transfer reserves previously received value; invoice application is a separate explicit decision. Held or suspense value keeps its source treatment until reviewed reclassification.
7. Select **Save cancellation draft for review**. Review the proposed actions, journal, remaining invoice obligations, and before/after source balances. Changing the effective date recalculates these checks during review.

Saving and previewing a draft leaves actual booked work, cash, credit, and invoice balances unchanged. The current desktop screen prepares this review; permanent approval and retained history controls are the next desktop step. Their business commands already require owner confirmation and one complete transaction.

Selecting another case or preparing a new case asks before discarding unsaved fields. Date-specific availability and later dated commitments remain separate. Shared receipts show other-booking applications so their value stays identifiable.

## Create a fictional rehearsal

Use a new empty directory:

```powershell
dotnet run --project tools/SarahBeauty.Demo/SarahBeauty.Demo.csproj -c Release -- --output .artifacts/cancellation-rehearsal --cancellation
```

The optional scenario adds a fictional no-trial booking, $100 service plus $10 test tax, and an applied $110 bank receipt dated January 10, 2027. These are software test records. Use January 11, 2027 for its cancellation review. No outcome is approved automatically.
