using SarahBeauty.Business.Customers;

namespace SarahBeauty.Storage.Tests;

public sealed class CustomerBookTests
{
    [Fact]
    public void NameOnlyCustomerCanBeSavedAndContactWarningTracksEdits()
    {
        using var fixture = new BusinessFixture();
        var book = new CustomerBook(fixture.Store);
        var customer = book.Save(fixture.Owner, new Customer { FullName = " Jordan Example ", Phone = " " });
        Assert.Equal("Jordan Example", customer.FullName);
        Assert.Equal("Contact information missing", customer.ContactWarning);
        customer = book.Save(fixture.Owner, customer with { Email = "jordan@example.com" });
        Assert.Empty(customer.ContactWarning);
        customer = book.Save(fixture.Owner, customer with { Email = "" });
        Assert.NotEmpty(customer.ContactWarning);
        Assert.Equal(customer, Assert.Single(book.Search(fixture.Owner, "example")));
    }

    [Theory]
    [InlineData(" ", "", "")]
    [InlineData("Example", "bad", "")]
    [InlineData("Example", "", "not an email")]
    public void InvalidCustomerDoesNotCreateARecord(string name, string phone, string email)
    {
        using var fixture = new BusinessFixture();
        var book = new CustomerBook(fixture.Store);
        Assert.Throws<ArgumentException>(() => book.Save(fixture.Owner,
            new Customer { FullName = name, Phone = phone, Email = email }));
        Assert.Empty(book.Search(fixture.Owner));
    }

    [Fact]
    public void DuplicateNamesArchiveAndPermissionHistoryKeepSeparateIdentities()
    {
        using var fixture = new BusinessFixture();
        var book = new CustomerBook(fixture.Store);
        var first = book.Save(fixture.Owner, new Customer { FullName = "Shared Example", PhotoPermission = "Granted", PermissionScope = "Portfolio" });
        var second = book.Save(fixture.Owner, new Customer { FullName = "Shared Example" });
        Assert.NotEqual(first.Id, second.Id);
        first = book.Save(fixture.Owner, first with { Archived = true, PhotoPermission = "Withdrawn" });
        Assert.Single(book.Search(fixture.Owner));
        Assert.Equal(2, book.Search(fixture.Owner, includeArchived: true).Count);
        Assert.Equal(new[] { "Granted", "Withdrawn" }, book.Permissions(fixture.Owner, first.Id).Select(p => p.Status));
    }

    [Fact]
    public void StaleEditIsRejectedWithoutOverwritingAnotherOwnersSave()
    {
        using var fixture = new BusinessFixture();
        var book = new CustomerBook(fixture.Store);
        var original = book.Save(fixture.Owner, new Customer { FullName = "Example" });
        var updated = book.Save(fixture.Owner, original with { Phone = "212-555-0100" });
        Assert.Throws<InvalidOperationException>(() => book.Save(fixture.Owner, original with { FullName = "Stale" }));
        Assert.Equal(updated, Assert.Single(book.Search(fixture.Owner)));
    }
}
