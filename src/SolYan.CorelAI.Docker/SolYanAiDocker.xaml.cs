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

        private async void SendButton_Click(object sender, RoutedEventArgs e) => await RunPromptAsync();

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
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false, null); }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            await EnsureEngineAndRefreshAsync();
        }

        private async void PreflightButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            SetBusy(true, "Running document preflight…");
            try
            {
                await EnsureEngineAsync();
                var result = await _engine.RunPreflightAsync();
                var blocking = ExtractJsonInt(result, "blocking_issue_count") ?? 0;
                var warnings = ExtractJsonInt(result, "warning_count") ?? 0;
                var ready = ExtractJsonBoolean(result, "ready_for_output") == true;

                AppendActivity(
                    "Preflight",
                    BuildPreflightSummary(ready, blocking, warnings) + Environment.NewLine + Compact(result, 5000));
                SetStatus(ready, ready
                    ? "Preflight ready"
                    : "Preflight found blocking issues");
            }
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false, null); }
        }

        private async void DiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            SetBusy(true, "Running diagnostics…");
            try
            {
                await EnsureEngineAsync();
                var result = await _engine.RunDiagnosticsAsync();
                var passed = ExtractJsonInt(result, "passed") ?? 0;
                var failed = ExtractJsonInt(result, "failed") ?? 0;

                AppendActivity(
                    "Diagnostics",
                    BuildDiagnosticsSummary(passed, failed) + Environment.NewLine + Compact(result, 7000));
                SetStatus(failed == 0, failed == 0
                    ? "Diagnostics passed"
                    : "Diagnostics degraded");
            }
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false, null); }
        }

        private async void MutationTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;

            var confirmation = MessageBox.Show(
                "Mutation + Undo Smoke Test will briefly create a temporary 5 × 5 mm rectangle in the active CorelDRAW document and immediately undo it.\n\n" +
                "Important: creating a new CorelDRAW action can clear existing Redo history. The test is never run automatically.\n\n" +
                "Run the test now?",
                "SolYan AI — Mutation Test",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes) return;

            SetBusy(true, "Running mutation + undo test…");
            try
            {
                await EnsureEngineAsync();
                var result = await _engine.RunMutationUndoSmokeTestAsync();
                var restored = ExtractJsonBoolean(result, "restored") == true;

                AppendActivity(
                    "Mutation Test",
                    (restored
                        ? "PASS: temporary object was created and the document shape count was restored by one Undo."
                        : "FAIL: the smoke test did not verify a fully restored document shape count.") +
                    Environment.NewLine + Compact(result, 5000));
                SetStatus(restored, restored
                    ? "Mutation test passed"
                    : "Mutation test failed");
                await RefreshContextAsync();
            }
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false, null); }
        }

        private async void SaveKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var key = (ApiKeyBox.Password ?? string.Empty).Trim();
            if (key.Length < 20)
            {
                AppendActivity("Error", "Paste a valid OpenAI API key before saving.");
                return;
            }

            SetBusy(true, "Saving API key…");
            try
            {
                await EnsureEngineAsync();
                var result = await _engine.SetApiKeyAsync(key);
                ApiKeyBox.Clear();
                AppendActivity("System", result);
                SetStatus(true, "AI configured");
                await RefreshContextAsync();
            }
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false, null); }
        }

        private async void ClearKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            SetBusy(true, "Clearing API key…");
            try
            {
                await EnsureEngineAsync();
                var result = await _engine.ClearApiKeyAsync();
                ApiKeyBox.Clear();
                AppendActivity("System", result);
                SetStatus(true, "API key cleared");
                await RefreshContextAsync();
            }
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false, null); }
        }

        private async void ApplyModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var item = ModelBox.SelectedItem as ComboBoxItem;
            var model = item == null ? null : item.Tag as string;
            if (string.IsNullOrWhiteSpace(model))
            {
                AppendActivity("Error", "Choose an AI model first.");
                return;
            }

            SetBusy(true, "Switching AI model…");
            try
            {
                await EnsureEngineAsync();
                var result = await _engine.SetModelAsync(model);
                AppendActivity("System", result);
                SetStatus(true, model);
                await RefreshContextAsync();
            }
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false, null); }
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
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false, null); }
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
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false, null); }
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
                catch (Exception ex) { lastError = ex; }
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
                if (File.Exists(candidate)) return candidate;
            return null;
        }

        private async Task RefreshContextAsync()
        {
            var context = await _engine.GetContextAsync();
            ContextText.Text = Compact(context, 700);
            SelectModelFromContext(context);
        }

        private void SelectModelFromContext(string context)
        {
            var model = ExtractJsonString(context, "model");
            if (string.IsNullOrWhiteSpace(model)) return;
            foreach (var value in ModelBox.Items)
            {
                var item = value as ComboBoxItem;
                if (item != null && string.Equals(item.Tag as string, model, StringComparison.Ordinal))
                {
                    ModelBox.SelectedItem = item;
                    return;
                }
            }
        }

        private static string ExtractJsonString(string json, string property)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var marker = "\"" + property + "\":\"";
            var start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += marker.Length;
            var end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static int? ExtractJsonInt(string json, string property)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var marker = "\"" + property + "\":";
            var start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += marker.Length;
            var end = start;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            int value;
            return end > start && int.TryParse(json.Substring(start, end - start), out value) ? value : (int?)null;
        }

        private static bool? ExtractJsonBoolean(string json, string property)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var marker = "\"" + property + "\":";
            var start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += marker.Length;
            if (json.IndexOf("true", start, StringComparison.Ordinal) == start) return true;
            if (json.IndexOf("false", start, StringComparison.Ordinal) == start) return false;
            return null;
        }

        private static string BuildPreflightSummary(bool ready, int blocking, int warnings)
        {
            if (ready)
                return "Ready for output: no blocking issues. Warnings: " + warnings + ".";
            return "Not ready for output. Blocking issues: " + blocking + ". Warnings: " + warnings + ".";
        }

        private static string BuildDiagnosticsSummary(int passed, int failed)
        {
            return failed == 0
                ? "Runtime diagnostics passed: " + passed + " checks passed."
                : "Runtime diagnostics degraded: " + passed + " passed, " + failed + " failed.";
        }

        private void SetBusy(bool busy, string message)
        {
            _busy = busy;
            SendButton.IsEnabled = !busy;
            UndoButton.IsEnabled = !busy;
            RefreshButton.IsEnabled = !busy;
            PreflightButton.IsEnabled = !busy;
            DiagnosticsButton.IsEnabled = !busy;
            MutationTestButton.IsEnabled = !busy;
            SaveKeyButton.IsEnabled = !busy;
            ClearKeyButton.IsEnabled = !busy;
            ApplyModelButton.IsEnabled = !busy;
            ModelBox.IsEnabled = !busy;
            PromptBox.IsEnabled = !busy;
            ApiKeyBox.IsEnabled = !busy;

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
            return singleLine.Length <= maxLength ? singleLine : singleLine.Substring(0, maxLength) + "…";
        }
    }
}
