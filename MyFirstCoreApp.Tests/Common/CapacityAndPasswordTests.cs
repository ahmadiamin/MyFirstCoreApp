using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Infrastructure.Security;
using Xunit;

namespace MyFirstCoreApp.Tests.Common;

public class PasswordGeneratorTests
{
    [Fact]
    public void Generated_PassesTheDefaultIdentityPolicy_OnEveryCall()
    {
        for (var i = 0; i < 200; i++)
        {
            var password = PasswordGenerator.Generate();

            Assert.Equal(20, password.Length);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, char.IsDigit);
            Assert.Contains(password, c => !char.IsLetterOrDigit(c));
        }
    }

    [Fact]
    public void Generated_AreUnique()
    {
        var passwords = Enumerable.Range(0, 500).Select(_ => PasswordGenerator.Generate()).ToList();

        Assert.Equal(passwords.Count, passwords.Distinct().Count());
    }

    [Fact]
    public void Generated_AvoidsLookAlikeCharacters()
    {
        var all = string.Concat(Enumerable.Range(0, 100).Select(_ => PasswordGenerator.Generate()));

        Assert.DoesNotContain('O', all);
        Assert.DoesNotContain('0', all);
        Assert.DoesNotContain('l', all);
        Assert.DoesNotContain('I', all);
        Assert.DoesNotContain('1', all);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void TooShortLengths_AreRejected(int length)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.Generate(length));

    [Fact]
    public void CustomLength_IsHonoured()
        => Assert.Equal(32, PasswordGenerator.Generate(32).Length);
}

public class PhoneMaskTests
{
    [Theory]
    [InlineData("09121234567", "0912***4567")]
    [InlineData("09350000000", "0935***0000")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Mask_HidesTheMiddleDigits(string? input, string expected)
        => Assert.Equal(expected, PhoneMask.Mask(input));

    [Fact]
    public void Mask_OfAShortValue_HidesEverything()
        => Assert.Equal("****", PhoneMask.Mask("1234"));
}

public class LoginCapacityInfoTests
{
    private static TenantRef T(int id) => new(id, $"شرکت {id}");

    [Fact]
    public void OnlyOwner_OrOnlyEmployee_NeverAsks()
    {
        var owner = new LoginCapacityInfo { OwnerTenants = { T(1), T(2) } };
        Assert.False(owner.NeedsChoice);
        Assert.Equal(LoginCapacity.Owner, owner.OnlyCapacity);

        var employee = new LoginCapacityInfo { EmployeeTenants = { T(3) } };
        Assert.False(employee.NeedsChoice);
        Assert.Equal(LoginCapacity.Employee, employee.OnlyCapacity);
    }

    [Fact]
    public void BothCapacities_MustAsk()
    {
        var both = new LoginCapacityInfo { OwnerTenants = { T(1) }, EmployeeTenants = { T(2) } };

        Assert.True(both.NeedsChoice);
        Assert.Null(both.OnlyCapacity);
        Assert.Equal(new[] { 1 }, both.TenantsFor(LoginCapacity.Owner).Select(t => t.Id));
        Assert.Equal(new[] { 2 }, both.TenantsFor(LoginCapacity.Employee).Select(t => t.Id));
    }

    [Fact]
    public void NoMembership_HasNothingToAsk()
    {
        var none = new LoginCapacityInfo();

        Assert.False(none.NeedsChoice);
        Assert.Null(none.OnlyCapacity);
        Assert.False(none.HasOwner);
        Assert.False(none.HasEmployee);
    }
}
