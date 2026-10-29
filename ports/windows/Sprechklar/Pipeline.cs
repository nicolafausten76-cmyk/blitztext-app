using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
namespace Sprechklar;

public static class Pipeline
{
    public static string Engine => Path.Combine(AppContext.BaseDirectory, "engine", "whisper-cli.exe");
    public static async Task DownloadModel(string model, IProgress<string> progress, CancellationToken ct)
    {
        model = model == "tiny" ? "tiny" : "base";
        string expected = model == "tiny" ? "bd577a113a864445d4c299885e0cb97d4ba92b5f" : "465707469ff3a37a2b9b8d8f89f2f99de7299dac";
        string dest = Storage.ModelPath(model); Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        string tmp = dest + "." + Guid.NewGuid() + ".part";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            using var response = await client.GetAsync("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-" + model + ".bin", HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            await using (var output = File.Create(tmp))
            await using (var source = await response.Content.ReadAsStreamAsync(ct))
            {
                byte[] buffer = new byte[81920]; long total = 0; int count;
                while ((count = await source.ReadAsync(buffer, ct)) != 0)
                {
                    total += count;
                    if (total > 170_000_000) throw new IOException("Modell ist unerwartet groß.");
                    await output.WriteAsync(buffer.AsMemory(0, count), ct);
                    progress.Report($"Modell laden: {total / 1024 / 1024} MB");
                }
            }
            // Checksum as published by whisper.cpp. Detects changed/corrupt content;
            // SHA-1 is not a modern authenticity guarantee. Download transport is HTTPS.
            await using (var file = File.OpenRead(tmp))
            {
                string actual = Convert.ToHexString(await SHA1.HashDataAsync(file, ct)).ToLowerInvariant();
                if (actual != expected) throw new IOException("Modell-Prüfsumme stimmt nicht. Download verworfen.");
            }
            File.Move(tmp, dest, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
    public static async Task<string> Transcribe(byte[] wav, Settings config, CancellationToken ct)
    {
        if (!File.Exists(Engine)) throw new IOException("Spracherkennungs-Engine fehlt im Unterordner engine.");
        if (!File.Exists(Storage.ModelPath(config.Model))) throw new IOException("Bitte zuerst das Sprachmodell laden.");
        using (var reader = new WaveFileReader(new MemoryStream(wav, false)))
        {
            if (reader.TotalTime.TotalSeconds < 0.3) return "";
            if (reader.WaveFormat.SampleRate != 16000 || reader.WaveFormat.Channels != 1 || reader.WaveFormat.BitsPerSample != 16) throw new IOException("Ungültiges Aufnahmeformat.");
            byte[] pcm = new byte[8192]; int n; double energy = 0; long sampleCount = 0;
            while ((n = reader.Read(pcm, 0, pcm.Length)) > 0)
                for (int i=0; i+1<n; i+=2) { double s=(short)(pcm[i] | pcm[i+1]<<8)/32768.0; energy+=s*s; sampleCount++; }
            if (sampleCount == 0 || Math.Sqrt(energy/sampleCount) < 0.001) return "";
            if (reader.TotalTime.TotalMinutes > 5) throw new IOException("Diese erste Version verarbeitet maximal fünf Minuten pro Aufnahme.");
        }
        var psi = new ProcessStartInfo(Engine)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        // With stdin ("-"), this pinned CLI also defaults its output basename to "-".
        // That suppresses segment callbacks. A non-stdout basename enables stdout callbacks;
        // no output-file flags are set, so no transcript files are created.
        foreach (string arg in new[] { "-m", Storage.ModelPath(config.Model), "-f", "-", "-l",
            config.Language == "auto" ? "auto" : "de", "-t", "4", "-nt", "-np", "-of", "sprechklar-memory" }) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi) ?? throw new IOException("Engine konnte nicht starten.");
        using var registration = ct.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(wav, ct);
            process.StandardInput.Close();
            await process.WaitForExitAsync(ct);
            string transcript = await stdout;
            await stderr;
            ct.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new IOException("Lokale Spracherkennung fehlgeschlagen. Exit-Code: " + process.ExitCode);
            return Core.ReplaceTerms(Core.Clean(transcript), config.Dictionary);
        }
        finally
        {
            // Includes pipe failures and cancellation: never leave a speech engine running.
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
        }
    }
    public static async Task<string> Rewrite(string text, int mode, Settings config, CancellationToken ct)
    {
        if (mode == 0 || text.Length == 0) return text;
        if (string.IsNullOrWhiteSpace(config.OllamaModel)) throw new InvalidOperationException("Für Schreibmodi ein installiertes Ollama-Modell eintragen. Das wortgetreue Transkript bleibt erhalten.");
        if (config.OllamaModel.Contains("cloud", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Bitte ein lokales Ollama-Modell wählen. Cloud-Modelle sind hier gesperrt.");
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        using (var info = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/show", new { model = config.OllamaModel.Trim() }, ct))
        {
            info.EnsureSuccessStatusCode();
            using var metadata = JsonDocument.Parse(await info.Content.ReadAsStringAsync(ct));
            foreach (string name in new[] { "remote_model", "remote_host" })
                if (metadata.RootElement.TryGetProperty(name, out var remote) && remote.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(remote.GetString()))
                    throw new InvalidOperationException("Dieses Ollama-Modell wird extern ausgeführt. Bitte lokales Modell wählen.");
        }
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/chat", new
        {
            model = config.OllamaModel.Trim(), stream = false,
            messages = new[] { new { role = "system", content = Core.Instruction(mode) }, new { role = "user", content = text } },
            options = new { temperature = 0, num_ctx = 2048 }
        }, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        string result = doc.RootElement.GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
        if (result.Length == 0) throw new IOException("Das Textmodell hat keine Antwort geliefert. Original bleibt erhalten.");
        return result;
    }
}
