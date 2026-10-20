using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace MyFirstCoreApp.Tests.Persistence;

/// <summary>
/// تست‌های ایزوله‌سازی شرکت‌ها، Audit و حذف نرم.
/// از SQLite درون‌حافظه استفاده می‌شود (به SQL Server واقعی نزدیک‌تر از InMemory است
/// و ستون‌های «تغییرنکرده» را هم درست می‌نویسد).
/// </summary>
public class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FakeTenant _tenant = new();
    private readonly FakeUser _user = new();

    public TenantIsolationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(_user, _tenant))
            .Options;

        return new ApplicationDbContext(options, _tenant);
    }

    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_FillsTenantIdAndAuditFields_FromCurrentContext()
    {
        _tenant.TenantId = 1;
        _user.UserId = "user-1";

        await using var ctx = NewContext();
        var contact = new Contact { Name = "علی رستمی" };
        ctx.Contacts.Add(contact);
        await ctx.SaveChangesAsync();

        Assert.Equal(1, contact.TenantId);
        Assert.Equal("user-1", contact.CreatedBy);
    }

    [Fact]
    public async Task Add_WithoutActiveTenant_Throws()
    {
        _tenant.TenantId = null;

        await using var ctx = NewContext();
        ctx.Contacts.Add(new Contact { Name = "بدون شرکت" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Add_ForAnotherTenant_Throws()
    {
        _tenant.TenantId = 1;

        await using var ctx = NewContext();
        ctx.Contacts.Add(new Contact { Name = "شرکت دیگر", TenantId = 2 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Query_ReturnsOnlyCurrentTenantRecords()
    {
        await Seed(tenantId: 1, name: "الف");
        await Seed(tenantId: 2, name: "ب");

        await using var ctx = NewContext();

        _tenant.TenantId = 1;
        var forTenant1 = await ctx.Contacts.ToListAsync();
        Assert.Single(forTenant1);
        Assert.Equal("الف", forTenant1[0].Name);

        // همان Context، شرکت دیگر ← فیلتر باید دوباره ارزیابی شود
        _tenant.TenantId = 2;
        var forTenant2 = await ctx.Contacts.ToListAsync();
        Assert.Single(forTenant2);
        Assert.Equal("ب", forTenant2[0].Name);
    }

    [Fact]
    public async Task Query_WithoutActiveTenant_ReturnsNothing()
    {
        await Seed(tenantId: 1, name: "الف");

        _tenant.TenantId = null;
        await using var ctx = NewContext();

        Assert.Empty(await ctx.Contacts.ToListAsync());
    }

    [Fact]
    public async Task Remove_BecomesSoftDelete_AndIsHiddenFromNormalQueries()
    {
        await Seed(tenantId: 1, name: "الف");
        _tenant.TenantId = 1;
        _user.UserId = "deleter";

        await using (var ctx = NewContext())
        {
            var contact = await ctx.Contacts.SingleAsync();
            ctx.Contacts.Remove(contact);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = NewContext())
        {
            Assert.Empty(await ctx.Contacts.ToListAsync());

            var deleted = await ctx.SetIncludingDeleted<Contact>().SingleAsync();
            Assert.True(deleted.IsDeleted);
            Assert.Equal("deleter", deleted.DeletedBy);
            Assert.NotNull(deleted.DeletedAt);
        }
    }

    [Fact]
    public async Task SetIncludingDeleted_NeverLeaksOtherTenantRecords()
    {
        await Seed(tenantId: 1, name: "الف");
        await Seed(tenantId: 2, name: "ب");

        _tenant.TenantId = 1;
        await using var ctx = NewContext();

        var all = await ctx.SetIncludingDeleted<Contact>().ToListAsync();

        Assert.Single(all);
        Assert.Equal(1, all[0].TenantId);
    }

    [Fact]
    public async Task Update_OfDetachedEntity_DoesNotOverwriteCreatedFields()
    {
        _tenant.TenantId = 1;
        _user.UserId = "creator";

        int id;
        await using (var ctx = NewContext())
        {
            var contact = new Contact { Name = "قبل" };
            ctx.Contacts.Add(contact);
            await ctx.SaveChangesAsync();
            id = contact.Id;
        }

        _user.UserId = "editor";
        var fakeOldDate = new DateTime(2000, 1, 1);

        await using (var ctx = NewContext())
        {
            // شبیه‌سازی ViewModel → Entity که CreatedAt/CreatedBy را ندارد
            var detached = new Contact
            {
                Id = id,
                TenantId = 1,
                Name = "بعد",
                CreatedAt = fakeOldDate,
                CreatedBy = null
            };
            ctx.Contacts.Update(detached);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = NewContext())
        {
            var loaded = await ctx.Contacts.SingleAsync(x => x.Id == id);

            Assert.Equal("بعد", loaded.Name);
            Assert.Equal("creator", loaded.CreatedBy);
            Assert.NotEqual(fakeOldDate, loaded.CreatedAt);
            Assert.Equal("editor", loaded.UpdatedBy);
            Assert.NotNull(loaded.UpdatedAt);
        }
    }

    // ---------------------------------------------------------------

    /// <summary>ثبت یک رکورد برای شرکت مشخص، بعد برگرداندن شرکت فعال به حالت قبل.</summary>
    private async Task Seed(int tenantId, string name)
    {
        var previous = _tenant.TenantId;
        _tenant.TenantId = tenantId;

        await using (var ctx = NewContext())
        {
            ctx.Contacts.Add(new Contact { Name = name });
            await ctx.SaveChangesAsync();
        }

        _tenant.TenantId = previous;
    }

    private sealed class FakeTenant : ICurrentTenant
    {
        public int? TenantId { get; set; }
        public string? TenantName => null;
        public bool IsSet => TenantId.HasValue;
    }

    private sealed class FakeUser : ICurrentUser
    {
        public string? UserId { get; set; }
        public string? UserName => UserId;
        public bool IsAuthenticated => UserId != null;
    }
}
