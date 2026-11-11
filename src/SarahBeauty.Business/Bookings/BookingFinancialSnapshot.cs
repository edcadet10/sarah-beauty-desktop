namespace SarahBeauty.Business.Bookings;

public sealed record BookingFinancialEntry(string MovementId,DateOnly Date,string Kind,string EffectiveKind,string Memo,string AccountCode,
    string AccountName,long DebitCents,long CreditCents,bool IsDirectCost,bool IsServiceIncome);

public sealed record BookingCashSource(string MovementId,DateOnly Date,string Kind,string EffectiveKind,string Memo,string CashAccountId,
    long SignedGrossCents,long SignedTaxCents,long SignedFeeCents)
{
    public long SignedNetCashCents=>checked(SignedGrossCents-SignedFeeCents);
}

public sealed record BookingFinancialSnapshot(string BusinessName,string Mode,long Watermark,string GeneratedAtUtc,DateOnly From,DateOnly Through,
    string BookingId,string CurrentBookingName,int CurrentBookingRevision,BookingFinancialEntry[] Entries,BookingCashSource[] Cash)
{
    public long RecordedDirectCostCents=>Entries.Where(e=>e.IsDirectCost).Sum(e=>e.DebitCents-e.CreditCents);
    public long PeriodRecordedDirectCostCents=>Entries.Where(e=>e.IsDirectCost && e.Date>=From).Sum(e=>e.DebitCents-e.CreditCents);
    public long RecognizedServiceIncomeCents=>Entries.Where(e=>e.IsServiceIncome).Sum(e=>e.CreditCents-e.DebitCents);
    public long PeriodRecognizedServiceIncomeCents=>Entries.Where(e=>e.IsServiceIncome && e.Date>=From).Sum(e=>e.CreditCents-e.DebitCents);
}
