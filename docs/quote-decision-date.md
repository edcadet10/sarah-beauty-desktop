# Reject future quote decisions

Quote decline, withdrawal and void now require a decision date from issue through the current local day, matching the invoice-void boundary. The check runs against the current issued source inside the writer transaction. It does not schedule a future state change.

A regression initially showed that tomorrow's quote decision changed today's cached status. The corrected case rejects the write without changing status/history/watermark, then records today's decision and verifies that an exact retry still returns it after an injected clock rollback. Nineteen date/quote/projection/generation/workbook Release cases pass. The culture case continues to verify Gregorian date output using a permitted decision date. No Windows system clock was changed.
