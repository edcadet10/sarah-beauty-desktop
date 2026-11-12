# Persist and compare readable export queue state

The instance-scoped queue record retains report-profile and Google-policy revisions, destination binding, observed and delivered watermarks, status, pending job, bounded failure count, next attempt time, and fixed diagnostic code. Preparing can precede job capture; Running requires an attached job. Succeeded requires observed coverage, and failed/cancelled states require a retry time and fixed failure code.

Each insert or update compares the expected previous record inside the writer. Stale callers cannot replace a newly attached job. The reader validates persisted identity, format, timestamps, bounds, and state consistency before returning it. Records are bookkeeping rather than permission to run: the coordinator must independently recheck the current profile and Google consent.

A separate queue lease excludes other queue processes while allowing it to acquire the delivery lease. Tests cover actual SQLite reopen after owner lock, stale insert/update rejection with preserved state, no watermark/session renewal, thirteen corrupt-record cases, and independent lease exclusion/reacquisition. Automatic execution, bounded backoff, runtime integration, and controls remain subsequent work.
