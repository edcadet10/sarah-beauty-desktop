# Record an actual trip and reimbursement review

1. Open **Resources**, choose **Actual trips**, and select **Show register**.
2. Select an existing trip or **New register entry**. Enter the actual date, business purpose, origin, destination, and distance.
3. Identify exactly one driver: an owner or an artist. Link the relevant booking when known. A historical trip can retain its original driver after that person is archived; a new trip needs an active driver.
4. Enter actual parking and tolls separately. Select Unreviewed, Not requested, or Requested for the reimbursement review.
5. Enter an explicitly reviewed reimbursement request when known. A blank amount remains unknown. Zero is an explicit amount and cannot represent a positive Requested claim. Do not infer the request from the price quoted to a customer.
6. Add the actual evidence and select **Save actual trip**. Expand **Trip review history** to see retained details, amounts, actor, and time.

Each saved review retains its actual route, driver, distance, costs, request status, amount, evidence, and archive state. Older records without review history begin retaining reviews when next saved; the app does not invent earlier reviews.

An actual distance correction does not rewrite a customer's travel charge or an earlier review. This log applies no tax mileage rate and posts no expense or reimbursement payment. Actual financial entries require their own review and approval.

## Link an approved payment

1. Approve the actual paid expense or owner reimbursement under **Money**. If an owner-paid expense is already recognized, the later owner reimbursement clears its payable instead of recording that expense again.
2. Save the trip fields. Expand **Trip reimbursement payments**, enter the payment review date, and select **Load trip reimbursement review**.
3. Review the saved driver and request. The screen separates payments linked to the current claim from payments belonging to an earlier reviewed basis. Source rows retain their original claim, payment, evidence, approver, and time.
4. Expand **Link an approved reimbursement payment**. Select the actual approved source, date, and amount. Record how the payment paid the identified driver and its supporting evidence.
5. Select **Review trip payment link**. Confirm the captured source, amount, payee review, and available value. Enter your owner passphrase and select **Approve trip payment link**. Changed fields require another review.

Partial payments and shared payments are bounded against both the reviewed claim and actual payment. Linking creates no additional expense or cash movement. Unsaved trip fields must be saved first. Active links protect the economic trip details and reviewed claim; changing only decimal precision, such as `24.8` to `24.80`, preserves the same basis.

## Correct payment attribution

1. Load the saved trip's reimbursement review. Expand **Original payment detail and release**, select the original link, and review its retained source details.
2. Enter the actual release date, amount, reason, and evidence. Select **Review trip payment release**.
3. Review the captured release, enter your owner passphrase, and select **Approve trip payment release**.

Release corrects attribution and preserves the original link. It does not refund cash or undo an actual expense/payment. Record any actual financial return or correction separately. A payment source return or cancellation requires sufficient dated releases first. Each trip's links and releases proceed in date order.

To change a linked claim's driver, actual basis, or requested amount, release its active attribution, save the explicit reviewed correction, and review any new links. Earlier claim/payment snapshots remain available. **Retained trip payment links** and **Retained trip payment releases** use 100-record pages.
