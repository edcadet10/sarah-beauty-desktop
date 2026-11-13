# Read balances before an invoice void

An invoice's retained void decision has an effective date. Dated customer balances, charged tax, booking billing, outstanding amounts, readable customer rows, and newly issued receipt balances include the invoice before that date and exclude it on or after that date. The issued snapshot stays unchanged.

A historical read does not authorize a new payment application or credit note against a currently voided invoice. Those write paths continue to require a current issued invoice. A voided invoice without its verified retained decision fails with a review message instead of inventing an effective date.

Three checks failed before this correction: a later void erased the earlier customer balance, charged tax, and outstanding invoice amount. After correction, all 20 initial related cases pass. Eighteen consumer checks also pass, including seven invoice-date cases covering the boundary, booking/export agreement, historical receipt balance, a blocked backdated application, and missing-decision rejection. These tests seed the retained decision to exercise reads; they do not establish a public invoice-void command or desktop workflow.
