# Measure owner inactivity with elapsed time

Owner sessions now expire after 15 minutes of monotonic elapsed inactivity. UTC expiry is informational; protected reads and confirmation use the session's active state. Polling that state does not renew it. Foreground authorized access renews the elapsed window, while manual lock or observed idle expiry is terminal for that session object.

The [TimeProvider timestamp](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider.gettimestamp?view=net-10.0) and [elapsed-time APIs](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider.getelapsedtime?view=net-10.0) separate interval measurement from the system's UTC clock. Microsoft's [Windows timing reference](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps) describes the monotonic performance counter used by managed timing. No system clock was changed during testing.

## Check clock changes without waiting or changing Windows

An injected clock first exercised the original UTC-deadline algorithm: backward UTC after 16 elapsed minutes kept the session active and allowed a protected read, while forward UTC expired a fresh session early. All three cases failed before the elapsed-time correction. All 14 related Release cases pass afterward, including five idle cases for those observations, exact idle expiry without status-poll renewal, and foreground renewal. Existing manual-lock and credential/lockout concurrency cases also pass.

This exposes a nonrenewing active-state check for the upcoming desktop idle-lock interface. Actual UI idle timing, user-gesture renewal, credential changes/recovery, and broader unsaved-work handling remain separate work. Physical suspend/resume acceptance is not established by virtual-clock tests.
