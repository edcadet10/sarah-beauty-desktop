# Native full-volume failure and retry checks

Date: November 12, 2026. Application source: `0a6185b`, downloaded self-contained Windows x64 preview; TEST schema 40. All records and receipt content are fictional. This executes the local out-of-space scenario in AC-054.

## Contained real filesystem failure

A new 128-MiB dynamic virtual disk is created inside the private QA workspace. Before initialization/format, checks bind the fresh RAW disk and its partition to that exact image, verify its size and non-system/non-boot identity, and reject the Windows system drive. Only the new disposable partition is formatted NTFS. A marker and volume label identify it. The Windows system drive retains more than 40 GB free.

The actual app creates a business with two authenticated owners, one customer, and one saved $1 expense draft on that volume. A filler file consumes the disposable volume's remaining allocation. Each failure begins with **zero available bytes**, and the app displays the actual storage error.

| Native action at zero free bytes | Independent failure result | Retry after freeing space |
| --- | --- | --- |
| Approve the valid saved expense | Zero movements, batches, and journal lines; existing customer/permission rows exact | The same draft posts once: one movement, one batch, two lines, 100 cents debit/credit |
| Save a valid long customer note | Customer stays revision 1 with its original empty notes; permission/financial rows exact | The retained editor text saves once as revision 2; exact 8,399-character normalized note, one permission entry |
| Import the selected fictional receipt PDF | Zero attachment-version rows, no saved receipt, no remaining partial file; prior customer/financial rows exact | The same import saves exactly one version; managed/source SHA-256 match, no extra money or permission history |

The customer input is below the 10,000-character field limit; these are storage failures, not rejected invalid data. Each failure's complete protected rows match its prior checkpoint. Failed actions show an error rather than a success confirmation; fields remain usable for retry. Integrity and foreign keys pass after each failure and retry.

## Durable restart and cleanup

The second owner restarts and reads the exact saved note and verifies the managed receipt. The virtual disk is then detached and remounted; a further second-owner native restart retains the same customer revision, permission entry, one posted movement/two journal lines, and single receipt version/hash. Full protected rows match the final successful checkpoint. No partial files remain.

The app closes and the disposable virtual disk is detached. Its private image, source receipt, and detailed checkpoints remain retained; no user drive is filled or reformatted. The [Microsoft virtual-disk](https://learn.microsoft.com/en-us/powershell/module/hyper-v/new-vhd) and [volume formatting](https://learn.microsoft.com/en-us/powershell/module/storage/format-volume) documentation describe the underlying Windows operations.

An initial setup harness omitted **New customer**, and another startup attempt could not select Money before any full-volume action. Setup resumed only after confirming two owners and zero customer/draft/movement rows; navigation retry reacquired the window and reset search. Interrupted attempts were not counted as failure-case passes. No product code or schema change was needed.

This is a real NTFS out-of-space test in an isolated local virtual volume. It does not simulate hardware damage, sudden physical power loss, or actual Surface storage behavior. Those and second-computer recovery remain separate release checks. No Google account or Cloud project is needed.
