# Download a Windows developer preview

The developer preview is a self-contained Windows x64 build for review. It is unsigned. Physical-device, clean-machine, signing, and complete release acceptance remain open. Google authorization and delivery are optional and do not gate the offline app.

## Download and open the app

1. Sign in to GitHub with read access to this repository. Open **Actions > Windows developer preview**.
2. Choose a successful run. Download its `SarahBeauty-Windows-preview-<source revision>` artifact before it expires. This workflow retains artifacts for 14 days. See [GitHub's artifact download guide](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts).
3. Extract the entire archive. Keep its contents together. Open `SarahBeautyDesktop/SarahBeauty.Desktop.exe`.

The folder includes the Windows and .NET runtimes, the recovery helper, and third-party notices. No customer database, owner passphrase, Google credential, or preconfigured test business is supplied. The preview does not install or sign itself.

The [verified offline preview](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43438361774/artifacts/14045930374) expires November 27, 2026. It adds [password or PIN unlock](owner-pin-unlock.md) and [voluntary exit review](workspace-exit-review.md) to the earlier Money/resource/trip/recovery improvements. With the exact downloaded executable's outbound network blocked, PIN setup/unlock/removal, both-owner PIN/password restart and status recovery pass with every saved business table retained. Its 1280/500 layouts and protected inputs also pass. Blocked-network archive review passed on `6cd916a`; recovery sources are unchanged. No Google account or Cloud project is required.

## Build another revision

After this workflow is on the default branch, choose **Run workflow**, enter a repository branch, tag, or commit in **revision**, and start the run. The selected source must contain `tools/publish-desktop.ps1`. Building in GitHub does not run the app or create a business database.

The default is the verified offline preview commit `818ec4f243ddf5966fcae8d325b30b9c3deea401`. Its business/test/recovery/tool inputs are byte-identical to the [complete hosted PIN check](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43433503257), which passes all 1,700 tests, recovery interoperability and Windows Debug with zero warnings/errors. An independent local full run also passes all 1,700 without a hang. The changed desktop sources separately pass clean Debug, GitHub self-contained publish and the downloaded-app checks. Choose another revision explicitly when reviewing a newer build.

`source-manifest.json` records the exact checked-out source commit, target, unsigned preview status, and each published file's path, byte count, and SHA-256 hash. The artifact name uses the source commit, which can differ from the workflow branch's commit. A failed publish or empty output fails the job. Packaging rejects database, recovery archive, and key-file extensions.

## Verified downloads

The workflow was exercised through a temporary push trigger before enabling manual use; that trigger is removed from the final workflow.

| Source commit | GitHub run | Download check |
| --- | --- | --- |
| `a886ca9e1fc5fc9b18f7530a31aa16ac0dffbcc7` | [43350179467](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43350179467) | All 574 paths, byte counts, and SHA-256 hashes match; second owner reads all six complete customer fields |
| `8dda1a7cf5e2e4c809fb9e3ce22526c1da7379d5` | [43354309923](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43354309923) | All 574 paths, byte counts, and SHA-256 hashes match; second owner reads deliberately edited loan terms and complete unchanged evidence |
| `0a6185b069d622700cf5ff081458e3eb881704b0` | [43357524913](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43357524913) | All 574 paths, byte counts, and SHA-256 hashes match; installed app retains the offline backup layout and verifies/decrypts its local archive under an outbound network block |
| `96da46d780ccac01f369a5786e07186251ea9844` | [43368056716](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43368056716) | All 575 paths, byte counts, and SHA-256 hashes match; extracted executable icon matches the expected 32-pixel frame; second owner reads edited multiline terms and complete unchanged evidence |
| `2912a34be0e9889723877164aef3a9eaf70371a3` | [43371951319](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43371951319) | All 575 paths, byte counts, and SHA-256 hashes match; second owner repeats New/reload/Keep/Discard/navigation in all five resource registers; every saved database table remains exact |
| `a1a4e2a65e1c8e86e7588cac98aea9341f8396a5` | [43375431442](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43375431442) | All 575 paths, byte counts, and SHA-256 hashes match; nested asset New/Keep, parent-save Keep, and New/Discard pass with unchanged tables; second-owner asset reads and all five main register probes pass |

| `d1fbb426dcf96244f647ad3cc496afc762d703f8` | [43387168315](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43387168315) | All 575 paths, byte counts and SHA-256 hashes match; resource paragraphs/edit guards and blocked-network archive review pass |
| `6cd916a4127ce17a2032e101bbcf78bf694f7c9c` | [43392094460](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43392094460) | All 620 paths, byte counts and SHA-256 hashes match; 29 runtime packages/44 notices, trip draft guards, second-owner paragraphs and blocked-network archive review pass with retained saved tables |
| `81afd6d0f0f062cc462bc563f1ef5cf385325fd5` | [43402725739](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43402725739) | All 620 paths, sizes and hashes match; Money Keep/Discard/reversion/filter probes, second-owner posting read and rejected-approval control restoration pass |
| `6b753ca7ed6dc8d73ae0d1436981c3f0b7471d9c` | [43408828743](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43408828743) | All 620 paths, sizes and hashes match; 29 packages/44 notice hashes, exact matching-receipt context, second-owner reads, Money draft guards and 1280/500 row layouts pass; saved tables remain exact |
| `97dffa1c0e6f5ba76fb5340457ac896a477a1019` | [43432004423](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43432004423) | All 620 paths/sizes/hashes match; cached customer/Money drafts survive cancelled lock/close, repeated close and another modal; explicit exits and second-owner restart retain saved tables |
| `818ec4f243ddf5966fcae8d325b30b9c3deea401` | [43438361774](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43438361774) | All 620 paths/sizes/hashes and 44 notice hashes match; outbound-blocked PIN setup/unlock/removal and password fallback, both-owner restart, status-race recovery and 1280/500 protected-input/action layouts pass; all saved tables remain exact |

All listed jobs succeeded. None of the downloads contains the rejected business-data file types. These checks establish packaging and the stated native probes; they do not certify production readiness or every app workflow. The [complete feature regression](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43389582998) passes all 1,684 cases, recovery interoperability and the Windows Debug build with zero warnings/errors.

## Observed runtime independence

The downloaded `0a6185b` app records .NET 10.0.15 as an included framework, with no shared-framework requirement in its runtime configuration. Its publish profile enables both .NET and Windows App SDK self-contained deployment and disables trimming. See [Microsoft's self-contained deployment guide](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps).

An actual startup module inventory resolves `coreclr.dll`, `Microsoft.WindowsAppRuntime.dll`, `Microsoft.UI.Xaml.dll`, `Microsoft.UI.Xaml.Controls.dll`, and `e_sqlite3.dll` inside the extracted app folder. Every observed loaded module is either app-local or under Windows; none comes from a developer SDK/shared-runtime directory. The probe closes normally and retains the fictional business. This directly checks the observed loads; it is not a substitute for a clean-machine or physical Surface check.
