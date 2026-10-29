using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
namespace Sprechklar;

public sealed class Recorder
{
    private WaveInEvent? input;
    private WaveFileWriter? writer;
    private string? path;
    private TaskCompletionSource<string>? completion;
    private readonly object gate = new();
    public void Start(int device)
    {
        lock (gate)
        {
            if (input != null) throw new InvalidOperationException("Aufnahme läuft bereits.");
            path = Path.Combine(Path.GetTempPath(), "sprechklart-" + Guid.NewGuid() + ".wav");
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                input = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
                writer = new WaveFileWriter(path, input.WaveFormat);
                input.DataAvailable += (_,e) => { lock (gate) writer?.Write(e.Buffer, 0, e.BytesRecorded); };
                input.RecordingStopped += (_,e) =>
                {
                    lock (gate)
                    {
                        writer?.Dispose(); writer = null;
                        input?.Dispose(); input = null;
                        if (e.Exception != null)
                        {
                            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
                            completion.TrySetException(e.Exception);
                        }
                        else completion.TrySetResult(path!);
                    }
                };
                input.StartRecording();
            }
            catch
            {
                writer?.Dispose(); writer = null; input?.Dispose(); input = null;
                if (File.Exists(path)) File.Delete(path); throw;
            }
        }
    }
    public Task<string> Stop()
    {
        lock (gate)
        {
            if (completion == null) throw new InvalidOperationException("Keine Aufnahme aktiv.");
            input?.StopRecording(); return completion.Task;
        }
    }
}
