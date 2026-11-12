using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Ownership;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerInterestHistoryTests
{
    [Fact]
    public void UnknownDatesCorrectionsAndInactiveOwnersKeepTheirOriginalHistoryAfterReopen()
    {
        using var f=new BusinessFixture();var interests=new OwnerInterests(f.Store);Assert.Empty(interests.History(f.Owner));Assert.Null(interests.At(f.Owner,new(2027,1,1)));
        var first=Insert(f,1,new(2027,1,1),.6m);var later=Insert(f,2,new(2027,4,1),.5m);var corrected=Insert(f,3,new(2027,1,1),.7m,first.Id);
        string other=interests.Owners(f.Owner).Single(o=>o.Id!=f.Owner.OwnerId).Id;
        f.Store.Transaction(db=>db.Execute("UPDATE Owners SET Active=0,Name='Fictional renamed owner' WHERE Id=$owner;",("$owner",other)));
        var reopened=new BusinessStore(f.Store.DatabasePath);var owner=new OwnerAccess(reopened).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);interests=new(reopened);
        Assert.Equal(3,interests.History(owner).Count);Assert.Null(interests.At(owner,new(2026,12,31)));var periods=interests.Periods(owner);Assert.Equal(2,periods.Count);
        Assert.Equal(corrected.Id,periods[0].Confirmation.Id);Assert.Equal(new DateOnly(2027,3,31),periods[0].EffectiveThrough);Assert.Equal(later.Id,periods[1].Confirmation.Id);Assert.Null(periods[1].EffectiveThrough);
        Assert.Equal(corrected.Id,interests.At(owner,new(2027,3,31))!.Confirmation.Id);Assert.Equal(later.Id,interests.At(owner,new(2027,4,1))!.Confirmation.Id);
        Assert.Contains(interests.Owners(owner),o=>o.Id==other&&!o.LoginActive);Assert.Equal("Morgan",interests.History(owner)[0].Shares.Single(s=>s.OwnerId==other).OwnerName);
        Assert.Throws<NotSupportedException>(()=>((IList<ConfirmedOwnerShare>)periods[0].Confirmation.Shares).Clear());
        Assert.Equal(1L,TestStore.Scalar(reopened.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(0L,TestStore.Scalar(reopened.DatabasePath,"SELECT count(*) FROM JournalLines;"));
    }
    [Theory]
    [InlineData("DROP TRIGGER OwnerInterestVersions_NoUpdate; UPDATE OwnerInterestVersions SET SnapshotJson=replace(SnapshotJson,'Fictional confirmation','Changed confirmation');")]
    [InlineData("DROP TRIGGER OwnerInterestVersions_NoUpdate; UPDATE OwnerInterestVersions SET EffectiveFrom='2027-02-01';")]
    [InlineData("DROP TRIGGER OwnerInterestShares_NoUpdate; UPDATE OwnerInterestShares SET FractionText='0.1';")]
    [InlineData("DROP TRIGGER OwnerInterestShares_NoDelete; DELETE FROM OwnerInterestShares;")]
    [InlineData("PRAGMA foreign_keys=OFF; DROP TRIGGER OwnerInterestVersions_NoDelete; DELETE FROM OwnerInterestVersions;")]
    public void ChangedSnapshotIdentityOrShareCacheIsRejected(string corruption)
    {
        using var f=new BusinessFixture();Insert(f,1,new(2027,1,1),.6m);f.Files.Execute(corruption);
        Assert.Throws<InvalidDataException>(()=>new OwnerInterests(f.Store).History(f.Owner));
    }
    [Fact]
    public void LockedAndForeignSessionsCannotReadThePrivateSettings()
    {
        using var f=new BusinessFixture();using var other=new BusinessFixture();var interests=new OwnerInterests(f.Store);
        Assert.Throws<UnauthorizedAccessException>(()=>interests.History(other.Owner));f.Owner.Lock();Assert.Throws<UnauthorizedAccessException>(()=>interests.Owners(f.Owner));Assert.Throws<UnauthorizedAccessException>(()=>interests.Periods(f.Owner));
    }
    internal static OwnerInterestVersion Insert(BusinessFixture f,long revision,DateOnly effective,decimal firstFraction,string? supersedes=null)
    {
        var owners=new OwnerInterests(f.Store).Owners(f.Owner);var shares=owners.Select(o=>new ConfirmedOwnerShare(o.Id,o.Name,o.Id==f.Owner.OwnerId?firstFraction:1-firstFraction)).OrderBy(s=>s.OwnerId,StringComparer.Ordinal).ToArray();
        var value=new OwnerInterestVersion(1,Guid.NewGuid().ToString(),f.Owner.BusinessId,revision,effective,supersedes,shares,"Fictional confirmation",null,null,f.Owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
        string json=JsonSerializer.Serialize(value);f.Store.Transaction(db=>
        {
            db.Execute("INSERT INTO OwnerInterestVersions(Id,BusinessId,Revision,EffectiveFrom,SupersedesId,RecordedByOwnerId,RecordedAtUtc,SnapshotJson,SnapshotHash) VALUES($id,$business,$revision,$date,$supersedes,$owner,$at,$json,$hash);",("$id",value.Id),("$business",value.BusinessId),("$revision",revision),("$date",effective.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)),("$supersedes",supersedes),("$owner",value.RecordedByOwnerId),("$at",value.RecordedAtUtc),("$json",json),("$hash",OwnerInterestCodec.Hash(json)));
            foreach(var share in shares)db.Execute("INSERT INTO OwnerInterestShares(VersionId,BusinessId,OwnerId,OwnerNameAtConfirmation,FractionText) VALUES($version,$business,$owner,$name,$fraction);",("$version",value.Id),("$business",value.BusinessId),("$owner",share.OwnerId),("$name",share.OwnerName),("$fraction",share.Fraction.ToString("0.######",CultureInfo.InvariantCulture)));
            return true;
        });return value;
    }
}
