# Keep TEST and LIVE data paths separate

`BusinessMode` names only TEST and LIVE. Stored-name parsing is exact; missing, mixed-case, padded or unknown names do not select a mode. Default paths are separate Test and Live children beneath the per-user application folder.

The existing TEST preview override remains available for isolated fixtures. It cannot redirect LIVE. Lexical checks reject a TEST override equal to, inside, or above the reserved LIVE directory. Path resolution creates neither directory nor database and does not open, convert, promote or authorize a LIVE store.

Eleven mode/path cases pass, including round-trip/invalid-mode handling, default separation, override behavior, overlapping scopes and no filesystem creation. Windows Debug has zero warnings/errors. A native TEST reopen with the new resolver preserves the fictional three-review profile fixture at schema 39/watermark 5, verifies its managed file, and returns to sign-in/closes normally. Store/session mode guards, explicit clean LIVE creation, mode-aware documents and native selection follow separately.
