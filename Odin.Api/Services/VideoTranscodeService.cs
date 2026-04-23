using System.Diagnostics;

namespace Odin.Api.Services;

public interface IVideoTranscodeService
{
    /// <summary>
    /// Transcodes an animated GIF into an MP4 (H.264 + faststart) suitable for &lt;video&gt; playback.
    /// Returns null if ffmpeg is not available on the host; throws on any other failure.
    /// </summary>
    Task<byte[]?> GifToMp4Async(byte[] gifBytes, CancellationToken cancellationToken = default);

    /// <summary>Returns true if the ffmpeg binary is resolvable on PATH.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

public class VideoTranscodeService(ILogger<VideoTranscodeService> logger) : IVideoTranscodeService
{
    // Resolved once at type init. Order: FFMPEG_PATH env var → known winget locations on Windows → "ffmpeg" on PATH.
    private static readonly string FfmpegExecutable = ResolveFfmpegPath();

    private static string ResolveFfmpegPath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                // winget's shim directory (newer winget versions add here).
                var shim = Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "ffmpeg.exe");
                if (File.Exists(shim)) return shim;

                // Gyan.FFmpeg winget package — extracts to a versioned sub-folder, never edits PATH.
                var packagesRoot = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
                if (Directory.Exists(packagesRoot))
                {
                    try
                    {
                        var match = Directory.EnumerateDirectories(packagesRoot, "Gyan.FFmpeg*")
                            .SelectMany(dir => Directory.EnumerateFiles(dir, "ffmpeg.exe", SearchOption.AllDirectories))
                            .FirstOrDefault();
                        if (match is not null) return match;
                    }
                    catch
                    {
                        // Permission or IO issue — fall through to PATH lookup.
                    }
                }
            }
        }

        return "ffmpeg";
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = FfmpegExecutable,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync(cancellationToken);
            if (proc.ExitCode == 0)
            {
                logger.LogInformation("ffmpeg resolved to: {Path}", FfmpegExecutable);
                return true;
            }
            logger.LogWarning("ffmpeg at {Path} exited {Code}", FfmpegExecutable, proc.ExitCode);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning("ffmpeg probe failed for {Path}: {Message}", FfmpegExecutable, ex.Message);
            return false;
        }
    }

    public async Task<byte[]?> GifToMp4Async(byte[] gifBytes, CancellationToken cancellationToken = default)
    {
        if (gifBytes.Length == 0) return null;
        if (!await IsAvailableAsync(cancellationToken))
        {
            logger.LogWarning("ffmpeg not available on PATH — skipping video transcode.");
            return null;
        }

        var gifPath = Path.Combine(Path.GetTempPath(), $"qpadm-gif-{Guid.NewGuid():N}.gif");
        var mp4Path = Path.Combine(Path.GetTempPath(), $"qpadm-mp4-{Guid.NewGuid():N}.mp4");

        try
        {
            await File.WriteAllBytesAsync(gifPath, gifBytes, cancellationToken);

            // H.264 MP4 with faststart, yuv420p for broad compatibility, even dimensions required by H.264.
            // -an strips audio (GIFs have none). -crf 28 is a good web-quality preset.
            var args =
                $"-y -i \"{gifPath}\" -movflags +faststart -pix_fmt yuv420p " +
                "-vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset medium -crf 28 -an " +
                $"\"{mp4Path}\"";

            var psi = new ProcessStartInfo
            {
                FileName = FfmpegExecutable,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start ffmpeg process.");

            var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
            {
                var err = await stderrTask;
                logger.LogError("ffmpeg exited with code {Code}. stderr: {Stderr}", proc.ExitCode, err);
                return null;
            }

            if (!File.Exists(mp4Path))
                return null;

            return await File.ReadAllBytesAsync(mp4Path, cancellationToken);
        }
        finally
        {
            try { if (File.Exists(gifPath)) File.Delete(gifPath); } catch { /* ignored */ }
            try { if (File.Exists(mp4Path)) File.Delete(mp4Path); } catch { /* ignored */ }
        }
    }
}
