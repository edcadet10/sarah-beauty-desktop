# Use the selected palette in new client PDFs

Date: November 12, 2026. Source: `fc30eec`. New client PDFs use emerald `#174E3C` for the business heading, cream `#EEE6D4` for table headings, and restrained gold `#86682A` for thin table borders. Body text remains black on white. These values share the owner's selected desktop palette; `DocumentColors` keeps the PDF constants together.

An independently read earlier PDF retained the former plum heading `#61374C`. Native newly issued quote/invoice PDFs now contain the emerald text, cream drawing fills, and gold strokes. Calculated contrast is 9.58:1 for emerald on white, 16.90:1 for black on cream, and 5.21:1 for gold on white. These are [text/non-text design checks](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html), not accessibility certification or proof of aesthetic preference.

## Validation

- Recovery helper built before project evaluation; Debug desktop build passes with zero warnings/errors.
- All four existing PDF tests pass: private-field exclusion/retained rendering, 90-service multipage output, failure/retry, and changed/unsafe artifact rejection.
- Fresh native fictional TEST documents include 148/146-character payer names, a long billing address, nine grouped guests, 20 wrapped extra descriptions, and split payers. The quote has 23 charge lines and four pages; the guest invoice has 21 lines and four pages. Quote first/middle/last and guest continuation renders were inspected. Table headings repeat and text fits the pages; stored totals and every snapshot description match extracted text.
- All saved PDF hashes, TEST marks, private-sentinel exclusion, exact share/milestone totals, zero financial movements, integrity, and foreign keys pass. Earlier payer-fixture financial/document checkpoints remain unchanged.

## Separate issue found by the walkthrough

This color change does not pass complete AC-019 acceptance. The quote retains 4,128 characters of multiline terms, while both newly prepared invoice snapshots retain only their first 226-character paragraph. Native quote-revision and invoice-editor probes reproduce the truncation. Correct multiline initialization is a separate fix; issued test snapshots/PDFs remain retained rather than overwritten. Automation setup interruptions were resumed from verified stored state and were not counted as passes.

Changing the renderer does not rewrite an existing issued PDF. Real Google delivery, complete document/device acceptance, signing, and release remain open.
