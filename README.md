# Sarah Beauty Desktop

## Purpose

This is an app for my wife and me to help us manage her beauty services business. It will help us organize records for our customers, bookings, payments, and expenses. I am building it to help us keep everything in one place and work without an internet connection or any subscription. This project is also an opportunity for me to learn software development by solving a real business problem.

The app must run fully offline, including owner sign-in, approvals, records, reports, PDFs, and local encrypted backup/recovery. No Google account or Cloud project is required. Optional Google Sheets/Drive features require a separate explicit setup and remain disabled until authorized.

## Feature scope

- Customer records and appointment history
- Wedding bookings, unnamed party members, trials, and payment schedules
- Quotes, invoices, payment receipts, credits, and refunds
- Income, expenses, supplies, assets, debts, and owner transactions
- Financial approvals, bank reconciliation, and reports
- A business dashboard with charts and links to supporting records
- Complete local encrypted backups and reviewed recovery
- Optional Google Sheets exports and Google Drive copies

## Technology

- C# and .NET 10
- WinUI 3 for the Windows interface
- SQLite through Microsoft.Data.Sqlite for local data storage

## Project status

See [Finish the offline release](docs/offline-release-status.md) for the verified preview, remaining usability/device/release checks, and private LIVE setup requirements. No Google setup is required for offline use.

The reviewed feature stack is merged into `main` through small GitHub pull requests. An [unsigned Windows preview](docs/windows-preview.md) is available. Development checks use fictional TEST businesses; LIVE setup requires the owners' actual reviewed facts. See [Build progress](docs/build-progress.md) for current coverage and dated evidence.

The implemented local core includes two-owner sign-in and approvals, customers, service prices, bookings and recipient places, appointment planning and calendar review, combined cancellation decisions and history, versioned client documents and PDFs, bookkeeping, payment applications and funded credits, resource registers, supplier bills and refunds, statements, bank matching, reconciliation, and retained period close/reopen reports.

The complete hosted PIN regression passes all 1,700 cases, recovery interoperability and Windows Debug with zero warnings/errors; an independent local full run passes all 1,700 too. Its business/test/recovery/tool inputs match the verified download; desktop build/native checks separately cover the new interface. All 620 downloaded files and runtime notices match. Fictional walkthroughs cover the local workflows, balanced reports, encrypted backup/review, draft/exit protection, complete input, installer retries, full-volume failures and second-owner restart. Physical Surface and second-computer release acceptance remain open.

Optional [Google connection](docs/desktop-google-connection.md), [Drive archive](docs/desktop-google-archives.md) and [readable Sheets](docs/desktop-sheets-controls.md) controls remain disabled without separate configuration and authorization. Real consent/delivery acceptance applies only before enabling those optional outputs; it does not gate the offline app.

[Ownership settings](docs/desktop-ownership-settings.md) now retain dated, reasoned percentage confirmations and corrections with optional managed evidence, exact totals, explicit unknown dates, and history. [Readable ownership coverage](docs/readable-ownership-generation.md) is included in projection format 2. Percentages create no financial allocation or tax-basis assumption.

[Owner security](docs/desktop-owner-security.md) supports private own-passphrase changes and [password or PIN unlock](docs/owner-pin-unlock.md). Each owner's optional 6–12 digit PIN is protected in the Windows profile, requires the password to set/remove, and shares the sign-in lockout. Password changes invalidate that owner's prior PIN. The verified download passes setup/unlock/removal and password fallback with its outbound access blocked. Reviewed archive recovery still requires portable recovery material and a new writable instance.

Remaining work includes form/accessibility and global unsaved-work review, actual Surface and second-device recovery acceptance, clean-machine/distribution/signing checks, and private LIVE setup. Keep local archives until the owners choose a retention policy. The current public download is an unsigned portable preview.

The first target is a Surface Pro 7. Everyday work and complete encrypted backups operate locally on Windows. An owner can later choose optional readable Sheets exports and private Drive copies. Those online copies never become a second place to edit the books.

The project targets $0 in required recurring software subscriptions. Runtime packages have a [checked license/notice inventory](docs/runtime-package-notices.md); final distribution-conditions review remains open. No paid plan is assumed.

## Run the development preview

The current development setup uses Windows, the .NET 10 SDK, Go 1.27.4, and Windows Developer Mode. The first build needs internet access to restore the project's packages and pinned encryption library.

From the repository root, run:

```powershell
./tools/build-recovery.ps1
dotnet run --project .\src\SarahBeauty.Desktop\SarahBeauty.Desktop.csproj
```

Development runs unpackaged by default, with the project directory as its working directory so the app can find its icon. On startup it creates or opens `%LOCALAPPDATA%\SarahBeautyDesktop\Test\sarahbeauty.db`, outside the repository. An empty store offers two-owner TEST setup; existing stores require local sign-in. Use fictional records during development.

The shared business project manages SQLite schema version 40. Metadata retains the permanent Store ID, TEST mode, and writable-instance identity/state. Business access rejects stale or retired instances and sessions. Opening validates identity, mode, and supported versions; archive and pre-upgrade snapshot checks also verify integrity and foreign keys. Retained document decisions, their history and desktop controls are implemented.

Before upgrading an older store, initialization creates a verified database snapshot in the data folder's `Backups` directory, then applies migrations atomically. A failed snapshot or migration stops the upgrade. Reopening a current store preserves its identity without creating another upgrade snapshot.

Run the automated checks with:

```powershell
dotnet test .\tests\SarahBeauty.Storage.Tests\SarahBeauty.Storage.Tests.csproj -c Release
```

The tests use temporary fictional SQLite stores and the same business assembly as the desktop app. They include historical balance limits, concurrent/stale decisions, interrupted transactions, migrations, document integrity, scheduling boundaries, complete encrypted archives, and staged restore validation through the real recovery helper. Build the helper before running the checks. Local upgrade snapshots contain only the database; full archives also retain managed files. TEST recovery activation is implemented; actual LIVE facts and second-device acceptance remain pending. Optional Google setup is separate from offline release acceptance.

An earlier welcome-screen preview launched on the Surface Pro 7. The current business workflows, touch interaction, offline behavior, and restore on a second Windows device still need explicit device checks.

## Publish a Surface test copy

From the repository root, run:

```powershell
./tools/publish-desktop.ps1
```

This builds the recovery helper, publishes the self-contained app, copies its runtime notices and checks required files. Copy the entire `.artifacts/publish/win-x64` folder onto the target computer, then open `SarahBeauty.Desktop.exe` from that folder. Extract a ZIP before launching if one is used for transfer.

Generated distribution files are ignored by Git; commit source, settings and documentation. This is an unsigned development preview. [Installer tooling and interrupted retry checks](docs/current-offline-installer.md) are implemented; clean-machine and final distribution acceptance remain open.

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
