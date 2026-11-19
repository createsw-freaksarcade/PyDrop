using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace PyDrop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new RunnerForm(args));
    }
}

internal sealed class RunnerForm : Form
{
    private const string RuntimeVersion = "3.12.10";
    private const string Bootstrap = "import os,runpy,sys; p=os.path.abspath(sys.argv[1]); sys.path.insert(0,os.path.dirname(p)); sys.path.insert(0,os.path.join(os.path.dirname(p),'packages')); sys.argv=sys.argv[1:]; runpy.run_path(p,run_name='__main__')";
    private readonly Label fileLabel = new();
    private readonly Label statusLabel = new();
    private readonly TextBox output = new();
    private readonly Button runButton = new();
    private readonly Button stopButton = new();
    private readonly string[] initialArgs;
    private Process? process;
    private string? selectedFile;

    public RunnerForm(string[] args)
    {
        initialArgs = args;
        Text = "PyDrop · Python 파일 실행기";
        ClientSize = new Size(820, 570);
        MinimumSize = new Size(620, 440);
        BackColor = Color.FromArgb(18, 23, 31);
        ForeColor = Color.FromArgb(230, 237, 245);
        Font = new Font("Malgun Gothic", 10);
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        var title = new Label { Text = "PyDrop", Font = new Font("Segoe UI", 26, FontStyle.Bold), ForeColor = Color.FromArgb(221, 235, 245), AutoSize = true, Location = new Point(30, 23) };
        var subtitle = new Label { Text = "Python 설치 없이 .py 실행  ·  Windows 64비트", AutoSize = true, ForeColor = Color.FromArgb(151, 169, 184), Location = new Point(34, 80) };
        fileLabel.Text = "여기에 .py 파일을 끌어다 놓거나 파일을 선택하세요";
        fileLabel.AutoEllipsis = true;
        fileLabel.TextAlign = ContentAlignment.MiddleLeft;
        fileLabel.BackColor = Color.FromArgb(29, 37, 48);
        fileLabel.BorderStyle = BorderStyle.FixedSingle;
        fileLabel.ForeColor = Color.FromArgb(199, 216, 228);
        fileLabel.SetBounds(30, 125, 760, 70);
        fileLabel.Padding = new Padding(16, 0, 16, 0);
        fileLabel.AllowDrop = true;
        fileLabel.DragEnter += OnDragEnter;
        fileLabel.DragDrop += OnDragDrop;

        var chooseButton = MakeButton("파일 선택", 30, 214, 145);
        chooseButton.Click += (_, _) => ChooseFile();
        runButton.Text = "실행";
        StyleButton(runButton, 188, 214, 145, true);
        runButton.Click += async (_, _) => await RunSelectedAsync();
        stopButton.Text = "중지";
        StyleButton(stopButton, 346, 214, 145, false);
        stopButton.Enabled = false;
        stopButton.Click += (_, _) => StopRunning();

        statusLabel.Text = "대기 중";
        statusLabel.AutoSize = true;
        statusLabel.ForeColor = Color.FromArgb(149, 174, 190);
        statusLabel.Location = new Point(32, 278);

        output.Multiline = true;
        output.ReadOnly = true;
        output.ScrollBars = ScrollBars.Both;
        output.WordWrap = false;
        output.BackColor = Color.FromArgb(10, 15, 21);
        output.ForeColor = Color.FromArgb(214, 228, 235);
        output.Font = new Font("Consolas", 10);
        output.BorderStyle = BorderStyle.FixedSingle;
        output.SetBounds(30, 307, 760, 214);
        output.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        var note = new Label { Text = "기본 라이브러리 포함  ·  pygame 등 외부 패키지는 스크립트 옆 packages 폴더에 별도 준비", AutoSize = true, ForeColor = Color.FromArgb(133, 149, 164), Location = new Point(32, 535), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        Controls.AddRange([title, subtitle, fileLabel, chooseButton, runButton, stopButton, statusLabel, output, note]);
        Resize += (_, _) => fileLabel.Width = ClientSize.Width - 60;
        Shown += async (_, _) =>
        {
            if (initialArgs.Length > 0 && SelectFile(initialArgs[0])) await RunSelectedAsync(initialArgs.Skip(1));
        };
        FormClosing += (_, _) => StopRunning();
    }

    private static Button MakeButton(string text, int x, int y, int width)
    {
        var button = new Button { Text = text };
        StyleButton(button, x, y, width, false);
        return button;
    }

    private static void StyleButton(Button button, int x, int y, int width, bool primary)
    {
        button.SetBounds(x, y, width, 42);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(93, 116, 135);
        button.BackColor = primary ? Color.FromArgb(144, 199, 222) : Color.FromArgb(31, 41, 53);
        button.ForeColor = primary ? Color.FromArgb(14, 25, 33) : Color.FromArgb(223, 233, 239);
        button.Cursor = Cursors.Hand;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        var paths = e.Data?.GetData(DataFormats.FileDrop) as string[];
        e.Effect = paths?.Length == 1 && paths[0].EndsWith(".py", StringComparison.OrdinalIgnoreCase) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        var paths = e.Data?.GetData(DataFormats.FileDrop) as string[];
        if (paths?.Length == 1 && SelectFile(paths[0])) await RunSelectedAsync();
    }

    private void ChooseFile()
    {
        using var dialog = new OpenFileDialog { Filter = "Python 파일 (*.py)|*.py", CheckFileExists = true, Title = "실행할 Python 파일 선택" };
        if (dialog.ShowDialog(this) == DialogResult.OK) SelectFile(dialog.FileName);
    }

    private bool SelectFile(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path) || !path.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "존재하는 .py 파일을 선택하세요.", "파일 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        selectedFile = path;
        fileLabel.Text = path;
        return true;
    }

    private async Task RunSelectedAsync(IEnumerable<string>? scriptArgs = null)
    {
        if (process is { HasExited: false } || selectedFile is null) return;
        runButton.Enabled = false;
        stopButton.Enabled = false;
        output.Clear();
        try
        {
            statusLabel.Text = "내장 Python 준비 중…";
            var python = await Task.Run(EnsureRuntime);
            var info = new ProcessStartInfo(python)
            {
                WorkingDirectory = Path.GetDirectoryName(selectedFile)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            info.Environment["PYTHONIOENCODING"] = "utf-8";
            info.Environment["PYTHONUTF8"] = "1";
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(Bootstrap);
            info.ArgumentList.Add(selectedFile);
            foreach (var arg in scriptArgs ?? []) info.ArgumentList.Add(arg);
            process = new Process { StartInfo = info };
            if (!process.Start()) throw new InvalidOperationException("Python 프로세스를 시작하지 못했습니다.");
            stopButton.Enabled = true;
            statusLabel.Text = $"실행 중 · PID {process.Id}";
            var stdout = PumpAsync(process.StandardOutput);
            var stderr = PumpAsync(process.StandardError);
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            statusLabel.Text = process.ExitCode == 0 ? "완료 · 종료 코드 0" : $"종료 · 코드 {process.ExitCode}";
        }
        catch (Exception ex)
        {
            AppendOutput($"[실행 오류] {ex.Message}{Environment.NewLine}");
            statusLabel.Text = "실행 실패";
        }
        finally
        {
            process?.Dispose();
            process = null;
            runButton.Enabled = true;
            stopButton.Enabled = false;
        }
    }

    private async Task PumpAsync(StreamReader reader)
    {
        var buffer = new char[2048];
        int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
            AppendOutput(new string(buffer, 0, count));
    }

    private void AppendOutput(string text)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            if (output.TextLength > 200_000) output.Text = output.Text[^150_000..];
            output.AppendText(text);
        });
    }

    private void StopRunning()
    {
        try { if (process is { HasExited: false }) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }

    private static string EnsureRuntime()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PyDrop");
        var target = Path.Combine(root, $"python-{RuntimeVersion}-x64");
        var executable = Path.Combine(target, "python.exe");
        if (File.Exists(executable)) return executable;
        Directory.CreateDirectory(root);
        var temporary = Path.Combine(root, $"install-{Guid.NewGuid():N}");
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("python-3.12.10-embed-amd64.zip", StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException("내장 Python을 찾을 수 없습니다.");
            ZipFile.ExtractToDirectory(stream, temporary);
            if (!File.Exists(Path.Combine(temporary, "python.exe"))) throw new InvalidDataException("Python 런타임 파일이 누락됐습니다.");
            try { Directory.Move(temporary, target); }
            catch (IOException) when (File.Exists(executable)) { Directory.Delete(temporary, true); }
            return executable;
        }
        catch
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            throw;
        }
    }
}
