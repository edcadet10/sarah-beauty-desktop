# Review packaged single and split payers

Date: November 12, 2026. Application: bundled Windows x64 Release from `cbe4e7a`, schema 40. Fixture: a fresh fictional TEST business with two owners, two payer customers, and one wedding. These are native development-machine checks, not real customer instructions or complete release acceptance.

## AC-015 same-booking checks

The owner saves the custom $752.91 wedding: bridal $325.01, three guests at $137.49, and 12.34 travel miles at $1.25 rounded to $15.43. All tax rates are explicitly fictional zero. Four unnamed service places remain four places, without extra customers. The booking has a $150.00 trial, $301.46 deposit, and $301.45 final installment.

The owner issues/accepts fictional quote Q-000001 and issues the complete single-payer invoice I-000001. Before any payment, the owner uses Document decisions to void that unused invoice, retaining its private reason, original snapshot, and PDF. The same booking then changes to Split, with explicit unpaid-share responsibility, bridal payer, and guest payer. The accepted pricing source and total stay unchanged.

| Native action | Result |
| --- | --- |
| Prepare a bridal/travel invoice with no explicit travel payer | Preview and issuance both reject the charge/travel payer mismatch; complete issued-document, coverage, and PDF rows match the pre-attempt checkpoint |
| Assign travel to the bridal payer; refresh the same saved draft | I-000002 issues for $340.44; the failed attempt consumed no number |
| Issue all remaining guest quantities to the guest payer | I-000003 issues for $412.47 |
| Attempt another invoice after complete coverage | All source quantities are exhausted; issuance rejects with no additional version |
| Restart and sign in as the second owner | Same $752.91 booking and all three retained invoice versions/PDFs reopen, including the voided original |

## Independent checks

Read-only checks verify that the two active invoices sum to $752.91 and their combined milestones are exactly Trial $150.00, Deposit $301.46, and Final $301.45. Active coverage contains each of the three source charges once and sums to the exact source quantities and cents. Voided coverage does not duplicate the active bill.

The quote, voided original invoice, PDF artifact rows, and retained void decision remain unchanged after split issuance. All four saved PDFs match their hashes. Extracted PDF text contains the TEST mark and excludes seeded private customer notes, sensitivities, booking notes, and owner names. Invoice creation and voiding create zero movements, journal lines, or payment applications. Integrity and foreign-key checks pass; second-owner restart matches the final stored booking and financial/document checkpoint.

## Scope limits

This passes the AC-015 native single/split-payer, once-only share, and unresolved travel checks. Initial automation attempts stopped on controls before layout was ready; setup resumed from verified saved state, and the harness now waits/focuses controls before selecting them. Those interrupted attempts are not acceptance passes.

Long/multipage PDF layout, all private-field categories, physical devices, real Google delivery, signing, and remaining acceptance scenarios are separate work. No real acceptance, tax decision, or payment is inferred from this fixture.
