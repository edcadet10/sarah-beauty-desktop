# Sarah Beauty Desktop

## Purpose

This is an app for my wife and me to help us manage her beauty services business. It will help us organize records for our customers, bookings, payments, and expenses. I am building it to help us keep everything in one place and work without an internet connection or any subscription. This project is also an opportunity for me to learn software development by solving a real business problem.

Google Sheets and Google Drive backups will require an internet connection.

## Feature scope

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
- SQLite through Microsoft.Data.Sqlite for local data storage

## Project status

The full build is in progress in small GitHub pull requests. The app remains in TEST mode. See [Build progress](docs/build-progress.md) for current coverage and dated verification evidence.

The implemented local core includes two-owner sign-in and approvals, customers, service prices, bookings and recipient places, appointment planning and calendar review, combined cancellation decisions and history, versioned client documents and PDFs, bookkeeping, payment applications and funded credits, resource registers, supplier bills and refunds, statements, bank matching, reconciliation, and retained period close/reopen reports.

The latest complete .NET suite has 1,020 passing cases, with four separate recovery-crypto test groups. The Windows Debug build succeeds with no warnings or errors. Fictional desktop walkthroughs cover selected workflows, manual/scheduled local encrypted backups, controlled TEST recovery activation, Google client import, Drive setup guards, and restart persistence. Simulated Google delivery also transfers and stages real encrypted archive bytes. These checks do not establish real Google delivery, full release acceptance, or current Surface compatibility.

The app now has [Google Desktop connection controls](docs/desktop-google-connection.md), including private client import, PKCE browser consent, account review, cancellation, and local disconnect. [Drive archive controls](docs/desktop-google-archives.md) add private-folder setup, explicit delivery permission, manual delivery, recurring delivery of completed local archives, and durable retry/history/health. Actual Google consent/delivery acceptance and readable Sheets export remain pending.

Remaining work includes further managed-evidence selection within entry forms, retention controls and second-device recovery acceptance, readable Sheets export and real Google acceptance, separate LIVE setup, owner-access and unsaved-work hardening, an installer, and full release/device checks. There is no published installer yet.

The first target is a Surface Pro 7. Everyday work will happen locally on Windows. Google Sheets will hold a readable export, and private Google Drive storage will hold recovery backups. These are backups of the desktop records, not a second place to edit the books.

The project targets $0 in required recurring software subscriptions. Available storage, API limits, and dependency licenses will be checked before integration; no paid plan is assumed.

## Run the development preview

The current development setup uses Windows, the .NET 10 SDK, Go 1.27.4, and Windows Developer Mode. The first build needs internet access to restore the project's packages and pinned encryption library.

From the repository root, run:

```powershell
./tools/build-recovery.ps1
dotnet run --project .\src\SarahBeauty.Desktop\SarahBeauty.Desktop.csproj
```

Development runs unpackaged by default, with the project directory as its working directory so the app can find its icon. On startup it creates or opens `%LOCALAPPDATA%\SarahBeautyDesktop\Test\sarahbeauty.db`, outside the repository. An empty store offers two-owner TEST setup; existing stores require local sign-in. Use fictional records during development.

The shared business project manages the SQLite schema, currently version 32. Metadata retains the permanent Store ID, TEST mode, and writable-instance identity/state. Business access rejects stale or retired instances and sessions. Opening validates identity, mode, and supported versions; archive and pre-upgrade snapshot checks also verify integrity and foreign keys.

Before upgrading an older store, initialization creates a verified database snapshot in the data folder's `Backups` directory, then applies migrations atomically. A failed snapshot or migration stops the upgrade. Reopening a current store preserves its identity without creating another upgrade snapshot.

Run the automated checks with:

```powershell
dotnet test .\tests\SarahBeauty.Storage.Tests\SarahBeauty.Storage.Tests.csproj -c Release
```

The tests use temporary fictional SQLite stores and the same business assembly as the desktop app. They include historical balance limits, concurrent/stale decisions, interrupted transactions, migrations, document integrity, scheduling boundaries, complete encrypted archives, and staged restore validation through the real recovery helper. Build the helper before running the checks. Local upgrade snapshots contain only the database; full archives also retain managed files. TEST recovery activation is implemented; LIVE setup, Google backups, and second-device acceptance remain pending.

An earlier welcome-screen preview launched on the Surface Pro 7. The current business workflows, touch interaction, offline behavior, and restore on a second Windows device still need explicit device checks.

## Publish a Surface test copy

From the repository root, run:

```powershell
dotnet publish .\src\SarahBeauty.Desktop\SarahBeauty.Desktop.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:WindowsAppSDKSelfContained=true `
  -p:WindowsPackageType=None `
  -p:PublishTrimmed=false `
  -o .\src\SarahBeauty.Desktop\publish\surface-preview
```

This creates an unpackaged folder containing the app, its .NET and Windows App SDK runtimes, and its image/icon assets. Copy the entire `surface-preview` folder onto the target computer, then open `SarahBeauty.Desktop.exe` from that folder. Extract a ZIP before launching if one is used for transfer.

The project's asset copy settings include all nine image/icon files in build and publish output. The `publish/` output is ignored by Git; commit the project settings and documentation rather than the generated distribution files. This folder is a development preview; installation and update behavior remain to be designed.

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
| [Contribute a small change](docs/contributing.md) | Short functions, checks, GitHub workflow, and documentation style |

## Demo data

Public examples and screenshots will use fictional data. Business records, customer photos, receipts, credentials, and backups will remain private and outside this repository.

TEST and LIVE data must use separate stores. Passing a documentation check is not evidence that the application works.
