# Distinguish same-name customer records

Customer list rows now show their existing name/status, available phone/email and full permanent customer reference. Selecting a row still opens the original saved customer, revision and edit fields. Search, saving, archive filtering and the customer data model keep their existing behavior.

The fresh native release walkthrough exposed two distinct same-name records with one visible list label. Before this change, UI Automation confirmed two rows and one distinct label. Afterward it confirms two distinct contact/reference labels, selects each row and verifies the matching saved phone/email fields. An independent read confirms that the records, revisions, approvals and 5,047-cent balanced journal remain unchanged.

Windows Debug passes without warnings or errors. This is a display change; no new storage test duplicates the existing customer validation rules. The parent full suite passes 1,639 cases at `05f8d3b`. Full release/device/account acceptance remains pending.
