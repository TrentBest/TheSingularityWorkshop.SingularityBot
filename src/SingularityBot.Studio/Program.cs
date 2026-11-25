using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using SingularityBot.Behavior;
using TheSingularityWorkshop.GUI.WPF;
using TheSingularityWorkshop.Workshop.Gui;

namespace SingularityBot.Studio;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var application = new Application();
        application.Run(StudioWindow.Create(args));
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

    private static TextBox _documentEditor = null!;
    private static TextBlock _status = null!;
    private static string? _currentPath;

    public static Window Create(string[] args)
    {
        _documentEditor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.NoWrap,
            MinHeight = 300,
            Padding = new Thickness(12),
        };
        _status = new TextBlock
        {
            Text = "Ready · edit the BotDefinition document, validate it, then save it for the Runner.",
            Margin = new Thickness(10, 6, 10, 6),
            TextWrapping = TextWrapping.Wrap
        };

        var root = new DockPanel();
        var statusFrame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(70, 78, 90)),
            BorderThickness = new Thickness(1, 1, 0, 0),
            Child = _status
        };
        DockPanel.SetDock(statusFrame, Dock.Bottom);
        root.Children.Add(statusFrame);

        var semanticHost = new ContentControl();
        DockPanel.SetDock(semanticHost, Dock.Top);
        root.Children.Add(semanticHost);

        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var editorFrame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(70, 78, 90)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(10, 4, 10, 4),
            Child = _documentEditor
        };
        root.Children.Add(editorFrame);

        var window = new WpfWindowBuilder()
            .WithTitle("SingularityBot Studio")
            .WithSize(1440, 960)
            .WithContent(root)
            .Build();

        void Render(string selectedTab)
        {
            var tree = BuildTree(selectedTab);
            semanticHost.Content = WpfGuiRenderer.Render(tree, command =>
            {
                const string prefix = "studio.tab.";
                if (command.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var tab = Tabs.FirstOrDefault(item => SafeId(item) == command[prefix.Length..]);
                    if (tab is not null)
                    {
                        var isEditor = tab == "Bot Definition";
                        editorFrame.Visibility = isEditor ? Visibility.Visible : Visibility.Collapsed;
                        toolbar.Visibility = isEditor ? Visibility.Visible : Visibility.Collapsed;
                        Render(tab);
                    }
                }
            });
        }

        _documentEditor.TextChanged += (_, _) =>
        {
            UpdateStatus("Unsaved changes · validate before saving or launching.");
        };

        var definitionArgument = args.FirstOrDefault(value =>
            value.StartsWith("--definition=", StringComparison.Ordinal));
        if (definitionArgument is not null)
            LoadDocument(definitionArgument["--definition=".Length..]);
        else
            _documentEditor.Text = BotDefinitionCatalog.CreateDefault().Serialize();

        Render("Bot Definition");
        return window;
    }

    private static UIElement BuildToolbar()
    {
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(10, 8, 10, 4)
        };
        toolbar.Children.Add(CreateButton("Open…", (_, _) => OpenDocument()));
        toolbar.Children.Add(CreateButton("Validate", (_, _) => ValidateDocument()));
        toolbar.Children.Add(CreateButton("Save", (_, _) => SaveDocument(saveAs: false)));
        toolbar.Children.Add(CreateButton("Save As…", (_, _) => SaveDocument(saveAs: true)));
        toolbar.Children.Add(new TextBlock
        {
            Text = "  The Runner consumes this exact versioned JSON artifact via --definition=<path>.",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        });
        return toolbar;
    }

    private static Button CreateButton(string label, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 92,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        button.Click += handler;
        return button;
    }

    private static void OpenDocument()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open SingularityBot definition",
            Filter = "Bot definitions (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
            LoadDocument(dialog.FileName);
    }

    private static void LoadDocument(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            // Parse before replacing the current document: a malformed file must not
            // destroy edits already open in the Studio.
            var definition = BotDefinition.Parse(text);
            _documentEditor.Text = definition.Serialize();
            _currentPath = Path.GetFullPath(path);
            UpdateStatus($"Loaded and parsed {Path.GetFileName(path)} · {definition.Name} · {definition.Race} / {definition.ModeId}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                         System.Text.Json.JsonException or InvalidOperationException or ArgumentException)
        {
            MessageBox.Show(exception.Message, "Unable to open BotDefinition", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus("Open failed · the current editor contents were preserved.");
        }
    }

    private static bool TryGetDefinition(out BotDefinition? definition)
    {
        definition = null;
        try
        {
            definition = BotDefinition.Parse(_documentEditor.Text);
            return true;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or
                                         InvalidOperationException or ArgumentException)
        {
            UpdateStatus("Validation failed · " + exception.Message);
            MessageBox.Show(exception.Message, "BotDefinition validation failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private static void ValidateDocument()
    {
        if (!TryGetDefinition(out var definition) || definition is null)
            return;
        var errors = definition.Validate();
        UpdateStatus(errors.Count == 0
            ? $"VALID · {definition.Name} · {definition.Race} / {definition.ModeId} · {definition.Opening.Steps.Count} opening steps"
            : "INVALID · " + string.Join(" | ", errors));
    }

    private static void SaveDocument(bool saveAs)
    {
        if (!TryGetDefinition(out var definition) || definition is null)
            return;

        var path = _currentPath;
        if (saveAs || string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save SingularityBot definition",
                Filter = "Bot definitions (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = definition.Id + ".json"
            };
            if (dialog.ShowDialog() != true)
                return;
            path = dialog.FileName;
        }

        try
        {
            File.WriteAllText(path!, definition.Serialize());
            _currentPath = Path.GetFullPath(path!);
            UpdateStatus($"SAVED · {_currentPath} · ready for Runner --definition");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(exception.Message, "Unable to save BotDefinition", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus("Save failed · " + exception.Message);
        }
    }

    private static GuiNode BuildTree(string selectedTab)
    {
        var root = GuiBuilder.Create("Column", "studio.root")
            .Child("Text", "studio.title", node => node.Text("SINGULARITYBOT STUDIO"))
            .Child("Text", "studio.subtitle", node =>
                node.Text("Bot authoring · validate and save the exact artifact executed by the Runner"));

        var navigation = GuiBuilder.Create("Row", "studio.navigation");
        foreach (var tab in Tabs)
        {
            var tabId = tab;
            navigation.Child("Button", "studio.tab." + SafeId(tab), node =>
                node.Text(tab).WithOnClick("studio.tab." + SafeId(tabId)));
        }
        root.Child(navigation);

        var body = GuiBuilder.Create("Panel", "studio.body");
        switch (selectedTab)
        {
            case "Bot Definition":
                AddHeading(body, "Bot Definition · editable JSON source");
                AddLine(body, "bot.editor.instructions",
                    "Edit the complete versioned BotDefinition below. Validate uses the same authoritative parser and semantic validator as the Runner. Save writes a portable JSON artifact; the Runner loads it with --definition=<path>.");
                AddLine(body, "bot.editor.features",
                    "The document contains race and mode selection, opening production steps, default goals, advisor composition, strategy transitions, and bounded AI consultation policy.");
                AddLine(body, "bot.editor.safety",
                    "Changes are authoring-only. Saving never mutates a live match. Load a saved artifact into a fresh Runner session to execute it.");
                break;
            case "CommandAi":
                AddCommandAi(body);
                break;
            case "HeadlessAi":
                AddHeadlessAi(body);
                break;
            case "GrammarAi":
                AddPlannedPackage(body, "GrammarAi",
                    "Planned integration · not a current SingularityBot runtime dependency.",
                    "Reserved for pinned, versioned GrammarAi statement and expression definitions. No arbitrary C# or LINQ execution is supported.");
                break;
            case "ProtocolAi":
                AddPlannedPackage(body, "ProtocolAi",
                    "Planned integration · not a current SingularityBot runtime dependency.",
                    "Reserved for qualified domain vocabulary identities and exact catalog provenance. It does not dispatch StarCraft actions.");
                break;
            default:
                AddLine(body, "studio.unknown", "Unknown workspace tab.");
                break;
        }

        root.Child(body);
        return root.Build();
    }

    private static void AddCommandAi(GuiBuilder body)
    {
        AddHeading(body, "CommandAi · active integration");
        AddLine(body, "command.version", $"Bot capability vocabulary: {BotCommandVocabulary.Version}");
        AddLine(body, "command.description",
            "The registered vocabulary is the bot's low-level proposal bridge, not yet the final fluent capability language.");
        foreach (var action in BotCommandVocabulary.All)
            AddLine(body, "command." + SafeId(action.Id),
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

    private static void UpdateStatus(string message) => _status.Text = message;

    private static string SafeId(string value) =>
        string.Concat(value.Select(character =>
            char.IsAsciiLetterOrDigit(character) ? character : '-')).Trim('-').ToLowerInvariant();
}
