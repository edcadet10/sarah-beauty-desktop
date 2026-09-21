# Sarah Beauty Desktop Requirements

Status: Draft

## Application scope
Sarah Beauty Desktop will be a Windows application intended for use on a Surface Pro.

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

## Customer records

Each customer record must have:

- An automatically generated, permanent customer ID.
- A full name.
- Optional phone number and email address.
- A preferred contact method, which can remain undecided.
- Optional billing name and address.
- Optional makeup preferences, sensitivities, and private notes.
- Photo permission status and supporting permission details.
- An active or archived status.
- Creation and last-updated timestamps.

### Customer validation rules

- A name is required. Empty or whitespace-only names are invalid.
- Phone and email are optional.
- When both phone and email are empty, display
    "Contact information missing" without blocking the record.
- Clear that warning when a valid phone number or email is added.
- Different customers may share the same name.
- Customers must be identified by their permanent IDs.
- Customer records must be available for entry and editing offline.
- Customer balances must be calculated from financial records.

## Acceptance criteria

### AC-001: Approve a valid expense

Given a valid draft expense, when either owner approves it, then the application records the expense once and saves the approver and approval time.

### AC-002: Approve your own entry

Given an owner has created a valid draft financial entry, when that same owner approves it, then the application records the entry once and identifies that owner as both its creator and approver.

### AC-003: Approve while offline

Given the Surface has no internet connection and a valid draft expense is ready for approval, when either owner approves it, then the application saves the expense and approval details locally, updates the financial totals, and marks the cloud backup as pending.

### AC-004: Save a name-only customer

  Given an owner enters a customer name without a phone number
  or email address,
  when they save the customer,
  then the application creates a customer ID, saves the record,
  and displays "Contact information missing".

  ### AC-005: Add missing contact information

  Given a customer has no phone number or email address,
  when an owner adds a valid phone number or email and saves,
  then the application saves the change and clears the warning.

  ### AC-006: Reject an empty customer name

  Given a new customer name is empty or contains only spaces,
  when an owner attempts to save,
  then the application explains that a name is required
  and does not create the customer record.
