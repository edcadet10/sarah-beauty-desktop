# Authorize automatic local archives

Status: Saved schedule consent, limited archive authorization, and persistent automatic attempt history are implemented. The recurring queue, runtime worker, and desktop schedule controls follow separately. No recurring worker runs in this slice.

## Save consent

An active owner can save an instance-specific schedule with an enabled flag, interval from 1 minute to 7 days, and an existing chosen private destination folder. Automatic archives require separately confirmed portable recovery material. The default is unconfigured; saving settings does not silently start a worker.

Schedule changes require the reviewed revision and are audited once. Repeating identical settings is idempotent. The key includes the current writable-instance identity, so a copied/restored database cannot automatically reuse another device instance's schedule.

## Limit background authority

The internal archive permit binds the exact schedule revision/instance, business, confirmed public recipient/fingerprint, and destination folder. It authorizes only consistent recovery capture and encryption. It contains no passphrase, private recovery material, or human session.

Capture checks current consent before copying and against the captured database. Publication checks the current policy and recovery recipient again. Disabled/revised settings, deactivated confirming owners, changed recovery material, or changed instances invalidate an earlier permit. A completed ciphertext still in staging is not published after revocation. Other destinations are rejected.

Automatic capture/encryption does not touch or renew an owner session. A locked human session stays locked while an already configured archive operation can finish. Automatic completion does not yet imply a running schedule, Google upload, or readable export.

## Retain automatic attempts

Automatic jobs choose a unique new `.age` filename in the confirmed folder and retain their schedule identity/revision, source instance, requested watermark, and independently verified completion facts. Failure and cancellation use fixed codes. A published file whose history cannot be completed stays available for review rather than claiming success. Job metadata never advances the business watermark.

Current health counts only attempts bound to this writable instance. Older unknown-instance and copied-instance records remain in history without claiming current execution or coverage. A new instance therefore needs its own complete archive. Completion checks every immutable attempt fact before replacing Running state.

## Verification

Nine fictional cases cover instance/revision/idempotent settings, actual encrypted output while the human session stays locked, revocation after encryption, active-owner and recovery-key changes, destination boundaries, stale/copied instances, and invalid/foreign setup. Existing complete-archive/source-capture checks run alongside them.
