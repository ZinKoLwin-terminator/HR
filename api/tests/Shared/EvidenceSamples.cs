using System.IO.Compression;
using System.Text;

namespace Apex.Hrms.TestSupport;

/// <summary>Fictional, generated evidence files: just enough of a PDF, PNG, JPEG or Office package for the content checks. No real document is ever used.</summary>
internal static class EvidenceSamples
{
    public static byte[] Pdf(int length = 64) => Padded("%PDF-1.4\n%fictional evidence\n"u8.ToArray(), length);

    public static byte[] Png(int length = 64) => Padded([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], length);

    public static byte[] Jpeg(int length = 64) => Padded([0xFF, 0xD8, 0xFF, 0xE0], length);

    /// <summary>A zip with the parts of a Word document.</summary>
    public static byte[] Docx() => Zip("[Content_Types].xml", "word/document.xml");

    /// <summary>A zip with the parts of an Excel workbook.</summary>
    public static byte[] Xlsx() => Zip("[Content_Types].xml", "xl/workbook.xml");

    /// <summary>A zip with the given entries (each holding a few bytes of fictional text).</summary>
    public static byte[] Zip(params string[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8);
                writer.Write("<fictional/>");
            }
        }

        return stream.ToArray();
    }

    /// <summary>Text that is not a file of any allowed kind (an HTML page).</summary>
    public static byte[] Html() => Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");

    /// <summary>A Windows executable header ("MZ").</summary>
    public static byte[] Executable() => Padded([0x4D, 0x5A, 0x90, 0x00], 64);

    public static byte[] Svg() => Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

    private static byte[] Padded(byte[] header, int length)
    {
        var content = new byte[Math.Max(length, header.Length)];
        header.CopyTo(content, 0);
        return content;
    }
}
