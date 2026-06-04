using Accanto.Application.Documents;
using FluentAssertions;

namespace Accanto.Tests;

public class FileSignatureValidatorTests
{
    private static readonly byte[] PdfHeader = "%PDF-1.7\n"u8.ToArray();
    private static readonly byte[] PdfMinimal = "%PDF-1.7\n%body\n%%EOF\n"u8.ToArray();
    private static readonly byte[] PngHeader = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D
    };
    private static readonly byte[] PngWithIhdr = new byte[]
    {
        // Signature
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        // Chunk: length=13 (00 00 00 0D), type="IHDR"
        0x00, 0x00, 0x00, 0x0D, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
        // 13 byte payload IHDR placeholder
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00,
        // CRC placeholder
        0x00, 0x00, 0x00, 0x00,
    };
    private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
    private static readonly byte[] JpegWithEoi = new byte[]
    {
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9
    };
    private static readonly byte[] PlainAscii = "Hello, world!\n"u8.ToArray();
    private static readonly byte[] PlainUtf8WithBom = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'c', (byte)'i', (byte)'a', (byte)'o' };

    // ---- IsValid (back-compat head-only) ---------------------------

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

    // ---- Validate (struttura + estensione + magics pericolosi) -----

    [Fact]
    public void Validate_accepts_well_formed_pdf_with_correct_extension()
        => FileSignatureValidator.Validate(PdfMinimal, "application/pdf", "report.pdf")
            .Should().BeNull();

    [Fact]
    public void Validate_rejects_pdf_without_eof_marker()
    {
        var pdfNoEof = "%PDF-1.7\nsome content without eof"u8.ToArray();
        FileSignatureValidator.Validate(pdfNoEof, "application/pdf", "report.pdf")
            .Should().Contain("%%EOF");
    }

    [Fact]
    public void Validate_rejects_pdf_with_wrong_version()
    {
        var pdfV3 = "%PDF-3.0\n%%EOF\n"u8.ToArray();
        FileSignatureValidator.Validate(pdfV3, "application/pdf", "x.pdf")
            .Should().Contain("versione");
    }

    [Fact]
    public void Validate_accepts_png_with_ihdr_chunk()
        => FileSignatureValidator.Validate(PngWithIhdr, "image/png", "icon.png")
            .Should().BeNull();

    [Fact]
    public void Validate_rejects_png_without_ihdr_first_chunk()
    {
        var bogus = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, (byte)'T', (byte)'E', (byte)'X', (byte)'T'
        };
        FileSignatureValidator.Validate(bogus, "image/png", "x.png")
            .Should().Contain("IHDR");
    }

    [Fact]
    public void Validate_accepts_jpeg_with_eoi()
        => FileSignatureValidator.Validate(JpegWithEoi, "image/jpeg", "photo.jpg")
            .Should().BeNull();

    [Fact]
    public void Validate_rejects_jpeg_without_eoi()
    {
        var truncated = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        FileSignatureValidator.Validate(truncated, "image/jpeg", "photo.jpg")
            .Should().Contain("EOI");
    }

    [Fact]
    public void Validate_rejects_extension_mismatch()
        => FileSignatureValidator.Validate(PdfMinimal, "application/pdf", "evil.png")
            .Should().Contain("estensione");

    [Fact]
    public void Validate_rejects_executable_pe_regardless_of_declared_type()
    {
        var pe = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        FileSignatureValidator.Validate(pe, "text/plain", "notes.txt")
            .Should().Contain("eseguibile");
    }

    [Fact]
    public void Validate_rejects_elf_regardless_of_declared_type()
    {
        var elf = new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00 };
        FileSignatureValidator.Validate(elf, "application/pdf", "x.pdf")
            .Should().Contain("eseguibile");
    }

    [Fact]
    public void Validate_rejects_zip_disguised_as_text()
    {
        var zip = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00 };
        FileSignatureValidator.Validate(zip, "text/plain", "x.txt")
            .Should().Contain("eseguibile");
    }

    [Fact]
    public void Validate_rejects_text_with_invalid_utf8_sequence()
    {
        // Lone 0xC3 (start of 2-byte sequence) senza byte di continuazione.
        var bad = new byte[] { (byte)'H', (byte)'i', 0xC3 };
        FileSignatureValidator.Validate(bad, "text/plain", "x.txt")
            .Should().Contain("UTF-8");
    }

    [Fact]
    public void Validate_rejects_text_with_nul_byte()
    {
        var bad = new byte[] { (byte)'a', 0x00, (byte)'b' };
        FileSignatureValidator.Validate(bad, "text/plain", "x.txt")
            .Should().Contain("NUL");
    }

    [Fact]
    public void Validate_accepts_text_with_unicode_emoji()
    {
        // "Ciao 🎉" valid UTF-8.
        var ok = new byte[]
        {
            (byte)'C', (byte)'i', (byte)'a', (byte)'o', (byte)' ',
            0xF0, 0x9F, 0x8E, 0x89
        };
        FileSignatureValidator.Validate(ok, "text/plain", "x.txt")
            .Should().BeNull();
    }

    [Fact]
    public void Validate_rejects_unknown_content_type()
        => FileSignatureValidator.Validate(PdfMinimal, "application/x-msdownload", "x.exe")
            .Should().NotBeNull();
}
