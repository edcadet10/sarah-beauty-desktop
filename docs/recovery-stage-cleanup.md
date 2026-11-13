# Finish cleanup before returning from recovery

Hosted [run 43378496537](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43378496537) passes 1,680 storage cases and fails the existing cancellation/timeout case: fixture removal finds `output.partial` still in use. Recovery cryptography passes. This is a failed regression, not release evidence. The runner does not identify the process holding that file.

The worker already requests termination and waits. Its output-stage cleanup instead swallowed an in-use-file exception and returned while the owned temporary file remained. A controlled exclusive-file lease reproduces that cleanup gap without relying on process timing: the original cleanup, exposed through an async adapter, returns immediately and fails the new completion assertion.

## Wait for a transient lease

The output stage now supports asynchronous disposal. Encryption/decryption, protected recovery-file saving, and full-archive creation await it. Cleanup retries only Windows sharing/lock violations, for at most 40 attempts with 25-millisecond waits. Other errors and a persistent lease report `TemporaryOutputCleanupFailed` with a fixed diagnostic. Existing synchronous callers use the same cleanup path.

Cleanup deletes only this operation's known temporary output and then attempts a nonrecursive directory removal. Unexpected files remain untouched. Existing destinations, publication/hash checks, encryption formats, owner authority, and database schemas do not change. Cancellation does not cancel the cleanup waits.

## Validate the failure case

The claim is that cleanup no longer silently reports completion while its known output remains leased. Completion before release, deletion of unrelated content, altered retained bytes, or publication during a failed operation rejects it.

- The held-lease test fails before the change and passes after release with no published destination or remaining stage.
- A persistent lease produces the explicit failure, retains its bytes, publishes nothing, and permits cleanup after release.
- An unexpected sibling file retains its exact contents while the known output is removed.
- All nine cleanup/cipher Release cases and all eleven full-archive Release cases pass. Five additional consecutive runs of the original cancellation/timeout case pass. The original no-publication/no-stage assertions remain unchanged.

The complete failed branch must receive this correction and pass a fresh full regression before merging. These tests establish the observed cleanup behavior; they do not identify the original runner's file-lock holder or establish physical power/device acceptance.

Microsoft documents the in-use-file exception in [File.Delete](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.delete?view=net-10.0), and describes awaited cleanup in its [async disposal guide](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync).
