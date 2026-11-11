using MigraDoc.DocumentObjectModel;
using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Documents;

internal static class ReceiptPdf
{
    internal static void Detail(Section section,ClientReceipt receipt)
    {
        section.AddParagraph($"Payment received {receipt.PaymentDate:yyyy-MM-dd} • Method: {receipt.Method}");
        if(receipt.Reference.Length>0)section.AddParagraph("Payment reference: "+receipt.Reference);
        section.AddParagraph("Applications as of issue date").Format.Font.Bold=true;
        if(receipt.Applications.Length==0)section.AddParagraph("No invoice applications recorded for this payment.");
        foreach(var application in receipt.Applications)
            section.AddParagraph($"{application.InvoiceNumber} • {application.Milestone}: {Money.Display(application.Cents)}");
        section.AddParagraph("Refunded as of issue date: "+Money.Display(receipt.RefundCents));
        section.AddParagraph("Unallocated payment: "+Money.Display(receipt.PaymentAvailableCents));
        section.AddParagraph("Unused funded credit from this payment: "+Money.Display(receipt.UnusedFundedCreditCents));
        section.AddParagraph("Payer's total open invoice balance: "+Money.Display(receipt.CustomerInvoiceBalanceCents));
    }
}
