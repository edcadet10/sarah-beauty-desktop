using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        return BuildGrid(cards);
    }

    private static UIElement BuildGrid(List<(string Title,string Value,string Detail)> cards)
    {
        var grid=new Grid{ColumnSpacing=12,RowSpacing=12};for(int i=0;i<3;i++)grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        for(int i=0;i<cards.Count;i++)
        {
            if(i%3==0)grid.RowDefinitions.Add(new(){Height=GridLength.Auto});var c=cards[i];var panel=Form.Panel();
            panel.Add(Form.Text(c.Title),Form.Title(c.Value),Form.Text(c.Detail));
            var card=new Border{Padding=new Thickness(16),CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1),Child=panel,
                BorderBrush=Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,Background=Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush};
            Grid.SetRow(card,i/3);Grid.SetColumn(card,i%3);grid.Children.Add(card);
        }
        return grid;
    }
}
