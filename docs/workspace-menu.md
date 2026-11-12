# Find a workspace section

Dashboard, Customers, Bookings, Calendar, and Money stay at the top of the menu. Other tools are grouped under Client documents, Accounts and reports, Resources and suppliers, and Business settings. Expand a group to choose its section, or type a section name in **Find a section** and choose a suggestion.

The menu uses Windows' adaptive navigation behavior. On a narrower window, open it with the menu button. Choosing a section closes the overlay so the working area stays visible. Existing cached pages and their unsaved fields retain the same behavior.

## Design check

Claim: grouping can reduce the initial menu's visual load while retaining access to every section. Reject the implementation if a daily route requires opening a group, a section disappears, a dashboard shortcut selects the wrong page, or the narrow menu obstructs the chosen page. Grouping adds one expansion for occasional tools; this is a tradeoff, not measured evidence that every task is faster.

On November 12, 2026, native UI Automation observed nine visible entries at 1280 × 900: five daily sections and four collapsed groups, compared with the previous 30 flat entries. Group expansion opened Quotes; a dashboard shortcut selected the nested Reports route and expanded its parent. Native search opened all 30 sections and selected the matching menu items. Assistive text entry initially did not update suggestions; the search now handles programmatic text changes as well as ordinary typing.

At 900 × 900, the menu collapsed, opened, selected Customers, and closed again. The customer search field remained inside the window. Original fictional financial history, cancellation records, balances, and issued PDF hashes were unchanged. The Debug build has zero warnings/errors.

These checks establish route reachability and the tested layouts. Full keyboard traversal, larger Windows text, physical touch, theme checks, and owner task testing remain pending. Use [Microsoft's NavigationView guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/navigationview) as the native control reference.
