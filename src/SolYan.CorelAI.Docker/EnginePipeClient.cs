using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace SolYan.CorelAI.Docker
{
    internal sealed class EnginePipeClient
    {
        private const string PipeName = "SolYan.CorelAI.v1";
        private const int ConnectTimeoutMs = 750;

        public Task<string> PingAsync() => SendAsync("PING");
        public Task<string> GetContextAsync() => SendAsync("CONTEXT");
        public Task<string> GetCapabilitiesAsync() => SendAsync("CAPABILITIES");
        public Task<string> UndoAsync() => SendAsync("UNDO");
        public Task<string> ClearApiKeyAsync() => SendAsync("CLEAR_API_KEY");

        public Task<string> SetApiKeyAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Trim().Length < 20)
                throw new ArgumentException("OpenAI API key is empty or invalid.", nameof(apiKey));

            return SendAsync("SET_API_KEY " + Encode(apiKey.Trim()));
        }

        public Task<string> RunTaskAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Task cannot be empty.", nameof(prompt));

            return SendAsync("TASK " + Encode(prompt));
        }

        private static async Task<string> SendAsync(string command)
        {
            using (var pipe = new NamedPipeClientStream(
                       ".",
                       PipeName,
                       PipeDirection.InOut,
                       PipeOptions.Asynchronous))
            {
                await Task.Run(() => pipe.Connect(ConnectTimeoutMs)).ConfigureAwait(false);

                using (var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, true))
                using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true))
                {
                    writer.AutoFlush = true;
                    await writer.WriteLineAsync(command).ConfigureAwait(false);
                    var response = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (response == null)
                        throw new IOException("SolYan Corel AI engine closed the IPC connection unexpectedly.");

                    if (response.StartsWith("OK ", StringComparison.Ordinal))
                        return Decode(response.Substring(3));

                    if (response.StartsWith("ERR ", StringComparison.Ordinal))
                        throw new InvalidOperationException(Decode(response.Substring(4)));

                    throw new InvalidDataException("SolYan Corel AI engine returned an invalid IPC response.");
                }
            }
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("SolYan Corel AI engine returned invalid Base64 data.", ex);
            }
        }
    }
}
