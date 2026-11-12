# Review packaged booking pricing

Date: November 12, 2026. Application: bundled Windows x64 Release from `cbe4e7a`, schema 40. Fixture: fresh fictional TEST business with two owners and one payer. These are native pricing checks on the development machine, not complete device or LIVE acceptance.

## AC-012 and AC-013 pricing checks

The owner creates a wedding through the app, adds nine catalog guest services and 76.6 billable travel miles, explicitly resolves every fixture tax rate to zero, and selects the fictional payer. The displayed plan and independently read stored inputs agree:

| Fixture | Total | Included trial | Deposit | Final |
| --- | ---: | ---: | ---: | ---: |
| $300 + 9 × $120 + 76.6 × $1 | $1,456.60 | $150.00 | $653.30 | $653.30 |
| Travel increased to 76.61 × $1 | $1,456.61 | $150.00 | $653.31 | $653.30 |
| $325.01 + 3 × $137.49 + 12.34 × $1.25 | $752.91 | $150.00 | $301.46 | $301.45 |

The custom travel line rounds to $15.43. The bridal charge includes the trial; no second trial charge is created. Exact-cent recomputation uses decimal quantities and midpoint-away-from-zero rounding, independent of the displayed summary.

After reducing guest quantity from nine to three, active recipient places change from ten to four. Original place IDs remain in retained history. One customer remains; unnamed places do not create customer records. The underlying catalog remains $300/$150/$120/$1, so booking overrides do not rewrite catalog defaults.

The second owner restarts the packaged app and sees the same saved custom booking and plan. Stored booking data matches the custom checkpoint, database integrity and foreign keys pass, and pricing creates zero financial movements.

## Scope limits

The zero-tax setting and venue/payer are fictional fixture inputs. No real tax policy, customer consent, payment, or reservation is inferred. Deposit-based reservation, split-payer issuance, complete device behavior, and the rest of AC-055 remain separate checks. These results establish the pricing portions of AC-012/013 and quantity consistency, not complete release acceptance.
