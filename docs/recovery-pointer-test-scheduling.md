# Run the pointer reader during the concurrency check

The full development suite executed 880 cases: 879 passed, and the pointer concurrency test timed out while waiting five seconds for its `Task.Run` reader to start. The 20 pointer cases then passed in isolation. This result does not demonstrate damaged pointer data.

A separate .NET 10 experiment occupied the only shared-pool worker. The ordinary task could not start, while a `LongRunning` task on the default scheduler started successfully. This supports pool scheduling as a possible cause; it does not establish the exact machine load during the failed run. Microsoft describes this option as a hint to use an additional thread for synchronous work that could block pool progress. See [TaskCreationOptions](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcreationoptions).

The concurrency test now uses that option for its synchronous reader. The five-second startup deadline, forty flushed pointer replacements, complete-selection assertions, reader-failure checks, and temporary-file cleanup assertions remain in place. The changed case passes locally. Production pointer publication and recovery behavior are unchanged. A later full run must establish the complete result.
