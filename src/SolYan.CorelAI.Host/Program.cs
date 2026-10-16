using SolYan.CorelAI.Core;
using SolYan.CorelAI.Corel;
using SolYan.CorelAI.Host;

var environmentApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var apiKey = !string.IsNullOrWhiteSpace(environmentApiKey)
    ? environmentApiKey
    : WindowsSecretStore.TryLoadApiKey();
var environmentModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");
var model = !string.IsNullOrWhiteSpace(environmentModel)
    ? environmentModel
    : WindowsSettingsStore.LoadModel();
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
using var corel = new CorelSession();

try
{
    await corel.ConnectAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Could not connect to CorelDRAW: {ex.Message}");
    return 3;
}

try
{
    var capabilities = await corel.GetCapabilitiesAsync();
    Console.WriteLine($"CorelDRAW runtime: {capabilities.ToJsonString()}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Capability probe warning: {ex.Message}");
}

var tools = CorelToolCatalog.Create(corel)
    .Concat(CorelAdvancedToolCatalog.Create(corel))
    .Concat(CorelExportToolCatalog.Create(corel))
    .Concat(CorelPreflightToolCatalog.Create(corel));
var registry = new ToolRegistry(tools);
var runtime = new CorelAiRuntime(corel, http, registry, model, apiKey);

if (args.Any(arg => arg.Equals("--pipe", StringComparison.OrdinalIgnoreCase)))
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cts.Cancel();
    };

    Console.WriteLine($"SolYan Corel AI pipe server — {PipeAgentServer.PipeName}");
    Console.WriteLine(runtime.AiConfigured
        ? $"AI configured with model {runtime.Model}."
        : "No OpenAI API key is configured yet; the Corel Docker can configure one for this Windows user.");

    try
    {
        await new PipeAgentServer(runtime).RunAsync(cts.Token);
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
        // Normal Ctrl+C shutdown.
    }

    return 0;
}

Console.WriteLine($"SolYan Corel AI — {runtime.Model}");
Console.WriteLine(runtime.AiConfigured
    ? "CorelDRAW connected. AI geometry uses millimetres and visual feedback is enabled. Type a task, /undo, /capabilities, /context, /preflight, /diagnostics, or /quit."
    : "CorelDRAW connected, but no OpenAI API key is configured. Inspection commands still work; the installed Docker can save an API key securely for this Windows user.");

while (true)
{
    Console.Write("\n> ");
    var prompt = Console.ReadLine();
    if (prompt is null || prompt.Equals("/quit", StringComparison.OrdinalIgnoreCase)) break;

    try
    {
        if (prompt.Equals("/undo", StringComparison.OrdinalIgnoreCase))
        {
            await runtime.UndoAsync();
            Console.WriteLine("Undone in CorelDRAW.");
            continue;
        }

        if (prompt.Equals("/capabilities", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine((await runtime.GetCapabilitiesAsync()).ToJsonString());
            continue;
        }

        if (prompt.Equals("/context", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine((await runtime.GetContextAsync()).ToJsonString());
            continue;
        }

        if (prompt.Equals("/preflight", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine((await runtime.RunPreflightAsync()).ToJsonString());
            continue;
        }

        if (prompt.Equals("/diagnostics", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine((await runtime.RunDiagnosticsAsync()).ToJsonString());
            continue;
        }

        if (prompt.Equals("/clear-key", StringComparison.OrdinalIgnoreCase))
        {
            runtime.ClearApiKey();
            Console.WriteLine("Stored OpenAI API key cleared for this Windows user.");
            continue;
        }

        if (string.IsNullOrWhiteSpace(prompt)) continue;
        Console.WriteLine(await runtime.RunTaskAsync(prompt));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Task failed: {ex.Message}");
    }
}

return 0;
