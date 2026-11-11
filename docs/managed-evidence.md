# Keep managed evidence files

Status: Versioned storage and record links are implemented. The desktop import/open interface follows separately. Complete encrypted archives and restore remain in progress.

Import copies a selected PDF, JPEG, or PNG into the business store's private `Attachments` directory. Moving or deleting the original does not remove this copy. The original file name is retained as metadata; the original absolute path is not stored. Generated filenames, extension/signature checks, and a 50 MiB limit constrain imports. Files are never opened automatically by the import service.

Each saved version retains its name, category, reason, size, SHA-256, owner, time, and previous version identity. Metadata also has a retained hash. A replacement appends a new version after checking the latest version number. It cannot change the category, customer, or permission use. Earlier bytes remain available. The service retains all versions and does not automatically prune files.

The app stages and flushes the copy before publishing its metadata in a database transaction. Failed copies and rolled-back imports remove their incomplete files where possible. If a commit's result cannot be established, the file is retained to avoid deleting a potentially committed version. An unreferenced leftover is not a successful imported record or a verified backup.

Retrying the same request with identical file bytes and metadata returns the existing version. Different bytes or metadata need a new request. If the original has since been removed, select the recorded imported version from history rather than importing it again.

Every content read checks the retained metadata identity/hash and the file's size/hash. Reads hold the verified file stream open rather than reopening a different path. Missing or changed copies require a verified recovery copy. Managed paths accept only generated filenames inside `Attachments` and reject links/reparse points in that managed path. These checks do not make the local store tamper-proof against a Windows administrator.

## Customer photos

Use the **Customer photo** category only with the relevant customer's current **Granted** permission and exact recorded use/scope. The retained version records that permission's identity. A receipt image does not create a photo grant or marketing permission.

Reading an existing photo checks the current customer permission again. A withdrawal, archived customer, or different current scope blocks content access while retaining the version and its original permission evidence. The service accepts JPEG/PNG images for this category; a PDF cannot be imported as a customer photo.

## Link evidence to a record

Each association pins a specific imported version and its retained metadata hash. It also records the target's type, identity, name, revision/status at capture, purpose, owner, and time. Replacing the file or editing the target does not retarget the original link. Linking an approval draft does not approve it or change its financial evidence text. Linking an approved movement adds supporting evidence without rewriting its source.

Supported targets include customers, bookings, money drafts/approved movements, issued documents, supplier bills and registers, assets, loans, supplies, trips, tax periods, artists/catalog services, and document/application/credit/cancellation drafts. The service validates the selected type, business, and reviewed target revision. Duplicate active associations to the same record and file version are blocked. Customer photos can link only to their own permission-reviewed customer record.

Release an incorrect association with an explicit reason. The link and release remain in history; the file is retained. Exact retries return the recorded decision, including its released state, without reactivating it. A withdrawn photo can still have an incorrect association released without opening the image. Reading through a link checks the originally pinned metadata hash and current photo permission as well as file integrity.

## Verification

Thirteen automated file cases cover original removal, replacement history, exact/conflicting retries, stale versions, interrupted copy/publication/metadata writes, changed file/metadata, current/withdrawn photo permission, type/size/cancellation/business boundaries, unsafe retained paths, and version-28 upgrade preservation. Eight link cases cover pinned versions/target facts without money changes, target boundaries, photo restrictions/withdrawal, interrupted decisions and retries, changed link/release snapshots, the originally pinned metadata hash, and version-29 upgrade preservation. Desktop file selection and actual device/recovery checks are separate acceptance work.

The import boundaries follow [OWASP's file upload guidance](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html), including generated names, supported types/signatures, size limits, and authorized access. Reparse checks use [File.GetAttributes](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.getattributes).
