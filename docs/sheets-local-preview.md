# Preview a readable export locally

Owners can build a verified local preview before connecting Google. It reports captured mode/schema/watermark/time, the selected report dates, every source table's dimensions, source and planned workbook cells, raw text bytes, and exact journal totals. Notices come from that same generation's ownership, record-scope, and privacy metadata.

The preview writes no staged export, job, permission, or queue record and makes no Google request. Its inventory contains counts rather than business rows or private database paths. A later export captures a new generation; this preview does not freeze subsequent edits or establish remote publication.

Five new preview cases and four assembly cases pass in Release. They check actual posted receipt journal totals and inventory sums, no watermark/job/staging changes, notices, foreign/locked access, and cancellation, owner lock, or instance replacement immediately before return. A final session and cancellation check prevents displaying the completed result after those changes. Desktop preview controls remain subsequent work.
