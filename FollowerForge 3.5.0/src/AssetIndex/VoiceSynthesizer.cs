using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Serilog;

namespace FollowerForge.AssetIndex;

/// <summary>
/// Speaks a line in a Skyrim voice and packs it into the .fuz the game expects, using the
/// user's own xVASynth install.
///
/// The call sequence mirrors Mantella's xVASynth client (src/tts/xvasynth.py), which drives this
/// same server fast enough for live in-game conversation — so where our first attempt disagreed
/// with it, Mantella won:
///  - address 127.0.0.1, never "localhost" (::1 first = stalled connections);
///  - liveness is a plain GET to "/", not a POST to /checkReady with a guessed body;
///  - the server is resources/app/cpython_cpu/server.exe launched with cwd at the xVASynth ROOT;
///  - /synthesize must carry the model's own base_emb, or v3 models return unusable audio;
///  - the lip/fuz chain is FaceFXWrapper -> xWMAEncode -> fuz_extractor, run from the lip_fuz
///    folder with a relative FonixData.cdf.
/// </summary>
public sealed class VoiceSynthesizer(VoiceModelCatalog catalog, ILogger log) : IDisposable
{
    // 127.0.0.1, never "localhost": on Windows that resolves to ::1 first and the connection
    // stalls instead of failing fast. Mantella, which drives this same server in real time,
    // hardcodes 127.0.0.1 for the same reason.
    private const string Base = "http://127.0.0.1:8008";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };

    private Process? _server;
    private string? _loadedModelId;

    public sealed record LineResult(bool Success, string? FuzPath, string? Error);

    /// <summary>True when this machine can synthesise and package a line at all.</summary>
    public bool Available => catalog.Installed && catalog.CanMakeFuz && File.Exists(catalog.ServerExe);

    /// <summary>Starts xVASynth's server if it is not already answering, and waits for it.</summary>
    public async Task<bool> EnsureServerAsync(CancellationToken cancel = default)
    {
        if (await PingAsync(cancel)) return true;
        if (!File.Exists(catalog.ServerExe))
        {
            log.Warning("xVASynth server not found at {Path}", catalog.ServerExe);
            return false;
        }

        log.Information("Starting xVASynth server…");
        _server = Process.Start(new ProcessStartInfo
        {
            FileName = catalog.ServerExe,
            // Without this the server dies immediately looking for ./javascript/script.js.
            WorkingDirectory = catalog.ServerWorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });

        for (var i = 0; i < 60; i++)
        {
            if (cancel.IsCancellationRequested) return false;
            await Task.Delay(1000, cancel);
            if (await PingAsync(cancel)) { log.Information("xVASynth ready after {Seconds}s", i + 1); return true; }
            if (_server is { HasExited: true })
            {
                log.Error("xVASynth server exited: {Error}", await _server.StandardError.ReadToEndAsync(cancel));
                return false;
            }
        }
        log.Error("xVASynth server did not become ready in time");
        return false;
    }

    /// <summary>
    /// A plain GET to the root is how Mantella probes the server. Posting to /checkReady with a
    /// guessed payload throws server-side and tells you nothing about liveness.
    /// </summary>
    private static async Task<bool> PingAsync(CancellationToken cancel)
    {
        try
        {
            using var quick = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancel, quick.Token);
            using var res = await Http.GetAsync(Base + "/", linked.Token);
            return true;
        }
        catch (Exception ex) when (IsAbruptClose(ex))
        {
            // The server hangs up without framing a reply. Mantella treats exactly this as proof
            // it is alive; only a refused connection means nothing is listening.
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// xVASynth's minimal HTTP server frequently closes the socket instead of finishing the
    /// response. Python's requests shrugs this off, .NET raises — so we distinguish "server hung
    /// up after taking the request" (fine, verify by side effect) from "nothing is listening".
    /// </summary>
    private static bool IsAbruptClose(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is HttpIOException) return true;
            if (e is System.Net.Sockets.SocketException s)
                return s.SocketErrorCode is not System.Net.Sockets.SocketError.ConnectionRefused
                    and not System.Net.Sockets.SocketError.HostUnreachable;
        }
        return false;
    }

    /// <summary>
    /// Posts JSON, tolerating the server's habit of closing the connection early.
    ///
    /// The body MUST be sent with a Content-Length. JsonContent streams chunked, and the server
    /// does `int(self.headers['Content-Length'])` unconditionally — with no such header it throws
    /// before reading a byte, so every request silently did nothing. StringContent buffers, so
    /// the length is known and set.
    /// </summary>
    private static async Task<bool> PostAsync(string path, object payload, CancellationToken cancel)
    {
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        try
        {
            using var res = await Http.PostAsync(Base + path, content, cancel);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex) when (IsAbruptClose(ex))
        {
            return true;   // request was delivered; the caller verifies the actual result
        }
    }

    private async Task<bool> LoadModelAsync(VoiceModel model, CancellationToken cancel)
    {
        if (_loadedModelId == model.ModelId) return true;
        // Field set copied from Mantella's working call; "model" is the path WITHOUT extension.
        var payload = new Dictionary<string, object?>
        {
            ["outputs"] = null,
            ["version"] = "3.0",
            ["model"] = model.CheckpointPath[..^3],
            ["modelType"] = model.ModelType,
            ["base_lang"] = "en",
            ["pluginsContext"] = "{}",                    // server json-decodes this string
        };
        if (!await PostAsync("/loadModel", payload, cancel))
        {
            log.Error("loadModel failed for {Model}", model.ModelId);
            return false;
        }
        _loadedModelId = model.ModelId;
        return true;
    }

    /// <summary>Synthesises one line and packs it to <paramref name="fuzPath"/>.</summary>
    public async Task<LineResult> SpeakAsync(string voiceTypeEditorId, string text, string fuzPath,
        CancellationToken cancel = default)
    {
        if (catalog.ForVoiceType(voiceTypeEditorId) is not { } model)
            return new LineResult(false, null, $"No xVASynth model for voice '{voiceTypeEditorId}'.");
        if (!catalog.CanMakeFuz)
            return new LineResult(false, null, "xVASynth's lip_fuz tools are missing; lines would be silent.");
        if (!await EnsureServerAsync(cancel))
            return new LineResult(false, null, "Could not start the xVASynth server.");
        if (!await LoadModelAsync(model, cancel))
            return new LineResult(false, null, $"xVASynth could not load model {model.ModelId}.");

        var work = Path.Combine(Path.GetTempPath(), "FollowerForge", "voice", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var wav = Path.Combine(work, "line.wav");

        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["pluginsContext"] = "{}",
                ["modelType"] = model.ModelType,
                ["sequence"] = text,
                ["pace"] = 1.0,
                ["outfile"] = wav,
                ["vocoder"] = "n/a",     // only "waveglow*" triggers a vocoder load server-side
                ["base_lang"] = "en",
                // Without the model's own speaker embedding a v3 model yields unusable audio.
                ["base_emb"] = model.BaseSpeakerEmb,
                ["useSR"] = false,
                ["useCleanup"] = false,
            };
            if (!await PostAsync("/synthesize", payload, cancel))
                return new LineResult(false, null, "xVASynth refused the synthesis request.");
            // The only trustworthy signal is the file itself, since the server may not reply.
            if (!File.Exists(wav))
                return new LineResult(false, null, "xVASynth produced no audio for this line.");

            Directory.CreateDirectory(Path.GetDirectoryName(fuzPath)!);
            var packed = PackFuz(wav, text, fuzPath);
            return packed is null
                ? new LineResult(true, fuzPath, null)
                : new LineResult(false, null, packed);
        }
        catch (Exception ex)
        {
            return new LineResult(false, null, ex.Message);
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// WAV -> .lip (FaceFXWrapper) -> .xwm (xWMAEncode) -> .fuz (fuz_extractor). The .lip is what
    /// moves the mouth; without it the follower talks with a closed face.
    /// </summary>
    private string? PackFuz(string wavPath, string text, string fuzPath)
    {
        var lip = Path.ChangeExtension(wavPath, ".lip");
        var resampled = Path.ChangeExtension(wavPath, ".resampled.wav");
        var xwm = Path.ChangeExtension(wavPath, ".xwm");

        // Run from the lip_fuz folder with a relative FonixData.cdf, matching the command
        // Mantella actually emits. We do NOT write a .bat into the user's xVASynth install.
        var lipRun = Run(catalog.FaceFxWrapper,
            $"Skyrim USEnglish FonixData.cdf \"{wavPath}\" \"{resampled}\" \"{lip}\" \"{LipText(text)}\"",
            catalog.LipFuzDir);
        if (lipRun is not null) return "lip generation failed: " + lipRun;
        if (!File.Exists(lip)) return "FaceFXWrapper produced no .lip (mouth would not move).";

        var xwmRun = Run(catalog.XwmaEncode, $"-b 160000 \"{wavPath}\" \"{xwm}\"");
        if (xwmRun is not null) return "xwm encoding failed: " + xwmRun;

        var fuzRun = Run(catalog.FuzExtractor, $"-c \"{fuzPath}\" \"{lip}\" \"{xwm}\"");
        if (fuzRun is not null) return "fuz packing failed: " + fuzRun;
        return File.Exists(fuzPath) ? null : "fuz_extractor produced no file.";
    }

    /// <summary>
    /// Letters and spaces only, which is exactly what xVASynth's own lip_fuz plugin feeds
    /// FaceFXWrapper (it does re.sub(r'[^a-zA-Z\s]+', '', text)). Punctuation left in produces
    /// worse phoneme timing, and a quote or backslash would break the command line outright.
    /// The subtitle keeps the user's original text — only the lipsync input is stripped.
    /// </summary>
    private static string LipText(string text)
    {
        var clean = new string(text.Select(c => char.IsAsciiLetter(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());
        return string.Join(' ', clean.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Runs a tool; returns null on success or a short error for the report.</summary>
    private string? Run(string exe, string args, string? workingDirectory = null)
    {
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        if (p is null) return $"could not start {Path.GetFileName(exe)}";
        var stderr = p.StandardError.ReadToEnd();
        p.StandardOutput.ReadToEnd();
        p.WaitForExit(120_000);
        return p.ExitCode == 0 ? null : $"{Path.GetFileName(exe)} exit {p.ExitCode} {stderr.Trim()}";
    }

    /// <summary>Stops only a server this instance started; a user's own session is left alone.</summary>
    public void Dispose()
    {
        if (_server is null || _server.HasExited) return;
        try
        {
            PostAsync("/stopServer", new { }, CancellationToken.None).Wait(5000);
            if (!_server.WaitForExit(5000)) _server.Kill(entireProcessTree: true);
        }
        catch (Exception) { try { _server.Kill(entireProcessTree: true); } catch (Exception) { } }
        _server.Dispose();
    }
}
