# Include retained decisions in complete exports

Projection format 3 includes the two verified decision tables in a dedicated group: 31 groups, 161 business tables, 165 source tabs, and one delivery-status tab. The inventory, dictionary, digests, immutable local generation and workbook schema all use the same expanded table set. The dictionary joins exact document versions and recording owners and identifies Gregorian decision dates.

Start Here, conventions and local preview explain captured decision coverage and private omissions. Decision reasons and supporting-file bindings stay excluded from the decision tables. A document decision does not establish a cash refund. Original invoice tax charges remain in their issue period; void reductions belong to their effective period.

Schema 38 excludes older writers from claiming this coverage. Prior completed jobs and artifacts remain history, but do not count as a matching current export or authorize a retry. Rehashed format-2 or earlier-writer manifests fail verification. The migration preserves retained decisions, money and the source watermark and leaves an openable prior-schema snapshot.

Thirty-nine generation/schema/projection/compatibility cases and 33 additional delivery/readback/preview cases pass. The posted-cell test writes and independently reads actual stored decision cells before publication, with private reason sentinels absent and money/watermark unchanged. It uses a local HTTP fixture, not a real Google account.

Windows Debug has zero warnings/errors. Native checks preserve four decisions through upgrade/restart and show 165 source tables plus status and the new notice. Unconfigured delivery remains guarded. One initial native preview wait timed out; a fresh run completed successfully. Read-only checks verify schema 38, watermark 34, seven original versions, four decisions, unchanged $54.50 receipt/application/journal, managed/PDF hashes, no Google metadata, and an openable schema-37 snapshot containing the same history. Full regression and real account/device/release acceptance are separate checks.
