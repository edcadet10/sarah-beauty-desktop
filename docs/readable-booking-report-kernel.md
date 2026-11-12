# Read captured booking contribution sources

Existing booking facts, billing, financial, and combined contribution readers now have internal business-ID paths. Public billing/financial calls retain owner checks and their read hooks. The combined human report retains authentication and uses the same invoice-reduction/application/balance consistency checks. Captured callers supply one timestamp for stable nested snapshots without creating or renewing a human session.

Issued net charges, customer applications, tagged cash, recognized service income, recorded direct costs, and unassigned costs remain separate. Unissued bookings retain an unknown contribution estimate. The estimate is billed net charges less recorded assigned direct costs; it is not exact business profit. Private booking/artist/appointment fields remain excluded by the existing facts reader.

Two new cases and twenty existing booking/billing/financial/report cases pass in Release (22 targeted). They compare actual charges/applications/cash/fees/costs with human reports, retain one source/watermark/time after later costs and lock, verify stable repeated serialization and private-note exclusion, and preserve unknown estimates/empty foreign scope/public access checks. Business Debug has zero warnings/errors. Latest full suite remains 926 before owner/booking additions. Table projection follows separately.
