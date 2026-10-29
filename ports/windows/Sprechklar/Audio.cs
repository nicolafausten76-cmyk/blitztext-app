using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NAudio.Wave;
namespace Sprechklar;

// No raw recording is written to disk. The child engine receives a WAV over stdin.
public sealed class Recorder
{
    private WaveInEvent? input;
    private WaveFileWriter? writer;
    private MemoryStream? buffer;
    private TaskCompletionSource<byte[]>? completion;
    private readonly object gate = new();
    public void Start(int device)
    {
        lock (gate)
        {
            if (input != null) throw new InvalidOperationException("Aufnahme läuft bereits.");
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                buffer = new MemoryStream();
                input = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
                writer = new WaveFileWriter(buffer, input.WaveFormat);
                input.DataAvailable += (_,e) => { lock (gate) writer?.Write(e.Buffer, 0, e.BytesRecorded); };
                input.RecordingStopped += (_,e) =>
                {
                    lock (gate)
                    {
                        try
                        {
                            writer?.Dispose(); writer = null; // Finalize the WAV header.
                            input?.Dispose(); input = null;
                            if (e.Exception != null) completion.TrySetException(e.Exception);
                            else completion.TrySetResult(buffer!.ToArray());
                        }
                        catch (Exception error) { completion.TrySetException(error); }
                        finally { ClearBuffer(); }
                    }
                };
                input.StartRecording();
            }
            catch
            {
                writer?.Dispose(); writer = null; input?.Dispose(); input = null;
                ClearBuffer(); throw;
            }
        }
    }
    private void ClearBuffer()
    {
        if (buffer != null)
        {
            if (buffer.TryGetBuffer(out var memory)) CryptographicOperations.ZeroMemory(memory.AsSpan());
            buffer.Dispose(); buffer = null;
        }
    }
    public Task<byte[]> Stop()
    {
        lock (gate)
        {
            if (completion == null) throw new InvalidOperationException("Keine Aufnahme aktiv.");
            input?.StopRecording(); return completion.Task;
        }
    }
}
