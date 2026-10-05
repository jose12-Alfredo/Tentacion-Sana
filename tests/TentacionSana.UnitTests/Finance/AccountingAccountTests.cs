using TentacionSana.Domain.Finance;

namespace TentacionSana.UnitTests.Finance;

public sealed class AccountingAccountTests
{
    [Fact]
    public void CreateNormalizesCodeAndKeepsAccountingKind()
    {
        var account = AccountingAccount.Create(" 5203 ", "Pasajes urbanos", AccountingAccountKind.OperatingExpense,
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal("5203", account.Code);
        Assert.Equal("Pasajes urbanos", account.Name);
        Assert.Equal(AccountingAccountKind.OperatingExpense, account.Kind);
        Assert.True(account.IsActive);
    }

    [Fact]
    public void AccountCanBeDeactivatedWithoutDeletingItsHistory()
    {
        var account = AccountingAccount.Create("5203", "Pasajes", AccountingAccountKind.OperatingExpense,
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        account.SetActive(false);

        Assert.False(account.IsActive);
    }
}
