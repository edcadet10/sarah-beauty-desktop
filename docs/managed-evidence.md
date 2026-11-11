# Keep managed evidence files

Status: Versioned storage, record links, and the desktop evidence workspace are implemented. Managed-file selection within individual entry forms, complete encrypted archives, and restore remain in progress.

## Import and review a file

1. Open **Evidence files**, then select **Start a new file import**.
2. Enter a name, category, and import reason. For a customer photo, select its customer and the exact permission use/scope recorded on that customer. Review permission in **Customers** first if needed.
3. Select **Choose evidence file**, choose a supported file in the Windows picker, then select **Import selected file**. The saved version is available in managed file history.
4. Search/select **Managed file version**, then select **Show selected file version**. Review the original filename, version identities, size, hashes, reason, and permission context.
5. Select **Verify managed file integrity**. Open a verified PDF in your PDF viewer, or preview a permitted image privately inside the app. Leaving the section clears the image preview.
6. To associate evidence, choose its record type, select **Load evidence target choices**, choose the record, enter the purpose, and select **Link reviewed file version**. Expand **File evidence link history** to review the captured association.

Use **Add a replacement file version** after showing the latest version. Choose the new file and record why it replaces the previous scan. Existing links stay with their originally reviewed version. To correct an association, choose an active **Evidence link to release**, enter the reason, and select **Release selected evidence link**.

Starting another import or showing a different file asks before discarding unsaved fields in this workspace. An import or evidence decision already running must finish before its corresponding editor changes. Newer fields are preserved after a late result. Saved decisions remain in history even if a history refresh fails.

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

Thirteen automated file cases cover original removal, replacement history, exact/conflicting retries, stale versions, interrupted copy/publication/metadata writes, changed file/metadata, current/withdrawn photo permission, type/size/cancellation/business boundaries, unsafe retained paths, and version-28 upgrade preservation. Eight link cases cover pinned versions/target facts without money changes, target boundaries, photo restrictions/withdrawal, interrupted decisions and retries, changed link/release snapshots, the originally pinned metadata hash, and version-29 upgrade preservation.

Fictional desktop checks imported a receipt through the native picker, linked version 1 to an existing approved payment, and added version 2 without transferring its link. After restart and moving the original, version 1 still verified. Income, expense, result, and processor balances remained $250/$7.50/$242.50/$242.50. A changed managed PDF was rejected. Separate photo checks retained unsaved input, rejected import before permission, imported/linked/previewed after an exact scoped grant, cleared the preview on navigation, blocked reopening after withdrawal, released the association without opening the image, and retained the withdrawal/release after restart. These development-computer checks do not replace Surface/recovery acceptance.

The import boundaries follow [OWASP's file upload guidance](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html), including generated names, supported types/signatures, size limits, and authorized access. Reparse checks use [File.GetAttributes](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.getattributes).

Private previews use [Windows BitmapDecoder](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.imaging.bitmapdecoder) to inspect dimensions, reject images beyond the preview limits, and decode a scaled image. The original imported bytes remain unchanged.
