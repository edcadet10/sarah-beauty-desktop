# Contribute a small change

Save completed source changes to [GitHub](https://github.com/edcadet10/sarah-beauty-desktop). A local working copy is used to edit and check the app; it is not the only saved copy. Business records and recovery files stay outside the repository.

## Keep each change focused

1. Create a branch for one working feature or fix.
2. Write short functions with a single responsibility and descriptive names.
3. Add a focused check when a failure could damage data or break a business rule.
4. Update the affected instructions and current progress.
5. Review the staged diff, commit the change, and push the branch to GitHub.
6. Open a pull request that explains the behavior and the checks you ran.

Extract a helper when it makes an operation easier to understand. Avoid large methods, unrelated cleanup, and abstractions for features that do not exist yet.

## Find the storage code

| File | Responsibility |
|---|---|
| `Storage/AppDataPaths.cs` | Locate the current Windows user's TEST folder |
| `Storage/LocalDatabase.cs` | Open the database and coordinate an atomic initialization or upgrade |
| `Storage/DatabaseSchema.cs` | Check supported versions and the expected metadata table |
| `Storage/DatabaseMetadata.cs` | Read the store identity and validate or save TEST mode |
| `Storage/DatabaseBackup.cs` | Create, verify, and publish a self-contained local snapshot |

All paths above are relative to `src/SarahBeauty.Desktop`. The storage tests compile these production files directly, including the real path helper, without loading WinUI. Fixtures call an internal entry point with a temporary database path. They do not open the user's application data folder. Move storage into its planned infrastructure project when that layer is introduced; do not maintain a second implementation for tests.

## Check a change

Prerequisites: Windows and the .NET 10 SDK. Restore requires internet access on the first run. `NuGet.Config` uses nuget.org and clears machine-specific fallback folders.

1. From the repository root, run the storage checks:

   ```powershell
   dotnet test tests/SarahBeauty.Storage.Tests/SarahBeauty.Storage.Tests.csproj --configuration Release --verbosity minimal
   ```

   A successful run reports all cases passed. The checks use temporary fictional stores, then delete their fixture folders.

2. Build the Windows app:

   ```powershell
   dotnet build src/SarahBeauty.Desktop/SarahBeauty.Desktop.csproj --configuration Debug --verbosity minimal
   ```

   A successful build reports zero errors. Building does not establish that the interface works on the Surface.

3. Review the changes before committing:

   ```powershell
   git diff --check
   git diff --cached
   ```

   Confirm the diff contains only the intended source, fictional examples, tests, and documentation. GitHub Actions repeats the storage checks and Windows build for pull requests.

## Write documentation

Follow the [Google developer documentation style guide](https://developers.google.com/style) for clear, consistent explanations. Follow its [procedure guidance](https://developers.google.com/style/procedures) for numbered tasks: state the goal, show the action or command, and explain the result. Project terminology and the reader's needs take precedence.

Record what was actually checked. Keep automated storage results separate from interface checks, Surface checks, and full recovery rehearsals.
