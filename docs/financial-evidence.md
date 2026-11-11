# Select a saved file for a money entry

Status: Optional managed-file selection and captured-source review are implemented in **Money**. Other entry forms retain their existing evidence notes and can use explicit associations in **Evidence files**.

1. Import the receipt or supporting document in **Evidence files**.
2. Prepare or edit a money draft. Select **Load saved evidence files**, then choose an exact **Saved receipt or evidence file** version. Leave **None / external reference only** selected to retain text notes without a managed-file reference.
3. Keep any required evidence or decision notes in the existing reference field. A file selection does not replace a required case-specific review.
4. Save the draft. Review **Captured supporting file** and the separately retained notes. Select **Review captured evidence file**, then verify/open its managed copy if needed.
5. Review and approve the money movement. Approval verifies the selected bytes again. A missing or changed selected file blocks approval and leaves the draft unposted.

The source retains the file version, original name, category, size, content hash, and metadata hash. A replacement scan does not move that reference to a new version. Editing a draft requires an explicit choice of another version. The saved-file picker excludes customer photos, and the business service also rejects photos as financial evidence.

Existing text references stay text; older sources with no file reference remain unconfirmed rather than acquiring an invented attachment. Their absent optional field retains the earlier serialized shape. A later correction can have its own evidence without reopening a missing original file; the original financial source remains unchanged.

Schema 31 raises the minimum writer contract. Older app models must not discard the new reference or bypass its approval check. Upgrading retains a verified database snapshot before raising that version; the selected file bytes and existing records remain in their managed store. This database snapshot is not a complete portable backup.

Eight automated cases cover version pinning, notes versus money, changed/foreign/photo references, missing or changed bytes, rehashed metadata, correction independence, older text/JSON, and version-30 upgrade preservation. The full suite has 522 passing cases. Fictional desktop checks selected receipt v1 while v2 existed, verified the captured file, approved one $10 expense, and retained v1 after restart. January income/expense/result/bank were $0/$10/-$10/-$10; the earlier processor balance stayed $242.50. These checks do not replace full release/device acceptance.
