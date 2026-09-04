using System.Diagnostics;

namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Encodes on the generator host, keeping native audio libraries off users' devices.</summary>
public sealed class FfmpegDvarTorahMp3Encoder : IDvarTorahMp3Encoder
{
    private readonly string executable;

    /// <summary>Initializes the server-side MP3 encoder.</summary>
    /// <param name="options">Approved local encoder executable configuration.</param>
    public FfmpegDvarTorahMp3Encoder(DvarTorahAudioOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FfmpegPath);
        executable = options.FfmpegPath;
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyMemory<byte>> EncodeAsync(Stream pcm, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pcm);
        if (!pcm.CanRead || !pcm.CanSeek || pcm.Length is <= 0 or > DvarTorahAudioValidation.MaximumPcmBytes || pcm.Length % 2 != 0)
        {
            throw new ArgumentException("A bounded seekable 16-bit PCM stream is required.", nameof(pcm));
        }
        cancellationToken.ThrowIfCancellationRequested();
        // A seekable output lets FFmpeg write the Xing duration and gapless metadata needed by mobile seeking.
        var outputPath = Path.Combine(Path.GetTempPath(), $"askarabbi-narration-{Guid.NewGuid():N}.mp3");
        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-f", "s16le", "-ar", "24000", "-ac", "1", "-i", "pipe:0", "-codec:a", "libmp3lame", "-b:a", "96k", "-write_xing", "1", "-n", outputPath })
        {
            startInfo.ArgumentList.Add(argument);
        }
        using var process = new Process { StartInfo = startInfo };
        var started = false;
        try
        {
            if (!process.Start())
            {
                throw new DvarTorahAudioException("EncoderStartFailed", "encoding");
            }
            started = true;
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process completion can race cancellation.
                }
            });
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            pcm.Position = 0;
            await pcm.CopyToAsync(process.StandardInput.BaseStream, cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new DvarTorahAudioException("EncoderExitFailure", "encoding");
            }
            var length = new FileInfo(outputPath).Length;
            if (length is < 4 or > DvarTorahAudioValidation.MaximumMp3Bytes)
            {
                throw new DvarTorahAudioException("EncodedAudioSizeInvalid", "encoding");
            }
            return await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            File.Delete(outputPath);
        }
    }
}
