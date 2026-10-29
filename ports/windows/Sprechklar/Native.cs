using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
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
    public static bool IsPassword()
    {
        try { return AutomationElement.FocusedElement?.Current.IsPassword == true; }
        catch { return true; }
    }
    public static string? FieldIdentity()
    {
        try
        {
            var field = AutomationElement.FocusedElement;
            if (field == null || field.Current.IsPassword || !field.Current.HasKeyboardFocus) return null;
            return string.Join(",", field.GetRuntimeId());
        }
        catch { return null; }
    }
    internal static DataObject ProtectedText(string text)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text, false);
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]), false);
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]), false);
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[] { 1, 0, 0, 0 }), false);
        return data;
    }
    public static void CopyProtected(string text) => Clipboard.SetDataObject(ProtectedText(text), true);
    public static void ClipboardSmokeTest()
    {
        // CI's isolated desktop only; no user clipboard is examined.
        CopyProtected("Sprechklar security smoke test");
        if (Clipboard.GetText() != "Sprechklar security smoke test") throw new InvalidOperationException("Clipboard round-trip failed.");
        foreach (string format in new[] { "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard" })
        {
            if (Clipboard.GetData(format) is not MemoryStream stream || stream.ToArray().Length != 4 || BitConverter.ToInt32(stream.ToArray(), 0) != 0)
                throw new InvalidOperationException("Clipboard exclusion marker missing: " + format);
        }
        if (!Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing"))
            throw new InvalidOperationException("Clipboard monitor exclusion missing.");
        Clipboard.Clear();
    }
    public static async Task<bool> Paste(string text, IntPtr target, string? fieldIdentity)
    {
        if (target == IntPtr.Zero || fieldIdentity == null || GetForegroundWindow() != target || IsPassword() || FieldIdentity() != fieldIdentity) return false;
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
        try { CopyProtected(text); } catch (ExternalException) { return false; }
        uint sequence = GetClipboardSequenceNumber();
        bool sent = false;
        try
        {
            if (GetForegroundWindow() != target || IsPassword() || FieldIdentity() != fieldIdentity) return false;
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
