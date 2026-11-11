namespace SarahBeauty.Business.Bookings;

internal static class CancellationSchema
{
    internal const string Sql="""
        CREATE TABLE CancellationDecisions (
            Id TEXT NOT NULL PRIMARY KEY, BusinessId TEXT NOT NULL REFERENCES Business(Id), BookingId TEXT NOT NULL REFERENCES Records(Id),
            DraftId TEXT NOT NULL UNIQUE REFERENCES Records(Id), RequestedDate TEXT NOT NULL, DecisionDate TEXT NOT NULL,
            PayloadHash TEXT NOT NULL, SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)), SnapshotHash TEXT NOT NULL,
            ApprovedBy TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE INDEX CancellationDecisions_Booking ON CancellationDecisions(BusinessId,BookingId,DecisionDate);
        CREATE TRIGGER CancellationDecisions_NoUpdate BEFORE UPDATE ON CancellationDecisions BEGIN SELECT RAISE(ABORT,'Cancellation decisions are immutable'); END;
        CREATE TRIGGER CancellationDecisions_NoDelete BEFORE DELETE ON CancellationDecisions BEGIN SELECT RAISE(ABORT,'Cancellation decisions are immutable'); END;
        """;
}
