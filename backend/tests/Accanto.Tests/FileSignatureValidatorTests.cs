using Accanto.Application.Documents;
using FluentAssertions;

namespace Accanto.Tests;

public class FileSignatureValidatorTests
{
    private static readonly byte[] PdfHeader = "%PDF-1.7\n"u8.ToArray();
    private static readonly byte[] PngHeader = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D
    };
    private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
    private static readonly byte[] PlainAscii = "Hello, world!\n"u8.ToArray();
    private static readonly byte[] PlainUtf8WithBom = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'c', (byte)'i', (byte)'a', (byte)'o' };

    [Fact]
    public void Pdf_signature_is_valid_for_pdf_content_type()
        => FileSignatureValidator.IsValid(PdfHeader, "application/pdf").Should().BeTrue();

    [Fact]
    public void Png_signature_is_valid_for_png_content_type()
        => FileSignatureValidator.IsValid(PngHeader, "image/png").Should().BeTrue();

    [Fact]
    public void Jpeg_signature_is_valid_for_jpeg_content_type()
        => FileSignatureValidator.IsValid(JpegHeader, "image/jpeg").Should().BeTrue();

    [Fact]
    public void Plain_ascii_is_valid_text_plain()
        => FileSignatureValidator.IsValid(PlainAscii, "text/plain").Should().BeTrue();

    [Fact]
    public void Utf8_bom_is_valid_text_plain()
        => FileSignatureValidator.IsValid(PlainUtf8WithBom, "text/plain").Should().BeTrue();

    [Fact]
    public void Pdf_content_with_png_declared_is_rejected()
        => FileSignatureValidator.IsValid(PdfHeader, "image/png").Should().BeFalse();

    [Fact]
    public void Png_content_with_pdf_declared_is_rejected()
        => FileSignatureValidator.IsValid(PngHeader, "application/pdf").Should().BeFalse();

    [Fact]
    public void Binary_content_with_text_plain_declared_is_rejected()
        => FileSignatureValidator.IsValid(PngHeader, "text/plain").Should().BeFalse();

    [Fact]
    public void Unknown_content_type_is_rejected()
        => FileSignatureValidator.IsValid(PdfHeader, "application/x-msdownload").Should().BeFalse();

    [Fact]
    public void Empty_head_is_rejected_for_any_type()
        => FileSignatureValidator.IsValid(ReadOnlySpan<byte>.Empty, "application/pdf").Should().BeFalse();
}
