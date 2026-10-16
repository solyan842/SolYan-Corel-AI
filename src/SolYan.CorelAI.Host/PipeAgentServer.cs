using System.IO.Pipes;
using System.Text;

namespace SolYan.CorelAI.Host;

public sealed class PipeAgentServer
{
    public const string PipeName = "SolYan.CorelAI.v1";

    private readonly CorelAiRuntime _runtime;

    public PipeAgentServer(CorelAiRuntime runtime) => _runtime = runtime;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            await ServeClientAsync(server, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ServeClientAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false), false, 4096, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null) return;

            string response;
            try
            {
                response = await ExecuteAsync(line, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                response = Error(ex.Message);
            }

            await writer.WriteLineAsync(response).ConfigureAwait(false);
        }
    }

    private async Task<string> ExecuteAsync(string command, CancellationToken cancellationToken)
    {
        if (command.Equals("PING", StringComparison.Ordinal))
            return Ok("SolYan Corel AI engine");

        if (command.Equals("CAPABILITIES", StringComparison.Ordinal))
            return Ok((await _runtime.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false)).ToJsonString());

        if (command.Equals("CONTEXT", StringComparison.Ordinal))
            return Ok((await _runtime.GetContextAsync(cancellationToken).ConfigureAwait(false)).ToJsonString());

        if (command.Equals("UNDO", StringComparison.Ordinal))
        {
            await _runtime.UndoAsync(cancellationToken).ConfigureAwait(false);
            return Ok("Undone in CorelDRAW.");
        }

        const string taskPrefix = "TASK ";
        if (command.StartsWith(taskPrefix, StringComparison.Ordinal))
        {
            var prompt = Decode(command[taskPrefix.Length..]);
            var answer = await _runtime.RunTaskAsync(prompt, cancellationToken).ConfigureAwait(false);
            return Ok(answer);
        }

        throw new InvalidOperationException("Unknown IPC command.");
    }

    private static string Ok(string value) => "OK " + Encode(value);
    private static string Error(string value) => "ERR " + Encode(value);

    private static string Encode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string Decode(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("IPC payload is not valid Base64.", ex);
        }
    }
}
