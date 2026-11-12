# Read captured asset schedules

Existing query-only asset schedule, book-value, and disposal readers now accept an internal business ID. Existing owner overloads delegate to those same reads; public reports and disposal/depreciation/refund commands retain their owner, approval, evidence, and historical validation requirements.

Previously authorized captured sources can calculate the same cost/depreciation roll-forward, carrying value, disposal state, journal attribution, and zero-money events as the human report. Register facts remain distinct from book values. A registered zero does not create an approved book balance; no approved history remains unknown. Querying a foreign business reveals no asset history. No owner session is created or renewed.

Two new cases and twenty-eight existing asset/refund/disposal/return cases pass in Debug (30 targeted). They compare three months across actual disposal/reversal with human reports, preserve captured register facts after a later rename/lock, and check unknown book value beside registered zero and foreign scope. The complete Release suite before these two additions passes all 918 cases in 2 minutes 5 seconds. Windows Debug has zero warnings/errors. Asset table projection and other report groups follow separately.
