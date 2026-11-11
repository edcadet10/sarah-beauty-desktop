using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Documents;

internal static class ClientPdf
{
    private static readonly Lazy<bool> Fonts=new(()=>{GlobalFontSettings.FontResolver??=new WindowsFontResolver();return true;});

    internal static byte[] Render(ClientSnapshot snapshot,string snapshotHash)
    {
        _=Fonts.Value;
        var document=new Document();
        document.Styles[StyleNames.Normal]!.Font.Name="Arial";
        document.Styles[StyleNames.Normal]!.Font.Size=10;
        document.Styles[StyleNames.Normal]!.ParagraphFormat.SpaceAfter=Unit.FromPoint(7);
        document.Info.Title=$"{snapshot.Kind} {snapshot.Number} v{snapshot.Version}";
        document.Info.Author=snapshot.BusinessName;
        document.Info.Subject="SarahBeauty snapshot "+snapshotHash;
        var section=Page(document,snapshot);
        Header(section,snapshot);
        Lines(section,snapshot);
        Totals(section,snapshot);
        var renderer=new PdfDocumentRenderer{Document=document};
        renderer.RenderDocument();
        using var stream=new MemoryStream();
        renderer.Save(stream,false);
        return stream.ToArray();
    }

    private static Section Page(Document document,ClientSnapshot snapshot)
    {
        var section=document.AddSection();
        section.PageSetup.PageFormat=PageFormat.Letter;
        section.PageSetup.TopMargin=section.PageSetup.BottomMargin=Unit.FromCentimeter(1.8);
        section.PageSetup.LeftMargin=section.PageSetup.RightMargin=Unit.FromCentimeter(1.5);
        var header=section.Headers.Primary.AddParagraph($"{snapshot.Mode} • {snapshot.BusinessName}");
        header.Format.Font.Size=8;
        header.Format.Font.Color=Colors.DarkGray;
        var footer=section.Footers.Primary.AddParagraph($"{snapshot.Mode} • {snapshot.Number} v{snapshot.Version} • Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();
        footer.Format.Font.Size=8;
        footer.Format.Alignment=ParagraphAlignment.Right;
        return section;
    }

    private static void Header(Section section,ClientSnapshot s)
    {
        var brand=section.AddParagraph(s.BusinessName);
        brand.Format.Font.Size=24;
        brand.Format.Font.Bold=true;
        brand.Format.Font.Color=Color.FromRgb(97,55,76);
        string kind=s.Kind==DocumentKind.CreditNote?"Credit note":s.Kind.ToString();
        section.AddParagraph($"{kind} {s.Number} — version {s.Version}");
        if(s.RelatedDocument.Length>0)section.AddParagraph("Related document: "+s.RelatedDocument);
        section.AddParagraph($"Issued {s.IssueDate:yyyy-MM-dd}"+(s.DueDate.HasValue?$" • {(s.Kind==DocumentKind.Quote?"Valid until":"Due")} {s.DueDate:yyyy-MM-dd}":""));
        section.AddParagraph("Prepared for "+s.Client.Name).Format.Font.Bold=true;
        string billing=s.Client.BillingName==s.Client.Name?"":s.Client.BillingName;
        foreach(string value in new[]{billing,s.Client.BillingAddress,s.Client.Email,s.Client.Phone}.Where(v=>!string.IsNullOrWhiteSpace(v)))
            section.AddParagraph(value);
        section.AddParagraph($"{s.Event.Name} • {s.Event.Date?.ToString("yyyy-MM-dd")??"Event date undecided"}");
        if(s.Event.Venue.Length>0)section.AddParagraph(s.Event.Venue);
        if(s.Event.ReadyBy.Length>0)section.AddParagraph("Ready by "+s.Event.ReadyBy+" (Eastern Time)");
    }

    private static void Lines(Section section,ClientSnapshot snapshot)
    {
        var table=section.AddTable();
        foreach(double width in new[]{8.1,1.6,2.8,2.8,3.2})table.AddColumn(Unit.FromCentimeter(width));
        table.Borders.Width=Unit.FromPoint(0.4);
        table.Borders.Color=Colors.LightGray;
        var heading=table.AddRow();
        heading.HeadingFormat=true;
        heading.Format.Font.Bold=true;
        heading.Shading.Color=Color.FromRgb(244,235,240);
        string[] labels=["Service / charge","Qty","Unit price","Tax","Total"];
        for(int i=0;i<labels.Length;i++)heading.Cells[i].AddParagraph(labels[i]);
        foreach(var line in snapshot.Lines)Line(table,line);
        section.AddParagraph();
    }

    private static void Line(Table table,ClientLine line)
    {
        var row=table.AddRow();
        row.TopPadding=row.BottomPadding=Unit.FromPoint(5);
        row.Cells[0].AddParagraph(line.Description);
        row.Cells[0].AddParagraph(line.Unit).Format.Font.Size=8;
        if(line.DiscountCents!=0)row.Cells[0].AddParagraph("Discount "+Money.Display(line.DiscountCents));
        if(line.RoundingCents!=0)row.Cells[0].AddParagraph("Rounding adjustment "+Money.Display(line.RoundingCents));
        row.Cells[1].AddParagraph(line.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        row.Cells[2].AddParagraph(Money.Display(line.UnitCents));
        row.Cells[3].AddParagraph(Money.Display(line.TaxCents));
        row.Cells[4].AddParagraph(Money.Display(line.TotalCents));
        for(int i=1;i<5;i++)row.Cells[i].Format.Alignment=ParagraphAlignment.Right;
    }

    private static void Totals(Section section,ClientSnapshot snapshot)
    {
        section.AddParagraph((snapshot.Kind==DocumentKind.CreditNote?"Net reduction: ":"Net charges: ")+Money.Display(snapshot.NetCents)).Format.Alignment=ParagraphAlignment.Right;
        section.AddParagraph("Tax: "+Money.Display(snapshot.TaxCents)).Format.Alignment=ParagraphAlignment.Right;
        var total=section.AddParagraph((snapshot.Kind==DocumentKind.CreditNote?"Total reduction: ":"Total: ")+Money.Display(snapshot.TotalCents));
        total.Format.Alignment=ParagraphAlignment.Right;
        total.Format.Font.Bold=true;
        foreach(var milestone in snapshot.Milestones)
            section.AddParagraph(snapshot.Kind==DocumentKind.CreditNote?$"{milestone.Kind} reduction: {Money.Display(milestone.Cents)}":
                $"{milestone.Kind}: {Money.Display(milestone.Cents)} • Due {milestone.DueDate?.ToString("yyyy-MM-dd")??"Undecided"}");
        if(snapshot.Terms.Length>0)
        {
            section.AddParagraph("Terms").Format.Font.Bold=true;
            section.AddParagraph(snapshot.Terms).Format.KeepTogether=false;
        }
    }
}
