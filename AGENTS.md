# Sarah Beauty development instructions

## Save work to GitHub

The GitHub repository is the shared source of truth. Commit reviewed changes to a focused branch, push the branch, and open or update its pull request. Do not leave completed work only on the development computer. Keep databases, credentials, customer records, and backups outside Git.

## Build in small steps

- Deliver one working feature per pull request.
- Merge focused feature PRs regularly after their required checks pass. Do not collect the entire app in one PR.
- Give each function one clear responsibility and a name that describes its action.
- Keep functions short enough to review at a glance. Extract a helper when it explains a separate operation; avoid arbitrary wrappers and speculative abstractions.
- Keep database validation, metadata operations, and snapshot handling separate.
- Preserve existing work and use focused commits. Do not replace the application with a large rewrite.
- Test meaningful failure cases for changes that can affect stored data.

## Write clear documentation

Use the [Google developer documentation style guide](https://developers.google.com/style) as the editorial reference, after these project instructions. Use direct language, task-based headings, short paragraphs, and numbered procedures. Explain the expected result after a command. Use fictional examples and distinguish implemented behavior from planned work.

See [Contribute a small change](docs/contributing.md) for commands and the current storage boundaries.

## Active full build

The user requested the full app on November 10, 2026. Continue through the agreed requirements and acceptance scenarios; status questions and PR-size guidance steer this work rather than cancel it. Read `docs/build-progress.md` when resuming. Keep tests, device checks, Google authorization, and live setup distinct, and do not claim completion while required workflows or checks remain pending.
