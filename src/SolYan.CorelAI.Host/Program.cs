using SolYan.CorelAI.Agent;
using SolYan.CorelAI.Core;
using SolYan.CorelAI.Corel;
using SolYan.CorelAI.Host;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-6-astra";
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

OpenAiResponsesAgent? agent = null;
if (!string.IsNullOrWhiteSpace(apiKey))
{
    var registry = new ToolRegistry(CorelToolCatalog.Create(corel));
    agent = new OpenAiResponsesAgent(http, registry, apiKey, model);
}

var runtime = new CorelAiRuntime(corel, agent);

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
        ? $"AI configured with model {model}."
        : "OPENAI_API_KEY is not set; inspection/undo are available but AI tasks are disabled.");

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

Console.WriteLine($"SolYan Corel AI — {model}");
Console.WriteLine(runtime.AiConfigured
    ? "CorelDRAW connected. AI geometry uses millimetres. Type a task, /undo, /capabilities, /context, or /quit."
    : "CorelDRAW connected, but OPENAI_API_KEY is not set. /undo, /capabilities and /context still work.");

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

        if (string.IsNullOrWhiteSpace(prompt)) continue;
        Console.WriteLine(await runtime.RunTaskAsync(prompt));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Task failed: {ex.Message}");
    }
}

return 0;
