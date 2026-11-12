# Check owner access without extending inactivity

The owner-access service now separates a nonrenewing current-session check from explicit user activity. Both validate the current store instance, session, business, and active owner. A status check does not touch the session or business watermark. An accepted user-activity call renews an active session and cannot revive locked or expired access.

The input adapter limits activity recording to once per second using monotonic time. It still rejects an expired session during a burst. This adapter is for foreground input events; background work and timer-driven status updates must use the nonrenewing check.

All 17 related Release cases pass, including three new cases for authoritative status polling through exact idle expiry without renewal, activity renewal/throttling, unchanged business watermark, and locked/foreign/inactive owner rejection. Existing elapsed-time, manual-lock, and credential/lockout concurrency checks also pass. Desktop event registration and actual idle-screen acceptance remain subsequent work.
