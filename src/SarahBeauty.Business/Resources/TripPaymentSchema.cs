namespace SarahBeauty.Business.Resources;

internal static class TripPaymentSchema
{
    internal const string Sql="""
        CREATE TABLE TripPaymentLinks (
            Id TEXT NOT NULL PRIMARY KEY, BusinessId TEXT NOT NULL REFERENCES Business(Id), TripId TEXT NOT NULL REFERENCES Records(Id), PaymentId TEXT NOT NULL REFERENCES Movements(Id),
            Date TEXT NOT NULL, Cents INTEGER NOT NULL CHECK(Cents>0), RequestId TEXT NOT NULL UNIQUE, PayloadHash TEXT NOT NULL,
            SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)), SnapshotHash TEXT NOT NULL, ApprovedBy TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE INDEX TripPaymentLinks_Trip ON TripPaymentLinks(BusinessId,TripId,Date);
        CREATE INDEX TripPaymentLinks_Payment ON TripPaymentLinks(BusinessId,PaymentId,Date);
        CREATE TABLE TripPaymentReleases (
            Id TEXT NOT NULL PRIMARY KEY, BusinessId TEXT NOT NULL REFERENCES Business(Id), LinkId TEXT NOT NULL REFERENCES TripPaymentLinks(Id),
            Date TEXT NOT NULL, Cents INTEGER NOT NULL CHECK(Cents>0), RequestId TEXT NOT NULL UNIQUE, PayloadHash TEXT NOT NULL,
            SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)), SnapshotHash TEXT NOT NULL, ApprovedBy TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE INDEX TripPaymentReleases_Link ON TripPaymentReleases(LinkId,Date);
        CREATE TRIGGER TripPaymentLinks_NoUpdate BEFORE UPDATE ON TripPaymentLinks BEGIN SELECT RAISE(ABORT,'Trip payment links are immutable'); END;
        CREATE TRIGGER TripPaymentLinks_NoDelete BEFORE DELETE ON TripPaymentLinks BEGIN SELECT RAISE(ABORT,'Trip payment links are immutable'); END;
        CREATE TRIGGER TripPaymentReleases_NoUpdate BEFORE UPDATE ON TripPaymentReleases BEGIN SELECT RAISE(ABORT,'Trip payment releases are immutable'); END;
        CREATE TRIGGER TripPaymentReleases_NoDelete BEFORE DELETE ON TripPaymentReleases BEGIN SELECT RAISE(ABORT,'Trip payment releases are immutable'); END;
        """;
}
