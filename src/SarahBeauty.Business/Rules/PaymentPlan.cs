namespace SarahBeauty.Business.Rules;

public sealed record PaymentPlan(long TrialCents, long DepositCents, long FinalCents)
{
    public long TotalCents => checked(TrialCents + DepositCents + FinalCents);

    public static PaymentPlan Bridal(long totalCents, long trialCents)
    {
        if (totalCents < 0 || trialCents < 0 || trialCents > totalCents)
            throw new ArgumentException("The trial must be between zero and the booking total.");
        long remaining = totalCents - trialCents;
        long deposit = checked(remaining / 2 + remaining % 2);
        return new(trialCents, deposit, remaining - deposit);
    }

    public static PaymentPlan Custom(long totalCents, long depositCents)
    {
        if (totalCents < 0 || depositCents < 0 || depositCents > totalCents)
            throw new ArgumentException("The deposit must be between zero and the total.");
        return new(0, depositCents, totalCents - depositCents);
    }

    public static long[] Allocate(long cents, IReadOnlyList<long> weights)
    {
        if (cents < 0 || weights.Count == 0 || weights.Any(w => w < 0)) throw new ArgumentException("Invalid allocation.");
        long totalWeight = weights.Aggregate(0L, (sum, weight) => checked(sum + weight));
        if (totalWeight <= 0) throw new ArgumentException("Allocation weights must total more than zero.");
        long[] shares = weights.Select(w => (long)((System.Numerics.BigInteger)cents * w / totalWeight)).ToArray();
        long remainder = cents - shares.Sum();
        for (int i = 0; remainder > 0; i = (i + 1) % shares.Length)
            if (weights[i] > 0) { shares[i]++; remainder--; }
        return shares;
    }
}
