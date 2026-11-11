using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Resources;

internal static class TripPaymentRules
{
    internal static TripPaymentBasis Basis(MileageTrip trip)
    {
        string json=JsonSerializer.Serialize(new{trip.Name,trip.TripDate,trip.DriverOwnerId,trip.DriverArtistId,trip.BookingId,trip.Origin,trip.Destination,
            trip.ActualMiles,trip.ParkingCents,trip.TollsCents,trip.ReimbursementStatus,trip.RequestedReimbursementCents});
        return new("1:"+TripPaymentQueries.Hash(json),trip.TripDate??DateOnly.MinValue,trip.RequestedReimbursementCents??0,trip.DriverOwnerId,trip.DriverArtistId);
    }

    internal static TripPaymentPreview Preview(DbWork db,BusinessStore store,OwnerSession owner,TripPaymentDraft draft)
    {
        Money.Positive(draft.Cents);
        if(string.IsNullOrWhiteSpace(draft.PayeeReview) || string.IsNullOrWhiteSpace(draft.EvidenceId))throw new ArgumentException("Confirm the actual driver/payee relationship and supporting payment evidence.");
        var trip=new RecordStore<MileageTrip>(store,"MileageTrip").Get(db,owner,draft.TripId);
        if(trip.Archived || trip.ReimbursementStatus!="Requested" || trip.RequestedReimbursementCents is null or <=0)
            throw new ArgumentException("Use an active trip with an explicitly reviewed positive reimbursement request.");
        PostingValidation.CheckClosedPeriod(db,owner,draft.Date);var payment=TripPaymentQueries.Payment(db,owner,draft.PaymentId);
        var state=TripPaymentQueries.State(db,owner,trip.Id,payment.Id);Chronology(state,trip.Id,draft.Date);
        var basis=Basis(trip);var candidate=new TripPaymentAllocation("proposed",trip.Id,basis,payment.Id,draft.Date,draft.Cents);
        TripPaymentBounds.Validate([..state.Links,candidate],state.Releases,state.Payments);
        return new(draft,trip,payment,basis.Hash,TripPaymentQueries.Watermark(db,owner),
            (long)TripPaymentAmounts.Paid(state.Links.Where(l=>l.TripId==trip.Id && l.Basis.Hash==basis.Hash),state.Releases,draft.Date),
            (long)TripPaymentAmounts.Unlinked(state.Payments.Single(p=>p.Id==payment.Id),state.Links,state.Releases,draft.Date));
    }

    internal static TripPaymentReleasePreview ReleasePreview(DbWork db,OwnerSession owner,TripPaymentReleaseDraft draft)
    {
        Money.Positive(draft.Cents);
        if(string.IsNullOrWhiteSpace(draft.LinkId) || string.IsNullOrWhiteSpace(draft.Reason) || string.IsNullOrWhiteSpace(draft.EvidenceId))throw new ArgumentException("Choose an original link and record the actual reason and evidence for releasing it.");
        var original=TripPaymentQueries.Links(db,owner,id:draft.LinkId).SingleOrDefault()??throw new ArgumentException("Choose an original trip payment link from this business.");
        var link=TripPaymentQueries.Allocation(original);var state=TripPaymentQueries.State(db,owner,link.TripId,link.PaymentId);
        PostingValidation.CheckClosedPeriod(db,owner,draft.Date);Chronology(state,link.TripId,draft.Date);
        TripPaymentBounds.Validate(state.Links,[..state.Releases,new("proposed",link.Id,draft.Date,draft.Cents)],state.Payments);
        return new(draft,original,TripPaymentQueries.Watermark(db,owner),(long)TripPaymentAmounts.Remaining(link,state.Releases,draft.Date));
    }

    private static void Chronology(TripPaymentState state,string trip,DateOnly date)
    {
        var links=state.Links.Where(l=>l.TripId==trip).ToArray();
        var dates=links.Select(l=>l.Date).Concat(state.Releases.Where(r=>links.Any(l=>l.Id==r.AllocationId)).Select(r=>r.Date));
        if(dates.Any(d=>d>date))throw new ArgumentException("Record this trip's payment links and releases in date order. Review its later actions first.");
    }

    internal static void ValidateTripEdit(DbWork db,OwnerSession owner,MileageTrip previous,MileageTrip value)
    {
        var state=TripPaymentQueries.State(db,owner,previous.Id,"");
        if(TripPaymentAmounts.Paid(state.Links,state.Releases,DateOnly.MaxValue)>0 && Basis(previous)!=Basis(value))
            throw new ArgumentException("Release the trip's active reimbursement links before changing its driver, actual trip basis, or reviewed claim amount. Existing payment and claim snapshots remain retained.");
    }

    internal static void ValidateMovement(DbWork db,OwnerSession owner,FinancialDraft draft)
    {
        var change=Change(db,owner,draft);
        if(change is null || TripPaymentQueries.Links(db,owner,payment:change.Value.Payment).Count==0)return;
        var state=TripPaymentQueries.State(db,owner,"",change.Value.Payment);TripPaymentBounds.Validate(state.Links,state.Releases,state.Payments);
    }

    internal static void PreviewMovement(DbWork db,OwnerSession owner,FinancialDraft draft)
    {
        var change=Change(db,owner,draft);
        if(change is null || TripPaymentQueries.Links(db,owner,payment:change.Value.Payment).Count==0)return;
        var state=TripPaymentQueries.State(db,owner,"",change.Value.Payment);
        var payments=state.Payments.Select(p=>p.Id==change.Value.Payment?p with{Adjustments=[..p.Adjustments,change.Value.Adjustment]}:p).ToArray();
        TripPaymentBounds.Validate(state.Links,state.Releases,payments);
    }

    private static (string Payment,TripPaymentAdjustment Adjustment)? Change(DbWork db,OwnerSession owner,FinancialDraft draft)
    {
        if(draft.Kind==FinancialKind.SupplierRefund)return(draft.OriginalId,new("proposed",draft.Kind,draft.Date!.Value,draft.AmountCents,OriginalId:draft.OriginalId));
        if(draft.Kind!=FinancialKind.Reversal)return null;
        var original=CorrectionRules.Original(db,owner,draft.OriginalId);
        string payment=original.Kind is FinancialKind.Expense or FinancialKind.OwnerReimbursement?draft.OriginalId:original.Kind==FinancialKind.SupplierRefund?original.OriginalId:"";
        return payment.Length==0?null:(payment,new("proposed",draft.Kind,draft.Date!.Value,draft.AmountCents,ReversesId:draft.OriginalId));
    }
}
