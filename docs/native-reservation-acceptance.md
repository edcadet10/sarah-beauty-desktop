# Review packaged wedding reservation

Date: November 12, 2026. Application: bundled Windows x64 Release from `cbe4e7a`, schema 40. This continues the [fictional pricing fixture](native-pricing-acceptance.md) on the development machine. All payments, agreement evidence, and appointments are explicitly fictional TEST inputs.

## AC-014 reservation boundary

The owner saves one planned wedding appointment for March 20, 2027, from 08:00 to 16:00 in `America/New_York`, with the correct `-04:00` offset, a fictional artist and venue, and all four active recipient places. The appointment ends before the 18:00 ready-by time. No duration calculation or completed work is asserted.

The owner issues and records fictional acceptance of quote Q-000001 for $752.91, then issues single-payer invoice I-000001. Both saved PDFs succeed and retain their recorded hashes. The invoice has trial $150.00, deposit $301.46, and final $301.45 milestones. Trial/deposit dates are November 12, 2026; final is March 20, 2027.

| Native step | Stored result |
| --- | --- |
| Approve a $150 receipt and apply it specifically to Trial; attempt reservation | Rejected with the missing trial/deposit-allocation message; status remains Awaiting deposit; zero reservation reviews |
| Approve a separate $301.46 receipt and apply it specifically to Deposit | Status still Awaiting deposit; zero reservation reviews until explicit review |
| Confirm with owner proof and fictional decision evidence | Confirmed; exactly one reservation review and one transition to Confirmed; accepted quote ID and required amounts retained |
| Restart and sign in as the second owner | Confirmed booking and issued invoice/PDF remain available; stored booking and financial facts match the confirmation checkpoint |

## Independent checks

Read-only database checks after each stage verify exact Trial/Deposit applications, one customer, the appointment and recipient IDs, milestone dates, integrity, and foreign keys. Trial alone leaves $602.91 due. With both payments, $301.45 remains due.

The two receipts create four journal lines with balanced debits/credits of $451.46. Bank and income each change by $451.46. All account openings remain unconfirmed. Payment applications create no extra receipt or journal line. Complete document, PDF artifact, invoice, movement, journal, and application rows match the pre-confirmation checkpoint after reservation and second-owner restart; confirming the booking creates no money.

## Scope limits

This passes the native trial-versus-deposit reservation scenario in AC-014. It does not establish real consent, payment, tax policy, device behavior, all schedule conflict cases, or complete release acceptance. Split-payer issuance, real Google delivery, physical device/recovery checks, signing, and other acceptance scenarios remain separate work.
