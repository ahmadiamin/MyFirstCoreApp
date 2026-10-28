using Microsoft.Extensions.Options;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Infrastructure.Services;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public class LocalAttachmentStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mfca-store-" + Guid.NewGuid().ToString("N"));
    private readonly LocalAttachmentStore _store;

    public LocalAttachmentStoreTests()
    {
        _store = new LocalAttachmentStore(Options.Create(new AttachmentOptions { RootPath = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Save_ThenOpenRead_ReturnsTheSameBytes()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        var path = await _store.SaveAsync(7, content, ".png");

        await using var stream = _store.OpenRead(path);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);

        Assert.Equal(content, memory.ToArray());
    }

    [Fact]
    public async Task Save_UsesTenantFolder_AndARandomName_NotAUserName()
    {
        var first = await _store.SaveAsync(7, new byte[] { 1 }, ".pdf");
        var second = await _store.SaveAsync(7, new byte[] { 1 }, ".pdf");

        Assert.StartsWith("7" + Path.DirectorySeparatorChar, first);
        Assert.EndsWith(".pdf", first);
        Assert.NotEqual(first, second);

        // نام فایل فقط hex تصادفی است
        var fileName = Path.GetFileNameWithoutExtension(first);
        Assert.Equal(32, fileName.Length);
        Assert.True(fileName.All(Uri.IsHexDigit));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("7/../../outside.png")]
    public void Paths_OutsideTheRoot_AreRefused(string relativePath)
    {
        Assert.Throws<InvalidOperationException>(() => _store.OpenRead(relativePath));
    }

    [Fact]
    public void RootedAndEmptyPaths_AreRefused()
    {
        Assert.Throws<InvalidOperationException>(() => _store.OpenRead(Path.GetTempFileName()));
        Assert.Throws<InvalidOperationException>(() => _store.OpenRead(""));
    }

    [Fact]
    public void OpenRead_OfAMissingFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => _store.OpenRead(Path.Combine("7", "none.png")));
    }

    [Fact]
    public async Task Delete_RemovesTheFile_AndNeverThrows()
    {
        var path = await _store.SaveAsync(1, new byte[] { 1 }, ".png");

        _store.Delete(path);
        Assert.Throws<FileNotFoundException>(() => _store.OpenRead(path));

        // دوباره و با مسیر بیرون از ریشه: خطا نمی‌دهد
        _store.Delete(path);
        _store.Delete("../outside.png");
    }
}