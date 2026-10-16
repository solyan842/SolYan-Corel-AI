using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SolYan.CorelAI.Docker
{
    [ComVisible(true)]
    [Guid("2ED025F2-6C26-4C11-8B01-1489063BA939")]
    public sealed partial class SolYanAiDocker : UserControl
    {
        private readonly EnginePipeClient _engine = new EnginePipeClient();
        private readonly object _corelApplication;
        private bool _busy;

        public SolYanAiDocker(object application)
        {
            _corelApplication = application;
            InitializeComponent();
            Loaded += SolYanAiDocker_Loaded;
        }

        private async void SolYanAiDocker_Loaded(object sender, RoutedEventArgs e)
        {
            await EnsureEngineAndRefreshAsync();
            GC.KeepAlive(_corelApplication);
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await RunPromptAsync();
        }

        private async void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;

            SetBusy(true, "Undoing…");
            try
            {
                var result = await _engine.UndoAsync();
                AppendActivity("System", result);
                SetStatus(true, "Ready");
                await RefreshContextAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            await EnsureEngineAndRefreshAsync();
        }

        private async void PromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                await RunPromptAsync();
            }
        }

        private async Task RunPromptAsync()
        {
            if (_busy) return;

            var prompt = (PromptBox.Text ?? string.Empty).Trim();
            if (prompt.Length == 0) return;

            SetBusy(true, "Working in CorelDRAW…");
            AppendActivity("You", prompt);

            try
            {
                await EnsureEngineAsync();
                var answer = await _engine.RunTaskAsync(prompt);
                AppendActivity("SolYan AI", answer);
                PromptBox.Clear();
                SetStatus(true, "Ready");
                await RefreshContextAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task EnsureEngineAndRefreshAsync()
        {
            if (_busy) return;

            SetBusy(true, "Connecting to engine…");
            try
            {
                await EnsureEngineAsync();
                SetStatus(true, "Engine connected");
                await RefreshContextAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task EnsureEngineAsync()
        {
            try
            {
                await _engine.PingAsync();
                return;
            }
            catch
            {
                // Start the packaged engine below.
            }

            var enginePath = ResolveEnginePath();
            if (enginePath == null)
                throw new FileNotFoundException(
                    "SolYan Corel AI engine was not found. Expected Engine\\SolYan.CorelAI.Host.exe next to the Docker add-on.");

            var startInfo = new ProcessStartInfo
            {
                FileName = enginePath,
                Arguments = "--pipe",
                WorkingDirectory = Path.GetDirectoryName(enginePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);

            Exception lastError = null;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                await Task.Delay(200);
                try
                {
                    await _engine.PingAsync();
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw new InvalidOperationException(
                "SolYan Corel AI engine was started but did not accept the local IPC connection.",
                lastError);
        }

        private static string ResolveEnginePath()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var addOnDirectory = Path.GetDirectoryName(assemblyPath);
            if (string.IsNullOrWhiteSpace(addOnDirectory)) return null;

            var candidates = new[]
            {
                Path.Combine(addOnDirectory, "Engine", "SolYan.CorelAI.Host.exe"),
                Path.Combine(addOnDirectory, "SolYan.CorelAI.Host.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }

        private async Task RefreshContextAsync()
        {
            var context = await _engine.GetContextAsync();
            ContextText.Text = Compact(context, 700);
        }

        private void SetBusy(bool busy, string message)
        {
            _busy = busy;
            SendButton.IsEnabled = !busy;
            UndoButton.IsEnabled = !busy;
            RefreshButton.IsEnabled = !busy;
            PromptBox.IsEnabled = !busy;

            if (!string.IsNullOrWhiteSpace(message))
            {
                StatusText.Text = message;
                StatusDot.Fill = Brushes.Goldenrod;
            }
        }

        private void SetStatus(bool healthy, string message)
        {
            StatusText.Text = message;
            StatusDot.Fill = healthy ? Brushes.LimeGreen : Brushes.IndianRed;
        }

        private void ShowError(Exception ex)
        {
            SetStatus(false, "Engine error");
            AppendActivity("Error", ex.Message);
        }

        private void AppendActivity(string role, string text)
        {
            var entry = role + ": " + (text ?? string.Empty).Trim();
            ActivityText.Text = string.IsNullOrWhiteSpace(ActivityText.Text)
                ? entry
                : ActivityText.Text + Environment.NewLine + Environment.NewLine + entry;
        }

        private static string Compact(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return "No active document context.";
            var singleLine = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return singleLine.Length <= maxLength
                ? singleLine
                : singleLine.Substring(0, maxLength) + "…";
        }
    }
}
