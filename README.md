# Sarah Beauty Desktop

## Purpose

This is an app for my wife and me to help us manage her beauty services business. It will help us organize records for our customers, bookings, payments, and expenses. I am building it to help us keep everything in one place and work without an internet connection or any subscription. This project is also an opportunity for me to learn software development by solving a real business problem.

Google Sheets and Google Drive backups will require an internet connection.

## Planned features

- Customer records and appointment history
- Wedding bookings, unnamed party members, trials, and payment schedules
- Quotes, invoices, payment receipts, credits, and refunds
- Income, expenses, supplies, assets, debts, and owner transactions
- Financial approvals, bank reconciliation, and reports
- A business dashboard with charts and links to supporting records
- Google Sheets exports and complete Google Drive backups

## Technology

- C# and .NET 10
- WinUI 3 for the Windows interface
- SQLite for local data storage (planned)

## Project status

The initial Windows application builds and launches on the development computer. It displays a Sarah Beauty welcome screen. Customer records, bookings, bookkeeping, local storage, and backups are still planned. There is no published installer yet.

The first target is a Surface Pro 7. Everyday work will happen locally on Windows. Google Sheets will hold a readable export, and private Google Drive storage will hold recovery backups. These are backups of the desktop records, not a second place to edit the books.

The project targets $0 in required recurring software subscriptions. Available storage, API limits, and dependency licenses will be checked before integration; no paid plan is assumed.

## Run the development preview

The current development setup uses Windows, the .NET 10 SDK, and Windows Developer Mode. The first build needs internet access to restore the project's packages.

From the repository root, run:

```powershell
dotnet run --project .\src\SarahBeauty.Desktop\SarahBeauty.Desktop.csproj
```

The welcome screen has been manually checked on the development computer. Surface Pro 7 compatibility and touch behavior remain to be tested. The preview does not store business records.

## Project documentation

| Document | What it covers |
|---|---|
| [Requirements](docs/requirements.md) | Scope, customer rules, workflows, and permissions |
| [Data model](docs/data-model.md) | Records, fields, IDs, and relationships |
| [Business rules](docs/business-rules.md) | Pricing, deposits, balances, and accounting calculations |
| [Screens and reports](docs/screens-and-reports.md) | Entry forms, dashboard, charts, reports, and client documents |
| [Architecture](docs/architecture.md) | Windows application structure, storage, and posting |
| [Backup and recovery](docs/backup-and-recovery.md) | Sheets exports, complete backups, and restore requirements |
| [Acceptance criteria](docs/acceptance-criteria.md) | Examples that the future application must pass |
| [Implementation plan](docs/implementation-plan.md) | Build order, open decisions, and release checks |

## Demo data

Public examples and screenshots will use fictional data. Business records, customer photos, receipts, credentials, and backups will remain private and outside this repository.

TEST and LIVE data must use separate stores. Passing a documentation check is not evidence that the application works.
