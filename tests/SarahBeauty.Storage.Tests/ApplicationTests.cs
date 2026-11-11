using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ApplicationTests
{
    private static DocumentVersion Invoice(LedgerFixture f,long net=12000,decimal taxRate=0m,string? payer=null)
    {
        payer??=f.CustomerId;
        var booking=BookingRules.AddCharge(new Booking{Name="Fictional invoice booking",HasTrial=false,TrialCents=0,
            CustomDepositCents=0,PrimaryCustomerId=payer,PayerMode="Single",SinglePayerCustomerId=payer,
            UnpaidResponsibility="Fictional payer covers the agreed balance"},"Fictional service","Custom",1,net);
        booking=new BookingBook(f.Business.Store).Save(f.Business.Owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=taxRate}).ToArray()});
        var quotes=new QuoteBook(f.Business.Store);
        var draft=quotes.SaveDraft(f.Business.Owner,quotes.FromBooking(f.Business.Owner,booking) with{IssueDate=f.Date});
        var quote=quotes.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        quotes.Accept(f.Business.Owner,quote.Id,f.Date,"Fictional acceptance for an allocation fixture");
        var invoices=new InvoiceBook(f.Business.Store);
        var invoice=invoices.SaveDraft(f.Business.Owner,new InvoiceDraft{Name="Fictional invoice",QuoteVersionId=quote.Id,
            PayerCustomerId=payer,IssueDate=f.Date,Portions=quote.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()});
        return invoices.Issue(f.Business.Owner,invoice.Id,invoice.Revision,BusinessFixture.Phrase);
    }

    private static List<PaymentApplication> Apply(LedgerFixture f,PaymentApplicationDraft value)
    {
        var book=new PaymentApplications(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,value);
        return book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    private static PaymentApplicationDraft Draft(LedgerFixture f,string payment,DocumentVersion invoice,long cents)=>new()
    {Name="Fictional payment allocation",PaymentId=payment,InvoiceVersionId=invoice.Id,Cents=cents,Date=f.Date};

    [Fact]
    public void OneReceiptCanPaySeveralInvoicesAndManyReceiptsCanPayOneInvoice()
    {
        using var f=new LedgerFixture();
        var first=Invoice(f,12000);
        var second=Invoice(f,8000);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,15000));
        var book=new PaymentApplications(f.Business.Store);
        Apply(f,Draft(f,receipt.Id,first,12000));
        Assert.Equal(3000,book.Available(f.Business.Owner,receipt.Id,f.Date).Cents);
        Apply(f,Draft(f,receipt.Id,second,3000));
        var other=f.Post(f.Draft(FinancialKind.Receipt,5000));
        Apply(f,Draft(f,other.Id,second,5000));
        Assert.Equal(0,book.Outstanding(f.Business.Owner,f.Date).Sum(d=>d.Cents));
        Assert.Equal(0,book.Available(f.Business.Owner,receipt.Id,f.Date).Cents);
        Assert.Equal(-20000,f.Balance("4000"));
    }

    [Fact]
    public void OverApplicationFailsAndDatedReversalReopensTheInvoiceBeforeRefund()
    {
        using var f=new LedgerFixture();
        var invoice=Invoice(f);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000));
        Assert.Throws<ArgumentException>(()=>Apply(f,Draft(f,receipt.Id,invoice,12001)));
        var application=Assert.Single(Apply(f,Draft(f,receipt.Id,invoice,12000)));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Refund,4000) with{OriginalId=receipt.Id}));
        Apply(f,new PaymentApplicationDraft{Name="Fictional partial unapplication",ReversesId=application.Id,Cents=4000,Date=f.Date.AddDays(1)});
        var book=new PaymentApplications(f.Business.Store);
        Assert.Equal(0,book.Outstanding(f.Business.Owner,f.Date).Sum(d=>d.Cents));
        Assert.Equal(4000,book.Outstanding(f.Business.Owner,f.Date.AddDays(1)).Sum(d=>d.Cents));
        f.Post(f.Draft(FinancialKind.Refund,4000) with{OriginalId=receipt.Id,Date=f.Date.AddDays(1)});
        Assert.Equal(0,book.Available(f.Business.Owner,receipt.Id,f.Date.AddDays(1)).Cents);
        Assert.Equal(8000,f.Balance("1010"));
    }

    [Theory]
    [InlineData("application-group")]
    [InlineData("application")]
    [InlineData("application-audit")]
    public void FailedApplicationRollsBackAndRetryKeepsOneGroup(string stage)
    {
        using var f=new LedgerFixture();
        var invoice=Invoice(f);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000));
        var book=new PaymentApplications(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,receipt.Id,invoice,12000));
        book.FailurePoint=point=>{if(point==stage)throw new IOException("Injected allocation failure");};
        Assert.Throws<IOException>(()=>book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Empty(book.History(f.Business.Owner));
        Assert.Equal(12000,book.Available(f.Business.Owner,receipt.Id,f.Date).Cents);
        book.FailurePoint=null;
        var saved=book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(saved[0].GroupId,book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase)[0].GroupId);
        Assert.Single(book.History(f.Business.Owner));
    }

    [Fact]
    public async Task CompetingApplicationsCannotConsumeTheSameReceiptTwice()
    {
        using var f=new LedgerFixture();
        var first=Invoice(f,5000);
        var second=Invoice(f,5000);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,5000));
        var book=new PaymentApplications(f.Business.Store);
        var drafts=new[]{book.SaveDraft(f.Business.Owner,Draft(f,receipt.Id,first,5000)),book.SaveDraft(f.Business.Owner,Draft(f,receipt.Id,second,5000))};
        var results=await Task.WhenAll(drafts.Select(d=>Task.Run(()=>
        {
            try{book.Approve(f.Business.Owner,d.Id,d.Revision,BusinessFixture.Phrase);return true;}
            catch(ArgumentException){return false;}
        })));
        Assert.Single(results,r=>r);
        Assert.Equal(0,book.Available(f.Business.Owner,receipt.Id,f.Date).Cents);
        Assert.Equal(5000,book.Outstanding(f.Business.Owner,f.Date).Sum(d=>d.Cents));
    }

    [Fact]
    public void TaxComponentsStayBoundedAndMismatchedReceiptClassificationIsRejected()
    {
        using var f=new LedgerFixture();
        var invoice=Invoice(f,10000,0.1m);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        Apply(f,Draft(f,receipt.Id,invoice,5500));
        Apply(f,Draft(f,receipt.Id,invoice,5500));
        var book=new PaymentApplications(f.Business.Store);
        Assert.Equal(1000,book.History(f.Business.Owner).Sum(a=>a.TaxCents));
        Assert.Equal(new ValueBalance(0,0),book.Available(f.Business.Owner,receipt.Id,f.Date));
        Assert.Equal(-1000,f.Balance("2000"));
        var otherInvoice=Invoice(f,10000,0.1m);
        var incorrectlyClassified=f.Post(f.Draft(FinancialKind.Receipt,11000));
        Assert.Throws<ArgumentException>(()=>Apply(f,Draft(f,incorrectlyClassified.Id,otherInvoice,11000)));
    }

    [Fact]
    public void AnotherPayersInvoiceRequiresAnExplicitOnBehalfDecision()
    {
        using var f=new LedgerFixture();
        var other=new SarahBeauty.Business.Customers.CustomerBook(f.Business.Store).Save(f.Business.Owner,
            new SarahBeauty.Business.Customers.Customer{FullName="Fictional other payer"});
        var invoice=Invoice(f,payer:other.Id);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000));
        Assert.Throws<ArgumentException>(()=>Apply(f,Draft(f,receipt.Id,invoice,12000)));
        Apply(f,Draft(f,receipt.Id,invoice,12000) with{OnBehalfReason="Fictional explicit agreement to cover the other payer"});
        Assert.Equal(0,new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,f.Date).Sum(d=>d.Cents));
    }
}
