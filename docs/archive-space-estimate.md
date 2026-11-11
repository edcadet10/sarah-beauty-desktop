# Estimate local archive space

The capacity service reads one database snapshot's logical page size and every referenced managed file version, including replaced evidence. It creates no recovery snapshot or output. An estimate is not a hash verification; archive capture still verifies actual contents.

The archive allowance includes uncompressed source bytes, 1% encoding overhead, and 64 MiB for the bounded manifest, ZIP metadata, and encryption overhead. Actual compression can make the file smaller. Working space reserves two allowances at the source for the copied capture/package and one at the destination for encrypted output. A shared drive root combines those budgets.

Drive-root free space is a point-in-time observation. Unavailable values remain Unknown, and mounted/network storage or later changes can prevent the estimate from predicting an actual write failure. The service does not reserve disk space or promise a successful future write. Known shortages stop automatic archive work before snapshot creation, retain a failed attempt with InsufficientLocalSpace, and leave previous archives intact.

Three fictional cases compare the estimated source total with a subsequently verified actual capture, retain all managed versions, leave financial watermarks unchanged, distinguish insufficient/unknown/sufficient capacity, reject foreign access/unavailable folders/missing evidence, and verify that estimation creates no staging workspace.
