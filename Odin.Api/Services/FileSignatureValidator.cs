namespace Odin.Api.Services;

/// <summary>
/// Magic-byte (a.k.a. file-signature) validation for uploads. MIME types come from the client
/// and are trivially spoofed; inspecting the first bytes is the only way to confirm the payload
/// actually matches the declared kind. Used by file/audio upload paths to reject "rename .exe
/// to .wav" attacks and corrupt-file class bugs at the boundary, before we persist anything.
/// </summary>
public static class FileSignatureValidator
{
    /// <summary>
    /// Returns true if the byte sequence matches the WAV (RIFF/WAVE) magic-number contract:
    /// bytes 0-3 = "RIFF", bytes 8-11 = "WAVE". The 4-byte size field at offset 4 is opaque
    /// and intentionally not inspected.
    /// </summary>
    public static bool IsWavAudio(byte[] data)
    {
        if (data is null || data.Length < 12) return false;
        return data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
            && data[8] == 'W' && data[9] == 'A' && data[10] == 'V' && data[11] == 'E';
    }

    /// <summary>
    /// Heuristic for raw genetic-data uploads (23andMe, AncestryDNA, MyHeritage, FTDNA, ...).
    /// Vendor formats differ but all are either plain text (typically prefixed with a `#`
    /// comment header or a CSV header row) or compressed text. Accepts:
    ///   (a) ZIP archive — leading "PK" bytes (0x50 0x4B) with 0x03/0x05/0x07 type byte
    ///   (b) GZIP — leading bytes 0x1F 0x8B
    ///   (c) Plain text — first 1 KB is printable ASCII/UTF-8, no NULs, &lt;5% non-printables
    /// Rejects binary executables, images, and anything with embedded NUL bytes in the header.
    /// </summary>
    public static bool LooksLikeGeneticFile(byte[] data)
    {
        if (data is null || data.Length < 16) return false;

        if (data[0] == 'P' && data[1] == 'K'
            && (data[2] == 0x03 || data[2] == 0x05 || data[2] == 0x07))
            return true;

        if (data[0] == 0x1F && data[1] == 0x8B)
            return true;

        var sampleSize = Math.Min(data.Length, 1024);
        var nonPrintable = 0;
        for (var i = 0; i < sampleSize; i++)
        {
            var b = data[i];
            if (b == 0x00) return false;
            // Allow tab (0x09), LF (0x0A), CR (0x0D), printable ASCII (0x20–0x7E), and any
            // high-byte (0x80+) which covers UTF-8 multi-byte sequences.
            if ((b < 0x09) || (b > 0x0D && b < 0x20) || b == 0x7F) nonPrintable++;
        }
        return nonPrintable * 20 < sampleSize;
    }
}
