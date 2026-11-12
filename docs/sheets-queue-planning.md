# Choose the next readable export without duplicating pending work

The planner checks current Sheets consent and the owner-confirmed automatic profile inside the writer. It observes verified coverage for the selected dates, current business watermark, existing jobs, creation checkpoints, and retry time. Covered data needs no capture. New changes wait for the configured minimum interval after matching completion.

Pending jobs survive restart and collect later desktop changes as pending coverage. A saved uncertain or known remote identity remains eligible even if report dates changed, so the queue resolves that existing job before creating another generation. An untouched old source for different dates can stay in history while the planner selects a new capture. Unsupported saved schemas need review.

Preparing state precedes capture; attachment compares the exact prior state and current profile/permission before saving the job ID. Stale profile changes cannot attach a job. A live worker is distinguished from an abandoned persisted Running attempt. The planner creates no Google file and sends no HTTP requests.

Three initial Debug cases passed for restart/coalescing, due-time backoff, and stale-profile attachment. All 21 related Release cases pass, including six planning cases that add current coverage/minimum-interval behavior and changed-date uncertain versus untouched source handling. These use fictional local completion records. Queue execution/backoff, runtime integration, and controls remain subsequent work.
