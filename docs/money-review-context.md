# Review the exact Money record

The downloaded `81afd6d` app stores a receipt's payer, payment method, external reference and provider, but does not show those values in saved review. Native inspection of a fresh fictional receipt reproduces the omission; independent saved rows confirm that the values exist. Its list also presents identical descriptions, dates and amounts without a record reference.

## Show retained identity and payment details

Money rows now wrap the description, date/kind/amount/status and full draft reference. Selection and unfinished-form restoration use the source ID and revision. The shared emerald, cream and subtle-gold theme remains in use.

Saved review shows the draft reference/revision, posted movement reference when present, payer and related booking references, payment method, external reference/provider, uniqueness claim, and relevant tax-review values. Payer and booking names are explicitly labelled as current names; their permanent source IDs remain visible. Missing values are labelled rather than invented. Journal lines show the account code and name.

Three small display helpers and shared field-visibility predicates keep this change in the desktop UI. No financial calculation, saved schema, authorization, posting, recovery or network behavior changes.

## Try to confuse two matching receipts

The claim is that a reviewer can identify the selected record and its retained payment context without losing unfinished-field protection. Selecting the wrong source, showing the other payer/reference, changing saved data during cancellation, or adding a duplicate posting would reject the claim.

- Two customers share a fictional name. Two receipts share their description, date and $3 amount, but retain distinct customer IDs and payment references. Native selection by each full draft reference displays its own payer ID, payment method and external reference.
- Editing A and selecting B retains A after Keep editing; Discard changes opens exact B. Every stored table remains unchanged before approval.
- The authenticated second owner approves B and then A. The second receipt retains the similar-receipt warning. Independent rows contain two posted source revisions, two movements and four journal lines balanced at 600 cents per side, with both original payer IDs and references retained.
- Second-owner restart opens both posted records with their exact context. Read-only checkpoints retain every saved table, database integrity and foreign keys.
- Native 1280- and 500-pixel layouts keep both full row references within the list. App-only captures were visually inspected. These window widths do not establish physical Surface, touch or display-scaling acceptance.
- The self-contained local publish passes the existing Money Keep/Discard, invalid-input retention, cached navigation, exact reversion, account/checkbox and search-filter probes without a save or approval. The earlier separate posting fixture remains exact.

Windows Debug builds with zero warnings/errors. The self-contained publish succeeds with 620 files and the runtime-notice inventory. A new GitHub-built download remains to be verified before replacing the default preview. Broader accessibility, global lock/shutdown and physical-device acceptance remain open.
