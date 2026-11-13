using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Ownership;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableOwnershipTablesTests
{
    [Fact]
    public void CapturedHistoryAndAsOfCorrectionsStayLiteralPrivateAndIndependentOfLaterChanges()
    {
        using var f=new BusinessFixture();var evidence=new ManagedAttachments(f.Store).Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());var interests=new OwnerInterests(f.Store);
        var first=interests.Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,0,new(2027,1,1),.6m) with{Reason="PRIVATE fictional ownership review",EvidenceVersionId=evidence.Version.Id});
        var later=interests.Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,1,new(2027,4,1),.5m));var correction=interests.Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,2,new(2027,1,1),.7m) with{SupersedesId=first.Id});
        using var snapshot=new ReadableExportSnapshots(f.Store).Capture(f.Owner);var before=ReadableOwnershipTables.Build(snapshot,new(),new(2027,3,31));
        interests.Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,3,new(2027,1,1),.8m) with{SupersedesId=correction.Id});f.Owner.Lock();var after=ReadableOwnershipTables.Build(snapshot,new(),new(2027,3,31));
        Assert.Equal(before.Select(t=>t.Sha256),after.Select(t=>t.Sha256));Assert.Equal(3,before[0].Rows.Count);Assert.Equal(6,before[1].Rows.Count);Assert.Equal(2,before[2].Rows.Count);
        Assert.Equal(new[]{"2027-01-01","2027-03-31",correction.Id,"3"},before[2].Rows[0].Skip(1));Assert.Equal(later.Id,before[2].Rows[1][3]);Assert.Equal("",before[2].Rows[1][2]);
        Assert.Equal("Confirmed",Assert.Single(before[3].Rows)[2]);Assert.Equal(correction.Id,before[3].Rows[0][3]);Assert.Equal("1",before[3].Rows[0][6]);
        string serialized=JsonSerializer.Serialize(before);Assert.DoesNotContain("PRIVATE fictional ownership review",serialized);Assert.DoesNotContain(evidence.Version.Id,serialized);Assert.DoesNotContain(evidence.SnapshotHash,serialized);Assert.DoesNotContain(f.Files.Folder,serialized);
        Assert.DoesNotContain(before.SelectMany(t=>t.Columns),c=>c.Contains("Reason",StringComparison.Ordinal)||c.Contains("Evidence",StringComparison.Ordinal)||c.Contains("Snapshot",StringComparison.Ordinal));
        Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Movements;"));
    }
    [Fact]
    public void UnconfirmedDatesKeepBlankCoverageAndCalendarDatesStayGregorian()
    {
        using var f=new BusinessFixture();var culture=CultureInfo.CurrentCulture;try
        {
            CultureInfo.CurrentCulture=new("th-TH");using(var empty=new ReadableExportSnapshots(f.Store).Capture(f.Owner))
            {
                var tables=ReadableOwnershipTables.Build(empty,new(),new(2027,1,1));Assert.All(tables.Take(3),t=>Assert.Empty(t.Rows));Assert.Equal("2027-01-01",tables[3].Rows[0][1]);Assert.Equal("Unknown / unconfirmed",tables[3].Rows[0][2]);Assert.All(tables[3].Rows[0].Skip(3).Take(4),cell=>Assert.Equal("",cell));
            }
            new OwnerInterests(f.Store).Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,0,new(2027,2,1),0m));using var snapshot=new ReadableExportSnapshots(f.Store).Capture(f.Owner);
            var earlier=ReadableOwnershipTables.Build(snapshot,new(),new(2027,1,31));Assert.Equal("Unknown / unconfirmed",earlier[3].Rows[0][2]);Assert.Equal("2027-02-01",earlier[0].Rows[0][3]);
            var confirmed=ReadableOwnershipTables.Build(snapshot,new(),new(2027,2,1));Assert.Equal("Confirmed",confirmed[3].Rows[0][2]);Assert.Contains(confirmed[1].Rows,row=>row[2]==f.Owner.OwnerId&&row[4]=="0");
        }
        finally{CultureInfo.CurrentCulture=culture;}
    }
}
