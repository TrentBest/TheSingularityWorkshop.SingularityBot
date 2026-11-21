using System.Windows;
using System.Windows.Controls;
using SingularityBot.Behavior;
using TheSingularityWorkshop.GUI.WPF;
using TheSingularityWorkshop.Workshop.Gui;

namespace SingularityBot.Studio;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new Application();
        application.Run(StudioWindow.Create());
    }
}

internal static class StudioWindow
{
    private static readonly string[] Tabs =
    [
        "Bot Definition",
        "CommandAi",
        "HeadlessAi",
        "GrammarAi",
        "ProtocolAi"
    ];

    public static Window Create()
    {
        var content = new DockPanel();
        var semanticHost = new ContentControl();
        content.Children.Add(semanticHost);

        var window = new WpfWindowBuilder()
            .WithTitle("SingularityBot Studio")
            .WithSize(1280, 840)
            .WithContent(content)
            .Build();

        void Render(string selectedTab)
        {
            var tree = BuildTree(selectedTab);
            semanticHost.Content = WpfGuiRenderer.Render(tree, command =>
            {
                const string prefix = "studio.tab.";
                if (command.StartsWith(prefix, StringComparison.Ordinal))
                    Render(command[prefix.Length..]);
            });
        }

        Render("Bot Definition");
        return window;
    }

    private static GuiNode BuildTree(string selectedTab)
    {
        var root = GuiBuilder.Create("Column", "studio.root")
            .Child("Text", "studio.title", node => node.Text("SINGULARITYBOT STUDIO"))
            .Child("Text", "studio.subtitle", node =>
                node.Text("Declarative authoring and inspection · no live-match mutation"));

        var navigation = GuiBuilder.Create("Row", "studio.navigation");
        foreach (var tab in Tabs)
        {
            var tabId = tab;
            navigation.Child("Button", "studio.tab." + SafeId(tab), node =>
                node.Text(tab).WithOnClick("studio.tab." + tabId));
        }
        root.Child(navigation);

        var body = GuiBuilder.Create("Panel", "studio.body");
        switch (selectedTab)
        {
            case "Bot Definition":
                AddBotDefinition(body);
                break;
            case "CommandAi":
                AddCommandAi(body);
                break;
            case "HeadlessAi":
                AddHeadlessAi(body);
                break;
            case "GrammarAi":
                AddPlannedPackage(body, "GrammarAi",
                    "Proposed authoring/interchange integration. Not a current SingularityBot runtime dependency.",
                    "This tab is reserved for pinned, versioned GrammarAi statement and expression definitions. No arbitrary C# or LINQ execution is supported.");
                break;
            case "ProtocolAi":
                AddPlannedPackage(body, "ProtocolAi",
                    "Proposed authoring/interchange integration. Not a current SingularityBot runtime dependency.",
                    "This tab is reserved for qualified domain vocabulary identities and exact catalog provenance. It does not dispatch StarCraft actions.");
                break;
            default:
                AddLine(body, "studio.unknown", "Unknown workspace tab.");
                break;
        }

        root.Child(body);
        return root.Build();
    }

    private static void AddBotDefinition(GuiBuilder body)
    {
        var definition = BotDefinitionCatalog.CreateDefault();
        AddHeading(body, "Bot Definition · compiled default");
        AddLine(body, "bot.id", $"Id: {definition.Id}");
        AddLine(body, "bot.name", $"Name: {definition.Name}");
        AddLine(body, "bot.race", $"Race / mode: {definition.Race} / {definition.ModeId}");
        AddLine(body, "bot.strategy", $"Strategy profile: {definition.StrategyProfile}");
        AddLine(body, "bot.opening", $"Opening: {definition.Opening.Name} ({definition.Opening.Steps.Count} steps)");
        AddLine(body, "bot.goals", $"Default goals: {definition.DefaultGoals.Name}");
        AddLine(body, "bot.transitions", $"Strategy transitions: {definition.StrategyTransitions.Count}");
        AddLine(body, "bot.validation", $"Validation: {(definition.Validate().Count == 0 ? "PASS" : "FAIL")}");
        AddHeading(body, "Compiled advisor composition");
        foreach (var advisor in definition.Runtime.Advisors)
            AddLine(body, "advisor." + SafeId(advisor.Id),
                $"{advisor.Id} · {advisor.Definition} · {(advisor.Enabled ? "enabled" : "disabled")} · priority {advisor.Priority}");
        AddLine(body, "bot.note",
            "This first slice reads the compiled catalog. Editing, persistence, and live runtime changes are intentionally not enabled yet.");
    }

    private static void AddCommandAi(GuiBuilder body)
    {
        AddHeading(body, "CommandAi · active integration");
        AddLine(body, "command.version", $"Bot capability vocabulary: {BotCommandVocabulary.Version}");
        AddLine(body, "command.description",
            "The current registered vocabulary is the bot's existing low-level proposal bridge, not yet the final fluent capability language.");
        foreach (var action in BotCommandVocabulary.All)
            AddLine(body, "command." + action.Id,
                $"{action.Name} · id {action.Id} · {action.Description}");
    }

    private static void AddHeadlessAi(GuiBuilder body)
    {
        var policy = BotDefinitionCatalog.CreateDefault().Ai;
        AddHeading(body, "HeadlessAi · active integration");
        AddLine(body, "headless.enabled", $"Consultation enabled: {policy.Enabled}");
        AddLine(body, "headless.provider", $"Provider: {policy.Provider}");
        AddLine(body, "headless.model", $"Model: {policy.Model}");
        AddLine(body, "headless.start-budget", $"Startup call budget: {policy.MaxOnStartCalls}");
        AddLine(body, "headless.match-budget", $"In-match call budget: {policy.MaxInMatchCalls}");
        AddLine(body, "headless.timeout", $"Timeout: {policy.TimeoutMilliseconds} ms");
        AddLine(body, "headless.spacing", $"Minimum game-loop spacing: {policy.MinimumGameLoopSpacing}");
        AddLine(body, "headless.triggers", "Allowed triggers: " + string.Join(", ", policy.AllowedTriggers));
        AddLine(body, "headless.secrets",
            "Credentials are host-supplied and are never read or persisted by this authoring view.");
    }

    private static void AddPlannedPackage(GuiBuilder body, string name, string status, string description)
    {
        AddHeading(body, name);
        AddLine(body, name + ".status", status);
        AddLine(body, name + ".description", description);
    }

    private static void AddHeading(GuiBuilder builder, string text) =>
        AddLine(builder, "heading." + SafeId(text), text);

    private static void AddLine(GuiBuilder builder, string id, string text) =>
        builder.Child("Text", id, node => node.Text(text));

    private static string SafeId(string value) =>
        string.Concat(value.Select(character =>
            char.IsAsciiLetterOrDigit(character) ? character : '-')).Trim('-').ToLowerInvariant();
}
