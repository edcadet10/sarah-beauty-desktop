# Pace readable delivery requests

The delivery pacer spaces access starts by at least 1,100 milliseconds using monotonic elapsed time. Callers in this process share one pacer per Google account. A cancellable semaphore prevents concurrent callers from reserving the same slot, and early timer wakes recheck the remaining delay. Cancelled waits release the gate without consuming a slot.

Google currently documents [60 reads and 60 writes per user per project per minute](https://developers.google.com/workspace/sheets/api/limits). The chosen spacing limits this pacer to at most 55 starts in any half-open rolling minute. It does not reserve quota across devices, other processes, applications, or projects, and it cannot prevent all quota responses. It does not enable billing or request quota increases. The worker must recheck current permission and connection after waiting and before each request.

The three focused tests use a virtual monotonic clock for 65 starts across minute boundaries and early wakes, plus an actual cancellable asynchronous wait to check gate release. Integration into the delivery worker and bounded quota backoff remain subsequent work.
