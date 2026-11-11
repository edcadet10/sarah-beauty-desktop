using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

internal sealed class BusinessFixture : IDisposable
{
    internal const string Phrase = "fictional meadow lantern window";
    internal TestStore Files { get; } = new();
    internal BusinessStore Store { get; }
    internal OwnerSession Owner { get; }
    internal BusinessFixture()
    {
        Store = new BusinessStore(Files.DatabasePath);
        Owner = new OwnerAccess(Store).Setup("Fictional Beauty", "Alex", Phrase, "Morgan", Phrase);
    }
    public void Dispose() => Files.Dispose();
}
