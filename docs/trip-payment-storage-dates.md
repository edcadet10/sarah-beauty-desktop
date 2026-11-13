# Preserve trip-payment link and adjustment dates

Trip-payment links/releases now store Gregorian ISO dates. Retained field/hash checks and original supplier-refund/reversal sources parse dates with the same shared storage helper.

All six Arabic/Thai probes failed before this change. New records stored different years; existing retained links threw a parse or consistency error. A supplier refund dated after the historical preview reduced its payment availability from $100.00 to $70.00 under Thai settings; Arabic settings failed to read that adjustment.

After changing two production files, all 32 related Release cases pass. Coverage includes exact link/release retries without a watermark change, original snapshot hashes, historical availability, future amount reservations, releases, corrected claim bases and unchanged cash/expense movements. Windows Debug builds with zero warnings and errors.

The parent complete regression passes 1,665 cases at `38a69ac`; cancellation's eight new cases and this change's six are covered by their targeted runs pending the next complete run. No stored history is rewritten. Native resource workflows, remaining financial aggregation work and full device/release acceptance continue separately.
