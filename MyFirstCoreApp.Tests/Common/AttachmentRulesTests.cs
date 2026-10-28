using MyFirstCoreApp.Application.Attachments;
using Xunit;

namespace MyFirstCoreApp.Tests.Common;

public class AttachmentRulesTests
{
    [Theory]
    [InlineData("receipt.jpg", ".jpg")]
    [InlineData("RECEIPT.JPEG", ".jpeg")]
    [InlineData("scan.PNG", ".png")]
    [InlineData("a.b.c.webp", ".webp")]
    [InlineData("حواله بانکی.pdf", ".pdf")]
    public void TryGetExtension_AcceptsAllowedTypes_CaseInsensitively(string fileName, string expected)
    {
        Assert.True(AttachmentRules.TryGetExtension(fileName, out var extension));
        Assert.Equal(expected, extension);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("page.html")]
    [InlineData("vector.svg")]          // SVG می‌تواند اسکریپت داشته باشد
    [InlineData("photo.jpg.exe")]       // پسوند واقعی آخرین بخش است
    [InlineData("noextension")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryGetExtension_RejectsEverythingElse(string? fileName)
    {
        Assert.False(AttachmentRules.TryGetExtension(fileName, out _));
    }

    [Theory]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".pdf", "application/pdf")]
    public void ContentType_ComesFromTheExtension(string extension, string expected)
        => Assert.Equal(expected, AttachmentRules.ContentTypeFor(extension));

    [Fact]
    public void Signature_Jpeg_Png_Webp_Pdf_AreRecognised()
    {
        Assert.True(AttachmentRules.MatchesSignature(".jpg", Jpeg()));
        Assert.True(AttachmentRules.MatchesSignature(".jpeg", Jpeg()));
        Assert.True(AttachmentRules.MatchesSignature(".png", Png()));
        Assert.True(AttachmentRules.MatchesSignature(".webp", Webp()));
        Assert.True(AttachmentRules.MatchesSignature(".pdf", Pdf()));
    }

    [Fact]
    public void Signature_PdfWithLeadingBytes_IsAcceptedWithinFirst1024Bytes()
    {
        var content = new byte[100].Concat(Pdf()).ToArray();

        Assert.True(AttachmentRules.MatchesSignature(".pdf", content));
    }

    [Fact]
    public void Signature_RejectsMismatchedContent()
    {
        // فایل اجرایی ویندوز («MZ») با پسوند عکس
        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };

        Assert.False(AttachmentRules.MatchesSignature(".jpg", exe));
        Assert.False(AttachmentRules.MatchesSignature(".png", exe));
        Assert.False(AttachmentRules.MatchesSignature(".webp", exe));
        Assert.False(AttachmentRules.MatchesSignature(".pdf", exe));

        // PNG با پسوند JPG
        Assert.False(AttachmentRules.MatchesSignature(".jpg", Png()));
        Assert.False(AttachmentRules.MatchesSignature(".png", Jpeg()));

        // RIFF بدون WEBP (مثلاً WAV)
        var wav = "RIFF\0\0\0\0WAVE"u8.ToArray();
        Assert.False(AttachmentRules.MatchesSignature(".webp", wav));
    }

    [Fact]
    public void Signature_RejectsTooShortOrEmptyContent()
    {
        Assert.False(AttachmentRules.MatchesSignature(".jpg", new byte[] { 0xFF, 0xD8 }));
        Assert.False(AttachmentRules.MatchesSignature(".png", Array.Empty<byte>()));
        Assert.False(AttachmentRules.MatchesSignature(".pdf", new byte[] { 0x25, 0x50 }));
        Assert.False(AttachmentRules.MatchesSignature(".gif", Jpeg()));   // پسوند ناشناخته
    }

    [Theory]
    [InlineData("receipt.png", "receipt.png")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Users\\x\\scan.jpg", "scan.jpg")]
    [InlineData("a/b/c.pdf", "c.pdf")]
    [InlineData("  spaced name.png  ", "spaced name.png")]
    [InlineData("..", "file")]
    [InlineData("", "file")]
    [InlineData(null, "file")]
    public void SanitizeFileName_KeepsOnlyASafeDisplayName(string? input, string expected)
        => Assert.Equal(expected, AttachmentRules.SanitizeFileName(input));

    [Fact]
    public void SanitizeFileName_ReplacesControlAndDirectionalCharacters()
    {
        // RLO («\u202E») برای مخفی کردن پسوند واقعی در نام نمایشی
        var name = AttachmentRules.SanitizeFileName("photo\u202Egnp.exe");

        Assert.DoesNotContain('\u202E', name);
        Assert.DoesNotContain('\0', AttachmentRules.SanitizeFileName("a\0b.png"));
    }

    [Fact]
    public void SanitizeFileName_TruncatesLongNames_ButKeepsTheExtension()
    {
        var name = AttachmentRules.SanitizeFileName(new string('a', 400) + ".pdf");

        Assert.True(name.Length <= 150);
        Assert.EndsWith(".pdf", name);
    }

    private static byte[] Jpeg() => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    private static byte[] Png() => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
    private static byte[] Webp() => "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();
    private static byte[] Pdf() => "%PDF-1.7\n"u8.ToArray();
}
