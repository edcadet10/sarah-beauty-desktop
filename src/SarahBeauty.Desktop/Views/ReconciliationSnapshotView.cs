using Microsoft.UI.Xaml;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class ReconciliationSnapshotView
{
    internal static UIElement Build(ReconciliationPreview p,bool historical=false)
    {
        var panel=Form.Panel();var s=p.Sources;
        panel.Add(Form.Title(s.AccountName+" • "+s.Statement.Through.ToString("yyyy-MM-dd")),
            Form.Text($"{s.Mode} • {s.BusinessName} • Record version {s.Watermark} • Generated {s.GeneratedAtUtc}"),
            Form.Text("Review reason: "+p.Draft.Reason),
            ReportTable.Build(["Reconciliation measure","USD"],[["Statement closing",Money.Display(s.Statement.ClosingCents)],
                ["Add deposits in transit",Money.Display(p.OutstandingDepositsCents)],["Less outstanding payments",Money.Display(p.OutstandingPaymentsCents)],
                ["Adjusted statement",Money.Display(p.AdjustedStatementCents)],["Ledger before selected posted corrections",Money.Display(p.LedgerBeforeCorrectionsCents)],
                ["Selected posted corrections",Money.Display(p.PostedBookCorrectionsCents)],["Ledger closing (corrections included)",Money.Display(p.AdjustedLedgerCents)],["Difference",Money.Display(p.DifferenceCents)]]),
            Form.Text($"Opening: {(s.OpeningConfirmed?"Confirmed":"Unconfirmed")} • Unexplained bank lines: {p.UnexplainedBankCount} • Unsupported cash items: {p.UnexplainedCashIds.Length}"));
        foreach(string warning in s.ContinuityWarnings)panel.Add(Form.Text(warning));
        if(!historical)panel.Add(Form.Text(p.ReadyToComplete?"Ready for owner confirmation.":"Resolve the listed source issues before completing."));
        if(s.PriorReconciliationIds.Length>0)panel.Add(Form.Text("Prior retained reconciliations: "+string.Join(", ",s.PriorReconciliationIds.Select(id=>id[..8]))));
        panel.Add(Lines("Bank source rows",s.Bank),Lines("Cash source entries",s.Cash),
            PagedReport.Build("Outstanding evidence",["Cash entry","Amount","Explanation","Evidence"],p.Draft.Outstanding.Select(o=>new[]{o.JournalLineId[..8],Money.Display(o.Cents),o.Explanation,o.EvidenceId})),
            PagedReport.Build("Selected posted corrections",["Date","Cash amount","Description","Evidence"],s.CorrectionChoices.Where(c=>p.Draft.PostedCorrectionIds.Contains(c.MovementId))
                .Select(c=>new[]{c.Date.ToString("yyyy-MM-dd"),Money.Display(c.SignedCents),c.Description,c.EvidenceId})));
        return panel;
    }

    private static UIElement Lines(string title,ReconciliationLine[] rows)=>PagedReport.Build(title,["Date / kind","Amount","Remaining","Description / code"],
        rows.Select(r=>new[]{r.Date.ToString("yyyy-MM-dd")+" • "+r.Kind,Money.Display(r.SignedCents),Money.Display(r.RemainingCents),r.Description+" • "+r.Id[..8]}));
}
