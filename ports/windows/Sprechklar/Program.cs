using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;
using Forms = System.Windows.Forms;
namespace Sprechklar;

internal static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--transcription-test")
        {
            try
            {
                byte[] sample = File.ReadAllBytes(args[1]);
                try
                {
                    string text = Pipeline.Transcribe(sample, new Settings { Language="auto", Dictionary="" }, CancellationToken.None).GetAwaiter().GetResult();
                    if (!text.Contains("country", StringComparison.OrdinalIgnoreCase)) throw new Exception("Reference transcription failed.");
                }
                finally { CryptographicOperations.ZeroMemory(sample); }
            }
            catch { Environment.ExitCode=1; }
            return;
        }
        var app = new System.Windows.Application();
        if (args.Length == 1 && args[0] == "--security-smoke-test")
        {
            try { Native.ClipboardSmokeTest(); }
            catch { Environment.ExitCode=1; }
            return;
        }
        app.DispatcherUnhandledException += (_,e) => { MessageBox.Show(e.Exception.Message, "Sprechklar"); e.Handled=true; };
        var window = new AppWindow();
        if (args.Length == 1 && args[0] == "--smoke-test")
            window.Loaded += (_,_) => { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.ok"), "OK"); window.Close(); };
        app.Run(window);
    }
}
public sealed class AppWindow : Window
{
    readonly Settings config = Storage.Load();
    readonly Recorder recorder = new();
    readonly OutputReview review = new();
    readonly ComboBox mode = new() { ItemsSource=Core.Modes, SelectedIndex=0 };
    readonly ComboBox model = new() { ItemsSource=new[]{"base","tiny"} };
    readonly ComboBox language = new() { ItemsSource=new[]{"de","auto"} };
    readonly ComboBox device = new();
    readonly TextBox dictionary = new() { AcceptsReturn=true, Height=65, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
    readonly TextBox ollama = new();
    readonly CheckBox history = new() { Content="Verlauf verschlüsselt speichern (max. 100 Ergebnisse)" };
    readonly TextBox result = new() { AcceptsReturn=true, TextWrapping=TextWrapping.Wrap, Height=150, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
    readonly TextBlock status = new() { Text="Offline bereit · zuerst Sprachmodell laden", TextWrapping=TextWrapping.Wrap };
    readonly Button recordButton = new() { Content="Aufnahme starten", Padding=new Thickness(14,9,14,9) };
    readonly Stopwatch elapsed = new();
    readonly DispatcherTimer limit = new() { Interval=TimeSpan.FromSeconds(1) };
    Forms.NotifyIcon? tray;
    HwndSource? source;
    IntPtr handle, target;
    string? targetField;
    CancellationTokenSource? operation;
    bool recording, busy, hotkeys;
    public AppWindow()
    {
        Title="Sprechklar · lokale Spracheingabe"; Width=650; Height=900; MinWidth=480; MinHeight=640;
        Background=new SolidColorBrush(Color.FromRgb(247,247,252));
        var stack=new StackPanel { Margin=new Thickness(24) };
        Content=new ScrollViewer { Content=stack, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        stack.Children.Add(new TextBlock { Text="Sprechklar", FontSize=30, FontWeight=FontWeights.Bold });
        stack.Children.Add(new TextBlock { Text="Deine Stimme bleibt auf diesem PC.", Margin=new Thickness(0,0,0,16) });
        Add(stack,"Schreibmodus",mode); Add(stack,"Sprachmodell · base: genauer / tiny: schneller",model);
        Add(stack,"Sprache · de: Deutsch / auto: automatisch",language);
        for (int i=0;i<WaveIn.DeviceCount;i++) device.Items.Add(WaveIn.GetCapabilities(i).ProductName);
        device.SelectedIndex=Math.Clamp(config.Device,0,Math.Max(0,device.Items.Count-1));
        model.SelectedItem=config.Model=="tiny"?"tiny":"base"; language.SelectedItem=config.Language=="auto"?"auto":"de";
        dictionary.Text=config.Dictionary; ollama.Text=config.OllamaModel; history.IsChecked=config.SaveHistory;
        Add(stack,"Mikrofon",device);
        var download=Button("Sprachmodell einmalig laden",async()=> await Download());
        stack.Children.Add(download);
        Add(stack,"Wörterbuch · eine Regel je Zeile: gesprochen => geschrieben",dictionary);
        Add(stack,"Optionales lokales Ollama-Modell · muss bereits installiert sein",ollama);
        stack.Children.Add(history);
        recordButton.Click+=async(_,_)=> await Toggle(IntPtr.Zero);
        stack.Children.Add(recordButton);
        stack.Children.Add(Button("Abbrechen",async()=> await Cancel()));
        stack.Children.Add(new TextBlock { Text="Strg + Umschalt + Leertaste: Start / Stopp in anderen Programmen\nStrg + Alt + Rücktaste: Abbrechen · maximal 5 Minuten je Aufnahme", TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,12,0,12) });
        Add(stack,"Ergebnis",result);
        stack.Children.Add(Button("Ergebnis kopieren",()=> { if(result.Text.Length>0) Native.CopyProtected(result.Text); return Task.CompletedTask; }));
        stack.Children.Add(Button("Text für ursprüngliches Zielfeld freigeben",()=>
        {
            review.Approve(result.Text,target,targetField,DateTimeOffset.UtcNow);
            status.Text="Text freigegeben für 60 Sekunden. Zum ursprünglichen Feld wechseln und Strg + Alt + Eingabe drücken. Änderungen widerrufen die Freigabe.";
            return Task.CompletedTask;
        }));
        result.TextChanged+=(_,_)=>review.Invalidate();
        stack.Children.Add(new TextBlock { Text="Ergebnis vor dem Kopieren oder Einfügen prüfen. Auch lokale KI kann Fakten ändern oder eingesprochene Anweisungen befolgen. Keine ungeprüften Befehle in ein Terminal einfügen.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8) });
        stack.Children.Add(Button("Verlauf ansehen",()=> { ShowHistory(); return Task.CompletedTask; }));
        stack.Children.Add(Button("Im Hintergrund weiterlaufen",()=> { Save(); Hide(); return Task.CompletedTask; }));
        stack.Children.Add(status);
        SourceInitialized+=(_,_)=>InitNative();
        Closing+=(_,e)=> { if(recording||busy){e.Cancel=true;status.Text="Bitte zuerst abbrechen und den Abschluss abwarten.";return;} Save(); };
        Closed+=(_,_)=> { limit.Stop(); Native.UnregisterHotKey(handle,1);Native.UnregisterHotKey(handle,2);Native.UnregisterHotKey(handle,3);source?.RemoveHook(Hook);tray?.Dispose();operation?.Dispose(); };
        limit.Tick+=async(_,_)=> { if(recording && elapsed.Elapsed.TotalMinutes>=5) await Toggle(IntPtr.Zero); };
    }
    static void Add(Panel panel,string label,Control control)
    {
        panel.Children.Add(new TextBlock { Text=label, Margin=new Thickness(0,12,0,4) });
        control.Margin=new Thickness(0,0,0,5);panel.Children.Add(control);
    }
    static Button Button(string label,Func<Task> action)
    {
        var b=new Button { Content=label, Margin=new Thickness(0,4,0,4), Padding=new Thickness(8) };
        b.Click+=async(_,_)=>{try{await action();}catch(Exception e){MessageBox.Show(e.Message,"Sprechklar");}};
        return b;
    }
    void Save()
    {
        if(recording || busy) return;
        config.Model=model.SelectedItem?.ToString()??"base";config.Language=language.SelectedItem?.ToString()??"de";
        config.Device=Math.Max(0,device.SelectedIndex);config.Dictionary=dictionary.Text;
        config.OllamaModel=ollama.Text;config.SaveHistory=history.IsChecked==true;Storage.Save(config);
    }
    void InitNative()
    {
        handle=new WindowInteropHelper(this).Handle;source=HwndSource.FromHwnd(handle);source.AddHook(Hook);
        hotkeys=Native.RegisterHotKey(handle,1,0x4006,0x20);
        bool cancelHotkey=Native.RegisterHotKey(handle,2,0x4003,0x08);
        bool insertHotkey=Native.RegisterHotKey(handle,3,0x4003,0x0D);
        if(!hotkeys || !cancelHotkey || !insertHotkey) status.Text="Tastenkürzel belegt. Aufnahme über den Button möglich.";
        tray=new Forms.NotifyIcon { Icon=System.Drawing.SystemIcons.Application,Text="Sprechklar",Visible=true };
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("Öffnen",null,(_,_)=>Dispatcher.Invoke(()=>{Show();Activate();}));
        menu.Items.Add("Beenden",null,(_,_)=>Dispatcher.Invoke(Close));
        tray.ContextMenuStrip=menu;
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>{Show();Activate();});
    }
    IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message==0x312)
        {
            handled=true;
            if(wParam.ToInt32()==1) Dispatcher.InvokeAsync(async()=>await Toggle(Native.GetForegroundWindow()));
            if(wParam.ToInt32()==2) Dispatcher.InvokeAsync(async()=>await Cancel());
            if(wParam.ToInt32()==3) Dispatcher.InvokeAsync(async()=>await InsertApproved());
        }
        return IntPtr.Zero;
    }
    async Task Download()
    {
        if(busy||recording)return;
        Save();busy=true;operation=new();
        try { await Pipeline.DownloadModel(config.Model,new Progress<string>(s=>status.Text=s),operation.Token);status.Text="Modell geprüft und gespeichert. Offline bereit."; }
        catch(OperationCanceledException){status.Text="Download abgebrochen.";}
        catch(Exception e){status.Text=e.Message;}
        finally{busy=false;operation.Dispose();operation=null;}
    }
    async Task Toggle(IntPtr foreground)
    {
        if(busy)return;
        if(!recording)
        {
            try
            {
                review.Invalidate();
                Save();
                if(!File.Exists(Pipeline.Engine)||!File.Exists(Storage.ModelPath(config.Model)))throw new IOException("Engine oder Sprachmodell fehlt. Bitte Einrichtung abschließen.");
                if(device.Items.Count==0)throw new IOException("Kein Mikrofon gefunden.");
                target=foreground==handle?IntPtr.Zero:foreground;
                if(target!=IntPtr.Zero && Native.IsPassword()) throw new InvalidOperationException("Keine Spracheingabe in Passwortfeldern.");
                targetField=target==IntPtr.Zero?null:Native.FieldIdentity();
                recorder.Start(config.Device);recording=true;elapsed.Restart();limit.Start();
                recordButton.Content="Aufnahme stoppen";status.Text="Aufnahme läuft …";
                mode.IsEnabled=model.IsEnabled=language.IsEnabled=device.IsEnabled=false;
            }
            catch(Exception e){status.Text=e.Message;}
            return;
        }
        recording=false;busy=true;limit.Stop();elapsed.Stop();recordButton.Content="Aufnahme starten";
        operation=new();byte[]? wav=null;int selectedMode=mode.SelectedIndex;
        try
        {
            status.Text="Lokale Spracherkennung …";
            wav=await recorder.Stop();
            string original=await Pipeline.Transcribe(wav,config,operation.Token);
            result.Text=original;
            if(original.Length==0){status.Text="Keine Sprache erkannt.";return;}
            string text=original;
            try { text=await Pipeline.Rewrite(original,selectedMode,config,operation.Token); }
            catch(OperationCanceledException){throw;}
            catch(Exception e){status.Text=e.Message;return;}
            operation.Token.ThrowIfCancellationRequested();result.Text=text;
            review.Invalidate();
            status.Text=target==IntPtr.Zero?"Fertig. Text prüfen und bei Bedarf kopieren.":"Fertig. Text prüfen und ausdrücklich freigeben; anschließend im ursprünglichen Feld Strg + Alt + Eingabe.";
            Show(); Activate();
            if(config.SaveHistory)
            {
                try{Storage.AddHistory(new(DateTimeOffset.Now,Core.Modes[selectedMode],text));}
                catch(Exception){status.Text+=" Verlauf konnte nicht gespeichert werden.";}
            }
        }
        catch(OperationCanceledException){status.Text="Abgebrochen. Bereits erkanntes Transkript bleibt im Ergebnisfeld.";}
        catch(Exception e){status.Text=e.Message;}
        finally
        {
            if(wav!=null) CryptographicOperations.ZeroMemory(wav);
            busy=false;operation?.Dispose();operation=null;
            mode.IsEnabled=model.IsEnabled=language.IsEnabled=device.IsEnabled=true;
        }
    }
    async Task Cancel()
    {
        review.Invalidate();
        if(recording)
        {
            recording=false;busy=true;limit.Stop();elapsed.Stop();
            try { byte[] wav=await recorder.Stop();CryptographicOperations.ZeroMemory(wav);status.Text="Aufnahme verworfen."; }
            catch(Exception e){status.Text=e.Message;}
            finally{busy=false;recordButton.Content="Aufnahme starten";mode.IsEnabled=model.IsEnabled=language.IsEnabled=device.IsEnabled=true;}
        }
        else operation?.Cancel();
    }
    async Task InsertApproved()
    {
        if(recording||busy) return;
        var value=review.Take(result.Text,DateTimeOffset.UtcNow);
        if(value==null){status.Text="Keine gültige Freigabe. Ergebnis zuerst in Sprechklar prüfen und freigeben.";return;}
        busy=true;
        try
        {
            bool pasted=await Native.Paste(value.Text,value.Target,value.Field);
            status.Text=pasted?"Freigegebenen Text eingefügt; Annahme im Zielprogramm prüfen.":"Zielfeld geändert oder nicht aktiv. Erneute Prüfung und Freigabe nötig.";
        }
        catch(Exception e){status.Text=e.Message;}
        finally{busy=false;}
    }
    void ShowHistory()
    {
        var items=Storage.History();var panel=new StackPanel { Margin=new Thickness(16) };
        var window=new Window { Title="Sprechklar · verschlüsselter Verlauf",Width=560,Height=500,Owner=this,Content=new ScrollViewer { Content=panel } };
        panel.Children.Add(Button("Gesamten Verlauf löschen",()=>
        {
            if(MessageBox.Show("Gespeicherten Verlauf löschen?","Sprechklar",MessageBoxButton.YesNo)==MessageBoxResult.Yes){Storage.ClearHistory();window.Close();}
            return Task.CompletedTask;
        }));
        foreach(var item in items)panel.Children.Add(new TextBox { Text=$"{item.At:g} · {item.Mode}\n{item.Text}",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8) });
        window.ShowDialog();
    }
}
