# Allow the complete Windows suite to finish

The storage/build job now has a 45-minute upper bound and supports manual workflow dispatch. All test and build commands remain required, with normal failure behavior. This changes the time available for verification rather than the checks themselves.

The observed PR #156 run passed 892 cases in 10 minutes 42 seconds and took 13 minutes 24 seconds overall. NuGet restores took about 12 seconds for tests and 23 seconds for the app, so restore caching would not address most elapsed time. Subsequent readable worker tests alone take 2 minutes 29 seconds locally; the full local suite through that worker passes 1,181 cases in 4 minutes 38 seconds. The earlier 15-minute job budget leaves little room for this measured growth or slower hosted runners.

The [workflow syntax reference](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idtimeout-minutes) documents the job timeout boundary. Manual dispatch allows the complete current feature branch to be checked before it reaches the front of a focused PR stack. The next hosted run will establish its actual duration; the 45-minute value is a limit, not a runtime prediction.
