using System.Text.Json;
using SarahBeauty.Business.Documents;

namespace SarahBeauty.Storage.Tests;

public sealed class DocumentDecisionContractTests
{
    [Theory]
    [InlineData(DocumentDecisionAction.DeclineQuote,"Declined")][InlineData(DocumentDecisionAction.WithdrawQuote,"Withdrawn")]
    [InlineData(DocumentDecisionAction.VoidQuote,"Void")][InlineData(DocumentDecisionAction.VoidInvoice,"Void")]
    public void SupportedDecisionsKeepTheirCanonicalPayloadAndExactResult(DocumentDecisionAction action,string result)
    {
        var draft=DocumentDecisionRules.Normalize(Draft() with{Action=action,Reason=" Fictional reviewed decision ",DocumentSnapshotHash=new string('b',64)});
        Assert.Equal("Fictional reviewed decision",draft.Reason);Assert.Equal(new string('B',64),draft.DocumentSnapshotHash);Assert.Equal(result,DocumentDecisionRules.Result(action));
        var value=Value(draft);string json=JsonSerializer.Serialize(value);Assert.Equal(value,DocumentDecisionCodec.Read(json,DocumentDecisionCodec.Hash(json)));Assert.Equal(DocumentDecisionCodec.Payload(draft),DocumentDecisionCodec.Payload(DocumentDecisionCodec.Draft(value)));
    }
    [Fact]
    public void MissingOrUnsupportedDraftFactsCannotBecomeAConfirmationPayload()
    {
        var draft=Draft();foreach(var invalid in new[]{draft with{Action=(DocumentDecisionAction)999},draft with{VersionId=Guid.Empty.ToString()},draft with{DocumentSnapshotHash="wrong"},draft with{ExpectedStatus="Accepted"},draft with{DecisionDate=default},draft with{Reason=" "}})
            Assert.Throws<ArgumentException>(()=>DocumentDecisionRules.Normalize(invalid));
    }
    [Fact]
    public void ChangedHashRehashedInvalidStatesAndUnknownOrDuplicateFieldsAreRejected()
    {
        var value=Value(Draft());string json=JsonSerializer.Serialize(value);Assert.Throws<InvalidDataException>(()=>DocumentDecisionCodec.Read(json,new string('0',64)));
        foreach(var invalid in new[]{value with{Version=2},value with{ResultStatus="Accepted"},value with{Reason=" "},value with{OwnerId="unknown"},value with{RecordedAtUtc="2027-01-01T12:00:00.0000000+01:00"}})
        {string altered=JsonSerializer.Serialize(invalid);Assert.Throws<InvalidDataException>(()=>DocumentDecisionCodec.Read(altered,DocumentDecisionCodec.Hash(altered)));}
        foreach(string altered in new[]{json[..^1]+",\"Unexpected\":true}",json[..^1]+",\"Id\":\""+value.Id+"\"}"})Assert.Throws<InvalidDataException>(()=>DocumentDecisionCodec.Read(altered,DocumentDecisionCodec.Hash(altered)));
    }
    private static DocumentDecisionDraft Draft()=>new(Guid.NewGuid().ToString(),new string('B',64),"Issued",DocumentDecisionAction.DeclineQuote,new(2027,1,1),"Fictional reviewed decision");
    private static DocumentDecision Value(DocumentDecisionDraft draft)=>new(1,Guid.NewGuid().ToString(),Guid.NewGuid().ToString(),Guid.NewGuid().ToString(),draft.VersionId,Guid.NewGuid().ToString(),draft.Action,draft.DecisionDate,draft.ExpectedStatus,DocumentDecisionRules.Result(draft.Action),draft.DocumentSnapshotHash,draft.Reason,draft.Evidence,Guid.NewGuid().ToString(),DateTimeOffset.UtcNow.ToString("O"));
}
