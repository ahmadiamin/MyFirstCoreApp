using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Domain.Entities.Attachments;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public class AttachmentServiceTests : IDisposable
{
    private const int TenantA = 1;
    private const int TenantB = 2;

    private static readonly byte[] PngBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };

    private readonly TestDatabase _db = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mfca-att-" + Guid.NewGuid().ToString("N"));
    private readonly AttachmentOptions _options;

    public AttachmentServiceTests()
    {
        _db.Tenant.TenantId = TenantA;
        _db.User.UserId = "user-1";

        _options = new AttachmentOptions { RootPath = _root, MaxFileSizeBytes = 1024, MaxFilesPerEntity = 2 };
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ---------------------------------------------------------------
    // افزودن و خواندن
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ValidPng_StoresFile_RecordsInfo_AndCanBeOpenedAgain()
    {
        var contact = await AddContactAsync(TenantA);

        var info = await AddAsync(contact, "رسید بانک.png", PngBytes, description: "  واریز اول  ");

        Assert.Equal(AttachmentEntityType.Contact, info.EntityType);
        Assert.Equal(contact, info.EntityId);
        Assert.Equal("رسید بانک.png", info.FileName);
        Assert.Equal("image/png", info.ContentType);
        Assert.Equal(PngBytes.Length, info.SizeBytes);
        Assert.Equal("واریز اول", info.Description);

        await using var context = _db.NewContext();
        var file = await NewService(context).OpenAsync(info.Id);

        using var memory = new MemoryStream();
        await file.Content.CopyToAsync(memory);
        await file.Content.DisposeAsync();

        Assert.Equal(PngBytes, memory.ToArray());
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(AttachmentEntityType.Contact, file.EntityType);
    }

    [Fact]
    public async Task ContentType_ComesFromTheExtension_NotFromTheClient()
    {
        var contact = await AddContactAsync(TenantA);

        // کلاینت هر چه بگوید، نوع از روی پسوندِ تأییدشده ساخته می‌شود (هیچ ورودیِ ContentType وجود ندارد)
        var info = await AddAsync(contact, "scan.PNG", PngBytes);

        Assert.Equal("image/png", info.ContentType);
    }

    [Fact]
    public async Task List_ReturnsOnlyThisEntitysAttachments_InUploadOrder()
    {
        var first = await AddContactAsync(TenantA, "الف");
        var second = await AddContactAsync(TenantA, "ب");

        await AddAsync(first, "1.png", PngBytes);
        await AddAsync(first, "2.png", PngBytes);
        await AddAsync(second, "other.png", PngBytes);

        await using var context = _db.NewContext();
        var list = await NewService(context).ListAsync(AttachmentEntityType.Contact, first);

        Assert.Equal(new[] { "1.png", "2.png" }, list.Select(i => i.FileName).ToArray());
    }

    // ---------------------------------------------------------------
    // اعتبارسنجی
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_RejectsEmptyOversizedWrongTypeAndFakeContent()
    {
        var contact = await AddContactAsync(TenantA);

        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };

        await AssertRejectedAsync(contact, "empty.png", Array.Empty<byte>(), "خالی");
        await AssertRejectedAsync(contact, "big.png", PngBytes.Concat(new byte[2000]).ToArray(), "حجم");
        await AssertRejectedAsync(contact, "virus.exe", exe, "مجاز");
        await AssertRejectedAsync(contact, "fake.jpg", exe, "نمی‌خواند");          // پسوند عکس، محتوای اجرایی
        await AssertRejectedAsync(contact, "fake.png", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "نمی‌خواند");

        await using var context = _db.NewContext();
        Assert.Empty(await NewService(context).ListAsync(AttachmentEntityType.Contact, contact));
        Assert.False(Directory.Exists(_root) && Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task Add_DoesNotTrustTheDeclaredLength_ButCapsTheRealStream()
    {
        var contact = await AddContactAsync(TenantA);

        // طول اعلام‌شده کوچک، ولی جریان واقعی 2KB (سقف 1KB)
        var upload = new AttachmentUpload
        {
            FileName = "liar.png",
            Length = 10,
            Content = new MemoryStream(PngBytes.Concat(new byte[2000]).ToArray())
        };

        await using var context = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(context).AddAsync(AttachmentEntityType.Contact, contact, upload));

        Assert.Contains("حجم", ex.Message);
    }

    [Fact]
    public async Task Add_RespectsTheMaximumNumberOfFilesPerEntity()
    {
        var contact = await AddContactAsync(TenantA);

        await AddAsync(contact, "1.png", PngBytes);
        await AddAsync(contact, "2.png", PngBytes);

        await AssertRejectedAsync(contact, "3.png", PngBytes, "حداکثر");
    }

    [Fact]
    public async Task Add_ToAMissingOrForeignOwner_IsRejected()
    {
        var foreignContact = await AddContactAsync(TenantB);

        // موجودیتِ شرکت دیگر برای شرکت فعال «وجود ندارد»
        await AssertRejectedAsync(foreignContact, "x.png", PngBytes, "پیدا نشد");
        await AssertRejectedAsync(999_999, "x.png", PngBytes, "پیدا نشد");
    }

    // ---------------------------------------------------------------
    // جداسازی شرکت‌ها
    // ---------------------------------------------------------------

    [Fact]
    public async Task AnotherTenant_CannotSeeOpenOrDeleteMyAttachment()
    {
        var contact = await AddContactAsync(TenantA);
        var info = await AddAsync(contact, "secret.png", PngBytes);

        _db.Tenant.TenantId = TenantB;

        await using var context = _db.NewContext();
        var service = NewService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenAsync(info.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetInfoAsync(info.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(info.Id));
        Assert.Empty(await service.ListAsync(AttachmentEntityType.Contact, contact));
    }

    [Fact]
    public async Task WithoutAnActiveTenant_NothingWorks()
    {
        _db.Tenant.TenantId = null;

        await using var context = _db.NewContext();
        var service = NewService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListAsync(AttachmentEntityType.Contact, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenAsync(1));
    }

    // ---------------------------------------------------------------
    // حذف
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_SoftDeletesTheRecord_KeepsThePhysicalFile_AndHidesIt()
    {
        var contact = await AddContactAsync(TenantA);
        var info = await AddAsync(contact, "evidence.png", PngBytes);

        _db.User.UserId = "deleter";
        await using (var context = _db.NewContext())
            await NewService(context).DeleteAsync(info.Id);

        await using var verify = _db.NewContext();
        var service = NewService(verify);

        Assert.Empty(await service.ListAsync(AttachmentEntityType.Contact, contact));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenAsync(info.Id));

        var record = await verify.SetIncludingDeleted<Attachment>().SingleAsync(a => a.Id == info.Id);
        Assert.True(record.IsDeleted);
        Assert.Equal("deleter", record.DeletedBy);

        // فایل فیزیکی برای سابقه مانده است
        Assert.True(File.Exists(Path.Combine(_root, record.StoredPath)));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private AttachmentService NewService(ApplicationDbContext context)
        => new(context, _db.Tenant, new LocalAttachmentStore(Options.Create(_options)), Options.Create(_options));

    private async Task<AttachmentInfo> AddAsync(int contactId, string fileName, byte[] content, string? description = null)
    {
        await using var context = _db.NewContext();
        return await NewService(context).AddAsync(
            AttachmentEntityType.Contact,
            contactId,
            new AttachmentUpload
            {
                FileName = fileName,
                Length = content.Length,
                Content = new MemoryStream(content),
                Description = description
            });
    }

    private async Task AssertRejectedAsync(int contactId, string fileName, byte[] content, string expectedMessagePart)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => AddAsync(contactId, fileName, content));
        Assert.Contains(expectedMessagePart, ex.Message);
    }

    private async Task<int> AddContactAsync(int tenantId, string name = "مشتری")
    {
        var previous = _db.Tenant.TenantId;
        _db.Tenant.TenantId = tenantId;

        try
        {
            await using var context = _db.NewContext();
            var contact = new Contact { Name = name, ContactType = ContactType.Both, IsActive = true };
            context.Contacts.Add(contact);
            await context.SaveChangesAsync();
            return contact.Id;
        }
        finally
        {
            _db.Tenant.TenantId = previous;
        }
    }
}