using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class DashboardCardsView
{
    internal static UIElement Build(DashboardSnapshot snapshot)
    {
        var r=snapshot.Finance.Reports;var asOf=$"As of {r.Through:yyyy-MM-dd}";var period=$"{r.From:yyyy-MM-dd} – {r.Through:yyyy-MM-dd}";
        var cards=new List<(string Title,string Value,string Detail)>
        {
            ("Income",Money.Display(r.ProfitLoss.IncomeCents),period+" • Approved cash-method income"),
            ("Expenses",Money.Display(r.ProfitLoss.ExpenseCents),period+" • Approved classifications"),
            ("Result",Money.Display(r.ProfitLoss.ResultCents),period+" • Income less expenses"),
            ("Customer amount due",Money.Display(r.Customers.Sum(c=>c.OpenCents)),asOf+" • Includes tax; credits remain separate"),
            ("Customer credits",Money.Display(r.Customers.Sum(c=>c.UnusedCreditCents)),asOf+" • Available funded value"),
            ("Supplier amount due",Money.Display(r.Suppliers.Sum(s=>s.OpenCents)),asOf+" • Operational unpaid bills"),
            ("Tax liability",Money.Display(r.Tax.LiabilityCents),asOf+" • Posted balance; filing status is separate"),
            ("Upcoming confirmed weddings",snapshot.Upcoming.Bookings.Length.ToString(),$"{snapshot.Upcoming.From:yyyy-MM-dd} – {snapshot.Upcoming.Through:yyyy-MM-dd} • {snapshot.Upcoming.Undated.Length} additional undated"),
            ("Known upcoming agreement value",Money.Display(snapshot.Upcoming.Months.Sum(m=>m.KnownAgreementGrossCents)),
                $"{snapshot.Upcoming.From:yyyy-MM-dd} – {snapshot.Upcoming.Through:yyyy-MM-dd} • Includes tax; {snapshot.Upcoming.Months.Sum(m=>m.UnknownValueCount)} values need review"),
            ("Pending work",snapshot.Operations.Work.Length.ToString(),$"Planning {snapshot.Operations.BusinessDate:yyyy-MM-dd} • Current records at capture"),
            ("Supply alerts",snapshot.Operations.Supplies.Count(s=>s.Low||s.Finished||s.ExpiryAlert).ToString(),$"Planning {snapshot.Operations.BusinessDate:yyyy-MM-dd} • Low, Finished, or expiry on/before date")
        };
        if(r.Debts is not null)cards.Add(("Known loan principal",Money.Display(r.Debts.Positions.Sum(p=>p.ClosingPrincipalCents??0)),asOf+$" • {r.Debts.Positions.Count(p=>p.ClosingPrincipalCents is null)} loans have no approved principal basis"));
        foreach(var cash in snapshot.Finance.CashAccounts)cards.Add((cash.Account.Name,Money.Display(cash.BalanceCents),asOf+" • "+(cash.Account.OpeningConfirmed?"Opening confirmed":"Opening unconfirmed")
            +" • "+(cash.ReconciledThrough.HasValue?$"Reconciled through {cash.ReconciledThrough:yyyy-MM-dd}":"Reconciliation not completed")));
        var panel=Form.Panel();
        panel.Add(BuildGrid(cards.Take(3)),new Expander{Header="Balances and planning",Content=BuildGrid(cards.Skip(3)),HorizontalAlignment=HorizontalAlignment.Stretch});
        return panel;
    }

    private static UIElement BuildGrid(IEnumerable<(string Title,string Value,string Detail)> cards)=>ResponsiveCards.Build(cards.Select(Card));

    private static UIElement Card((string Title,string Value,string Detail) card)
    {
        var panel=Form.Panel();panel.Add(Form.Muted(card.Title),Form.Title(card.Value),Form.Muted(card.Detail));
        return Form.Card(panel);
    }
}
