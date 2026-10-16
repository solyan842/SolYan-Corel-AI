using SolYan.CorelAI.Agent;
using SolYan.CorelAI.Core;
using SolYan.CorelAI.Corel;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("OPENAI_API_KEY is not set.");
    return 2;
}

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

var registry = new ToolRegistry(CorelToolCatalog.Create(corel));
var agent = new OpenAiResponsesAgent(http, registry, apiKey, model);

Console.WriteLine($"SolYan Corel AI — {model}");
Console.WriteLine("CorelDRAW connected. Type a task, /undo, or /quit.");

while (true)
{
    Console.Write("\n> ");
    var prompt = Console.ReadLine();
    if (prompt is null || prompt.Equals("/quit", StringComparison.OrdinalIgnoreCase)) break;

    if (prompt.Equals("/undo", StringComparison.OrdinalIgnoreCase))
    {
        await corel.UndoAsync();
        Console.WriteLine("Undone in CorelDRAW.");
        continue;
    }

    if (string.IsNullOrWhiteSpace(prompt)) continue;

    var grouped = false;
    try
    {
        grouped = await corel.BeginAiJobAsync("SolYan AI");
        var answer = await agent.RunAsync(prompt);
        Console.WriteLine(answer);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Task failed: {ex.Message}");
    }
    finally
    {
        if (grouped)
        {
            try { await corel.EndAiJobAsync(); }
            catch (Exception ex) { Console.Error.WriteLine($"Could not close Corel undo group: {ex.Message}"); }
        }
    }
}

return 0;
