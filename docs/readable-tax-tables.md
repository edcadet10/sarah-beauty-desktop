# Project tax filing decisions

Two source-bound tables retain tax-period dates, jurisdiction, status, archive/revision/provenance, and ordered filing decisions with request/reviewed revision, actual filing date, signed reported tax, reference, owner, and time. Unknown due dates and filing details stay blank. An actual zero return stays `0`; an amendment with a refund amount retains its negative sign.

Filing status is a reviewed record. A tax remittance does not file a return, and a filing decision does not post cash, income, or an expense. Tax liability, refund treatment, and actual recovery belong to the financial records and reports. Free-form filing reasons and evidence inputs stay in the desktop and encrypted archive; reference numbers remain readable.

Two fictional checks pass for zero filing and a signed amendment alongside an unreviewed period, and for remittance without filing alongside a reviewed `Not required` decision with unknown filing details. Tables share the captured generation/watermark and budget. This remains a partial projection; no workbook is published.
