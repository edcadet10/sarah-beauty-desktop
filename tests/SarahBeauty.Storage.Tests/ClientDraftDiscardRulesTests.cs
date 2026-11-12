using SarahBeauty.Business.Documents;

namespace SarahBeauty.Storage.Tests;

public sealed class ClientDraftDiscardRulesTests
{
    [Theory]
    [InlineData(DocumentKind.Quote)][InlineData(DocumentKind.Invoice)][InlineData(DocumentKind.Receipt)][InlineData(DocumentKind.CreditNote)]
    public void AReviewBindsEveryClientDraftKindToItsSavedRevisionAndSnapshot(DocumentKind kind)
    {
        var draft=Valid() with{Kind=kind,Reason="  Fictional private draft review\nA second line.  "};var value=ClientDraftDiscardRules.Normalize(draft);Assert.Equal(draft with{Reason="Fictional private draft review\nA second line."},value);
    }
    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(int.MaxValue)]
    public void UnsavedOrUnwritableRevisionsAreRejected(int revision)=>Assert.Throws<ArgumentException>(()=>ClientDraftDiscardRules.Normalize(Valid() with{ExpectedRevision=revision}));
    [Theory]
    [InlineData("")][InlineData(" ")]
    public void ASourceIdentityIsRequired(string id)=>Assert.Throws<ArgumentException>(()=>ClientDraftDiscardRules.Normalize(Valid() with{DraftId=id}));
    [Fact]
    public void SavedOpaqueIdentitiesArePreservedExactly()
    {
        var draft=Valid() with{DraftId=" legacy opaque draft id "};Assert.Equal(draft.DraftId,ClientDraftDiscardRules.Normalize(draft).DraftId);
    }
    [Theory]
    [InlineData("")][InlineData("short")][InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void SnapshotDigestsMustBeCompleteCanonicalSha256(string hash)=>Assert.Throws<ArgumentException>(()=>ClientDraftDiscardRules.Normalize(Valid() with{DraftSnapshotHash=hash}));
    [Theory]
    [InlineData("")][InlineData(" \t ")][InlineData("Fictional\0reason")][InlineData("Fictional\u0001reason")]
    public void MissingOrControlCharacterReasonsAreRejected(string reason)=>Assert.Throws<ArgumentException>(()=>ClientDraftDiscardRules.Normalize(Valid() with{Reason=reason}));
    [Fact]
    public void InvalidUnicodeAndOversizedReasonsAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>ClientDraftDiscardRules.Normalize(Valid() with{Reason="Fictional "+new string('\ud800',1)}));Assert.Throws<ArgumentException>(()=>ClientDraftDiscardRules.Normalize(Valid() with{Reason=new string('x',1001)}));Assert.Equal(1000,ClientDraftDiscardRules.Normalize(Valid() with{Reason=new string('x',1000)}).Reason.Length);
    }
    [Fact]
    public void UnknownKindsAreRejected()=>Assert.Throws<ArgumentException>(()=>ClientDraftDiscardRules.Normalize(Valid() with{Kind=(DocumentKind)99}));
    private static ClientDraftDiscardRequest Valid()=>new(DocumentKind.Quote,Guid.NewGuid().ToString(),1,new string('A',64),"Fictional draft no longer needed");
}
