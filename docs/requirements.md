# Sarah Beauty Desktop Requirements

Status: Draft

## Application scope
Sarah Beauty Desktop will be a Windows application intended fo use on a Surface Pro.

The application must:
- Support everyday business operations without internet access.
- Keep the main business records in a local database
- Provide a readable backup of records in Google Sheets.
- Store complete recovery backups in private Google Drive storage.
- Continue working when internet access is unavailable.
- Display the date and time of the last successful backup.

Google Sheets and Google Drive backups require internet access.

## Users and approvals

The application will support two owners.
- Both owners can create and review financial entries.
- Either owner can approve an entry, including their own.
- Draft entries must not affect posted financial totals.
- Approval must validate an entry before recording it in the books.
- Records must identify the creator, approver, and approval time.
- Approvals must work offline.
- Approving the same entry again must not record it twice.

## Acceptance criteria

### AC-001: Approve a valid expense

Given a valid draft expense, when either owner approves it, then the application records the expense once and saves the approver and approval time.

### AC-002: Approve your own entry

Given an owner has created a valid draft financial entry, when that same owner approves it, then the application records the entry once and identifies that owner as both its creator and approver.

### AC-003: Approve while offline

Given the Surface has no internet connection and a valid draft expense is ready for approval, when either owner approves it, then the application saves the expense and approval details locally, updates the financial totals, and marks the cloud backup as pending.
