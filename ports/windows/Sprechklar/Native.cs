using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
namespace Sprechklar;

internal static class Native
{
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", SetLastError=true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public UNION data; }
    [StructLayout(LayoutKind.Explicit)] private struct UNION
    {
        [FieldOffset(0)] public KEYBOARD key;
        [FieldOffset(0)] public MOUSE mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBOARD { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSE { public int x,y; public uint data,flags,time; public IntPtr extra; }
    private static INPUT Key(ushort key, bool up=false) => new() { type=1, data=new UNION { key=new KEYBOARD { vk=key, flags=up?2u:0u } } };
    public static async Task<bool> Paste(string text, IntPtr target)
    {
        if (target == IntPtr.Zero || GetForegroundWindow() != target) return false;
        var snapshot = new DataObject();
        try
        {
            var previous = Clipboard.GetDataObject();
            if (previous != null)
                foreach (string format in previous.GetFormats(false))
                {
                    object? value = previous.GetData(format, false);
                    if (value != null) snapshot.SetData(format, value);
                }
        }
        catch { return false; } // Never overwrite a clipboard we cannot preserve.
        try { Clipboard.SetText(text); } catch (ExternalException) { return false; }
        uint sequence = GetClipboardSequenceNumber();
        bool sent = false;
        try
        {
            if (GetForegroundWindow() != target) return false;
            var keys = new[] { Key(0x11), Key(0x56), Key(0x56,true), Key(0x11,true) };
            sent = SendInput(4, keys, Marshal.SizeOf<INPUT>()) == 4;
            return sent; // OS accepted input; destination acceptance still needs device testing.
        }
        finally
        {
            // Windows has no destination-read acknowledgement. Conservative grace period;
            // do not restore over anything the user copied meanwhile.
            await Task.Delay(sent ? 1500 : 0);
            if (GetClipboardSequenceNumber() == sequence)
            {
                try
                {
                    if (snapshot.GetFormats(false).Length == 0) Clipboard.Clear();
                    else Clipboard.SetDataObject(snapshot, true);
                }
                catch (ExternalException) { }
            }
        }
    }
}
