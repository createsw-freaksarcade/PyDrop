using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
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
    private const string PipWheel = "pip-25.3-py3-none-any.whl";
    private const string Bootstrap = "import os,runpy,sys; p=os.path.abspath(sys.argv[1]); root=os.path.abspath(sys.argv[2]); deps=sys.argv[3]; sys.path[:0]=[os.path.dirname(p),root,os.path.join(root,'packages')]+([deps] if deps else []); sys.argv=[p]+sys.argv[4:]; runpy.run_path(p,run_name='__main__')";
    private readonly Label fileLabel = new();
    private readonly Label statusLabel = new();
    private readonly TextBox output = new();
    private readonly Button runButton = new();
    private readonly Button stopButton = new();
    private readonly string[] initialArgs;
    private Process? process;
    private string? selectedFile;
    private string? selectedProjectRoot;
    private bool busy;
    private bool stopRequested;

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
        fileLabel.Text = "여기에 .py 파일 또는 프로젝트 폴더를 끌어다 놓으세요";
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
        var folderButton = MakeButton("폴더 선택", 188, 214, 145);
        folderButton.Click += (_, _) => ChooseProject();
        runButton.Text = "실행";
        StyleButton(runButton, 346, 214, 145, true);
        runButton.Click += async (_, _) => await RunSelectedAsync();
        stopButton.Text = "중지";
        StyleButton(stopButton, 504, 214, 145, false);
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

        var note = new Label { Text = "프로젝트 파일 함께 사용  ·  requirements.txt 패키지는 첫 실행 때 자동 설치", AutoSize = true, ForeColor = Color.FromArgb(133, 149, 164), Location = new Point(32, 535), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        Controls.AddRange([title, subtitle, fileLabel, chooseButton, folderButton, runButton, stopButton, statusLabel, output, note]);
        Resize += (_, _) => fileLabel.Width = ClientSize.Width - 60;
        Shown += async (_, _) =>
        {
            if (initialArgs.Length > 0 && SelectPath(initialArgs[0])) await RunSelectedAsync(initialArgs.Skip(1));
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
        e.Effect = paths?.Length == 1 && (Directory.Exists(paths[0]) || paths[0].EndsWith(".py", StringComparison.OrdinalIgnoreCase)) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        var paths = e.Data?.GetData(DataFormats.FileDrop) as string[];
        if (paths?.Length == 1 && SelectPath(paths[0])) await RunSelectedAsync();
    }

    private void ChooseFile()
    {
        using var dialog = new OpenFileDialog { Filter = "Python 파일 (*.py)|*.py", CheckFileExists = true, Title = "실행할 Python 파일 선택" };
        if (dialog.ShowDialog(this) == DialogResult.OK) SelectFile(dialog.FileName);
    }

    private void ChooseProject()
    {
        using var dialog = new FolderBrowserDialog { Description = "실행할 Python 프로젝트 폴더 선택", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) SelectPath(dialog.SelectedPath);
    }

    private bool SelectPath(string path)
    {
        path = Path.GetFullPath(path);
        if (!Directory.Exists(path)) return SelectFile(path);
        var entry = new[] { "main.py", "app.py", "__main__.py" }
            .Select(name => Path.Combine(path, name)).FirstOrDefault(File.Exists);
        if (entry is null)
        {
            var scripts = Directory.GetFiles(path, "*.py", SearchOption.TopDirectoryOnly);
            if (scripts.Length == 1) entry = scripts[0];
        }
        if (entry is null)
        {
            MessageBox.Show(this, "폴더에서 main.py, app.py 또는 __main__.py를 찾지 못했습니다. 시작할 .py 파일을 직접 선택하세요.", "시작 파일 없음", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        selectedProjectRoot = path;
        selectedFile = entry;
        fileLabel.Text = $"{path}  →  {Path.GetFileName(entry)}";
        return true;
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
        selectedProjectRoot = Path.GetDirectoryName(path)!;
        fileLabel.Text = path;
        return true;
    }

    private async Task RunSelectedAsync(IEnumerable<string>? scriptArgs = null)
    {
        if (busy || selectedFile is null || selectedProjectRoot is null) return;
        busy = true;
        stopRequested = false;
        runButton.Enabled = false;
        stopButton.Enabled = false;
        output.Clear();
        try
        {
            statusLabel.Text = "내장 Python 준비 중…";
            var python = await Task.Run(EnsureRuntime);
            var dependencyPath = await EnsureDependenciesAsync(python, selectedProjectRoot);
            var info = new ProcessStartInfo(python)
            {
                WorkingDirectory = selectedProjectRoot,
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
            info.ArgumentList.Add(selectedProjectRoot);
            info.ArgumentList.Add(dependencyPath ?? "");
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
            busy = false;
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
        stopRequested = true;
        try { if (process is { HasExited: false }) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }

    private async Task<string?> EnsureDependenciesAsync(string python, string projectRoot)
    {
        var requirements = Path.Combine(projectRoot, "requirements.txt");
        if (!File.Exists(requirements)) return null;
        var input = Encoding.UTF8.GetBytes($"{RuntimeVersion}\n{projectRoot}\n{File.ReadAllText(requirements)}");
        var key = Convert.ToHexString(SHA256.HashData(input))[..24];
        var cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PyDrop", "packages");
        var target = Path.Combine(cacheRoot, key);
        if (File.Exists(Path.Combine(target, ".ready"))) return target;

        statusLabel.Text = "requirements.txt 패키지 준비 중…";
        AppendOutput("[PyDrop] 필요한 패키지를 준비합니다.\r\n");
        Directory.CreateDirectory(cacheRoot);
        var temporary = Path.Combine(cacheRoot, $"install-{Guid.NewGuid():N}");
        try
        {
            var installed = false;
            foreach (var offline in new[] { true, false })
            {
                if (stopRequested) throw new OperationCanceledException("패키지 준비가 중지됐습니다.");
                if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
                Directory.CreateDirectory(temporary);
                statusLabel.Text = offline ? "내장 패키지 설치 중…" : "온라인에서 패키지 설치 중…";
                AppendOutput(offline ? "[PyDrop] 내장 패키지 확인 중…\r\n" : "[PyDrop] 추가 패키지를 온라인에서 설치합니다. 인터넷 연결이 필요합니다.\r\n");
                var info = new ProcessStartInfo(python)
                {
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                info.Environment["PYTHONIOENCODING"] = "utf-8";
                info.Environment["PYTHONUTF8"] = "1";
                foreach (var argument in new[] { "-m", "pip", "install", "--only-binary=:all:", "--disable-pip-version-check", "--no-input", "--progress-bar", "off" })
                    info.ArgumentList.Add(argument);
                if (offline)
                {
                    info.ArgumentList.Add("--no-index");
                    info.ArgumentList.Add("--find-links");
                    info.ArgumentList.Add(Path.Combine(Path.GetDirectoryName(python)!, "wheels"));
                }
                info.ArgumentList.Add("--target");
                info.ArgumentList.Add(temporary);
                info.ArgumentList.Add("-r");
                info.ArgumentList.Add(requirements);
                process = new Process { StartInfo = info };
                if (!process.Start()) throw new InvalidOperationException("패키지 설치 프로세스를 시작하지 못했습니다.");
                stopButton.Enabled = true;
                var stdout = PumpAsync(process.StandardOutput);
                var stderr = PumpAsync(process.StandardError);
                await process.WaitForExitAsync();
                await Task.WhenAll(stdout, stderr);
                installed = process.ExitCode == 0;
                process.Dispose();
                process = null;
                if (installed) break;
            }
            if (!installed) throw new InvalidOperationException("패키지 설치 실패. 위의 설치 로그를 확인하세요.");
            stopButton.Enabled = false;
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            Directory.Move(temporary, target);
            File.WriteAllText(Path.Combine(target, ".ready"), "ok");
            AppendOutput("[PyDrop] 패키지 설치 완료.\r\n");
            return target;
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
        }
    }

    private static string EnsureRuntime()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PyDrop");
        var target = Path.Combine(root, $"python-{RuntimeVersion}-x64");
        var executable = Path.Combine(target, "python.exe");
        Directory.CreateDirectory(root);
        if (!File.Exists(executable))
        {
            var temporary = Path.Combine(root, $"install-{Guid.NewGuid():N}");
            try
            {
                using var stream = EmbeddedResource("python-3.12.10-embed-amd64.zip");
                ZipFile.ExtractToDirectory(stream, temporary);
                if (!File.Exists(Path.Combine(temporary, "python.exe"))) throw new InvalidDataException("Python 런타임 파일이 누락됐습니다.");
                try { Directory.Move(temporary, target); }
                catch (IOException) when (File.Exists(executable)) { Directory.Delete(temporary, true); }
            }
            catch
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
                throw;
            }
        }

        var wheel = Path.Combine(target, PipWheel);
        if (!File.Exists(wheel))
        {
            using var source = EmbeddedResource(PipWheel);
            using var destination = File.Create(wheel);
            source.CopyTo(destination);
        }
        var pathConfig = Path.Combine(target, "python312._pth");
        if (!File.Exists(pathConfig)) throw new InvalidDataException("Python 경로 설정 파일이 누락됐습니다.");
        if (!File.ReadAllLines(pathConfig).Any(line => line.Trim() == PipWheel))
            File.AppendAllText(pathConfig, $"{Environment.NewLine}{PipWheel}{Environment.NewLine}");
        var wheelDirectory = Path.Combine(target, "wheels");
        Directory.CreateDirectory(wheelDirectory);
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.Contains(".runtime.wheels.", StringComparison.Ordinal) && name.EndsWith(".whl", StringComparison.Ordinal)))
        {
            var filename = resource[(resource.LastIndexOf(".runtime.wheels.", StringComparison.Ordinal) + ".runtime.wheels.".Length)..];
            var path = Path.Combine(wheelDirectory, filename);
            if (File.Exists(path)) continue;
            using var source = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException($"내장 패키지를 찾을 수 없습니다: {filename}");
            using var destination = File.Create(path);
            source.CopyTo(destination);
        }
        return executable;
    }

    private static Stream EmbeddedResource(string suffix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith(suffix, StringComparison.Ordinal));
        return assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException($"내장 리소스를 찾을 수 없습니다: {suffix}");
    }
}
