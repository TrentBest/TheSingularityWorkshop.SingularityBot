using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows.Data;
using System.Windows.Input;
using System.Globalization;
using System.Diagnostics;
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
    private static TextBox _mapEditor = null!;
    private static string? _currentPath;
    private static Process? _runnerProcess;
    private static bool _isDirty;
    private static bool _isLoadingDocument;

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
            if (_isLoadingDocument)
                return;
            _isDirty = true;
            UpdateStatus("Unsaved changes · validate before saving or launching.");
        };

        var definitionArgument = args.FirstOrDefault(value =>
            value.StartsWith("--definition=", StringComparison.Ordinal));
        if (definitionArgument is not null)
            LoadDocument(definitionArgument["--definition=".Length..]);
        else
            SetDocumentText(BotDefinitionCatalog.CreateDefault().Serialize());

        Render("Bot Definition");
        window.PreviewKeyDown += (_, eventArgs) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && eventArgs.Key == Key.S)
            {
                SaveDocument(saveAs: false);
                eventArgs.Handled = true;
            }
            else if (eventArgs.Key == Key.F5)
            {
                LaunchLocalMatch();
                eventArgs.Handled = true;
            }
        };
        window.Closing += (_, eventArgs) =>
        {
            if (_isDirty && MessageBox.Show(
                    "The BotDefinition has unsaved changes. Close Studio and discard them?",
                    "Unsaved BotDefinition", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                eventArgs.Cancel = true;
        };
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
        toolbar.Children.Add(CreateButton("Build Production Plan…", (_, _) => EditProductionPlan()));
        toolbar.Children.Add(CreateButton("Edit Goals…", (_, _) => EditGoals()));
        toolbar.Children.Add(CreateButton("Save", (_, _) => SaveDocument(saveAs: false)));
        toolbar.Children.Add(CreateButton("Save As…", (_, _) => SaveDocument(saveAs: true)));
        toolbar.Children.Add(new TextBlock
        {
            Text = "Map",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 5, 0)
        });
        _mapEditor = new TextBox
        {
            Text = "AcolyteLE.SC2Map",
            Width = 190,
            Padding = new Thickness(6, 4, 6, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "SC2 map filename or relative path beneath the StarCraft II Maps directory"
        };
        toolbar.Children.Add(_mapEditor);
        toolbar.Children.Add(CreateButton("Launch Local Match", (_, _) => LaunchLocalMatch()));
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


    private static void LaunchLocalMatch()
    {
        if (string.IsNullOrWhiteSpace(_mapEditor.Text))
        {
            MessageBox.Show("Enter an SC2 map filename or relative path.", "Map required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TrySaveDocumentForLaunch())
            return;

        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
        {
            MessageBox.Show(
                "The SingularityBot source checkout was not found. Open Studio from the repository workspace, or launch the saved JSON from PowerShell using the Runner README instructions.",
                "Runner project unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_runnerProcess is { HasExited: false })
        {
            var restart = MessageBox.Show(
                $"A training match is already running (PID {_runnerProcess.Id}). Stop it and start a fresh match on '{_mapEditor.Text.Trim()}'? The current match will end.",
                "Restart training session", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (restart != MessageBoxResult.Yes)
                return;

            try
            {
                _runnerProcess.Kill(entireProcessTree: true);
                if (!_runnerProcess.WaitForExit(5000))
                {
                    const string message = "The Runner did not exit within five seconds. Studio has left the process handle intact; no replacement match was started.";
                    UpdateStatus("Runner shutdown timed out · existing process may still be active.");
                    MessageBox.Show(message, "Runner shutdown timed out",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _runnerProcess.Dispose();
                _runnerProcess = null;
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                if (_runnerProcess.HasExited)
                {
                    _runnerProcess.Dispose();
                    _runnerProcess = null;
                }
                else
                {
                    MessageBox.Show(exception.Message, "Unable to restart Runner",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }
        else
        {
            _runnerProcess?.Dispose();
            _runnerProcess = null;
        }

        try
        {
            var runnerProject = Path.Combine(repositoryRoot, "src", "SingularityBot.Runner",
                "SingularityBot.Runner.csproj");
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(runnerProject);
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add("Debug");
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add($"--definition={_currentPath}");
            startInfo.ArgumentList.Add($"--map={_mapEditor.Text.Trim()}");

            _runnerProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The .NET host did not return a Runner process.");
            UpdateStatus($"TRAINING SESSION STARTED · map={_mapEditor.Text.Trim()} · Runner PID {_runnerProcess.Id}. A map change requires a fresh session.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or
                                         IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(exception.Message, "Unable to launch Runner",
                MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus("Runner launch failed · " + exception.Message);
        }
    }

    private static bool TrySaveDocumentForLaunch()
    {
        if (!TryGetDefinition(out var definition) || definition is null)
            return false;

        var path = _currentPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save BotDefinition before launching",
                Filter = "Bot definitions (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = definition.Id + ".json"
            };
            if (dialog.ShowDialog() != true)
                return false;
            path = dialog.FileName;
        }

        try
        {
            File.WriteAllText(path!, definition.Serialize());
            _currentPath = Path.GetFullPath(path!);
            _isDirty = false;
            UpdateStatus($"VALIDATED AND SAVED · {_currentPath}");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(exception.Message, "Unable to save BotDefinition",
                MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus("Save failed · " + exception.Message);
            return false;
        }
    }

    private static string? FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var runnerProject = Path.Combine(directory.FullName, "src", "SingularityBot.Runner",
                    "SingularityBot.Runner.csproj");
                if (File.Exists(runnerProject))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        return null;
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
            LoadDocument(dialog.FileName, confirmDiscard: true);
    }

    private static void LoadDocument(string path, bool confirmDiscard = false)
    {
        try
        {
            var text = File.ReadAllText(path);
            // Parse before replacing the current document: a malformed file must not
            // destroy edits already open in the Studio.
            var definition = BotDefinition.Parse(text);
            if (confirmDiscard && _isDirty &&
                MessageBox.Show("Discard the current unsaved BotDefinition and open this file?",
                    "Discard unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            SetDocumentText(definition.Serialize());
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

    private static void EditProductionPlan()
    {
        if (!TryGetDefinition(out var definition) || definition is null)
            return;

        var plan = definition.Opening;
        var rows = new ObservableCollection<ProductionStepRow>(
            plan.Steps.Select(step => new ProductionStepRow
            {
                Id = step.Id,
                Target = step.Target,
                Producer = step.Producer,
                DesiredCount = step.DesiredCount,
                EarliestGameLoop = step.EarliestGameLoop,
                Priority = step.Priority,
                DependsOn = string.Join(", ", step.DependsOn ?? Array.Empty<string>()),
                Lane = step.Lane.ToString()
            }));

        var planId = new TextBox { Text = plan.Id, MinWidth = 180, Margin = new Thickness(4) };
        var planName = new TextBox { Text = plan.Name, MinWidth = 260, Margin = new Thickness(4) };
        var race = new ComboBox { MinWidth = 120, Margin = new Thickness(4), IsEditable = false };
        race.ItemsSource = new[] { "Terran", "Protoss", "Zerg" };
        race.SelectedItem = plan.Race;
        race.IsEnabled = false; // Race selection belongs to the parent BotDefinition, not an isolated opening plan.

        var metadata = new WrapPanel { Margin = new Thickness(8) };
        metadata.Children.Add(new TextBlock { Text = "Plan ID", VerticalAlignment = VerticalAlignment.Center });
        metadata.Children.Add(planId);
        metadata.Children.Add(new TextBlock { Text = "Name", VerticalAlignment = VerticalAlignment.Center });
        metadata.Children.Add(planName);
        metadata.Children.Add(new TextBlock { Text = "Race", VerticalAlignment = VerticalAlignment.Center });
        metadata.Children.Add(race);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = true,
            CanUserSortColumns = false,
            IsReadOnly = false,
            ItemsSource = rows,
            Margin = new Thickness(8),
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow
        };

        void AddColumn(string header, string property, double width)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(property) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(width)
            });
        }

        AddColumn("Step ID", nameof(ProductionStepRow.Id), 145);
        AddColumn("Target", nameof(ProductionStepRow.Target), 125);
        AddColumn("Producer", nameof(ProductionStepRow.Producer), 125);
        AddColumn("Count", nameof(ProductionStepRow.DesiredCount), 65);
        AddColumn("Earliest Loop", nameof(ProductionStepRow.EarliestGameLoop), 100);
        AddColumn("Priority", nameof(ProductionStepRow.Priority), 70);
        AddColumn("Depends On (comma separated IDs)", nameof(ProductionStepRow.DependsOn), 230);
        AddColumn("Lane (Buildings / Units)", nameof(ProductionStepRow.Lane), 145);

        var add = CreateButton("Add Step", (_, _) =>
        {
            var index = rows.Count + 1;
            rows.Add(new ProductionStepRow
            {
                Id = $"step-{index}",
                Target = "Marine",
                Producer = "Barracks",
                DesiredCount = 1,
                EarliestGameLoop = 0,
                Priority = 50,
                Lane = "Units"
            });
            grid.SelectedItem = rows[^1];
            grid.ScrollIntoView(rows[^1]);
        });
        var remove = CreateButton("Remove Selected", (_, _) =>
        {
            if (grid.SelectedItem is ProductionStepRow selected)
                rows.Remove(selected);
        });
        var moveUp = CreateButton("Move Up", (_, _) =>
        {
            if (grid.SelectedItem is ProductionStepRow selected)
            {
                var index = rows.IndexOf(selected);
                if (index > 0) rows.Move(index, index - 1);
                grid.SelectedItem = selected;
            }
        });
        var moveDown = CreateButton("Move Down", (_, _) =>
        {
            if (grid.SelectedItem is ProductionStepRow selected)
            {
                var index = rows.IndexOf(selected);
                if (index >= 0 && index < rows.Count - 1) rows.Move(index, index + 1);
                grid.SelectedItem = selected;
            }
        });
        var apply = CreateButton("Apply to Definition", (_, _) => { });
        var cancel = CreateButton("Cancel", (_, _) => { });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8)
        };
        actions.Children.Add(add);
        actions.Children.Add(remove);
        actions.Children.Add(moveUp);
        actions.Children.Add(moveDown);
        actions.Children.Add(apply);
        actions.Children.Add(cancel);

        var layout = new DockPanel();
        DockPanel.SetDock(metadata, Dock.Top);
        layout.Children.Add(metadata);
        DockPanel.SetDock(actions, Dock.Bottom);
        layout.Children.Add(actions);
        layout.Children.Add(grid);

        var dialog = new Window
        {
            Title = "Production Plan Builder · Buildings and Units",
            Width = 1180,
            Height = 620,
            MinWidth = 900,
            MinHeight = 420,
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = layout
        };

        apply.Click += (_, _) =>
        {
            if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) ||
                !grid.CommitEdit(DataGridEditingUnit.Row, true))
                return;
            if (string.IsNullOrWhiteSpace(planId.Text) ||
                string.IsNullOrWhiteSpace(planName.Text) ||
                race.SelectedItem is not string selectedRace)
            {
                MessageBox.Show("Plan ID, name, and race are required.", "Production plan",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var steps = new List<ProductionStepDefinition>();
            foreach (var row in rows)
            {
                if (!Enum.TryParse<ProductionLane>(row.Lane, ignoreCase: true, out var lane))
                {
                    MessageBox.Show($"Step '{row.Id}' has an invalid lane. Use Buildings or Units.",
                        "Production plan", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                steps.Add(new ProductionStepDefinition(
                    row.Id.Trim(),
                    row.Target.Trim(),
                    row.Producer.Trim(),
                    row.DesiredCount,
                    row.EarliestGameLoop,
                    row.Priority,
                    row.DependsOn.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                    lane));
            }

            var updatedPlan = new ProductionPlanDefinition
            {
                SchemaVersion = plan.SchemaVersion,
                Id = planId.Text.Trim(),
                Name = planName.Text.Trim(),
                Race = selectedRace,
                Steps = steps
            };
            var errors = ProductionPlanValidator.Validate(updatedPlan);
            if (errors.Count != 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, errors), "Production plan validation failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var root = JsonNode.Parse(_documentEditor.Text)!.AsObject();
                var opening = root["opening"]!.AsObject();
                opening["id"] = updatedPlan.Id;
                opening["name"] = updatedPlan.Name;
                opening["race"] = updatedPlan.Race;
                var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
                options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                opening["steps"] = JsonSerializer.SerializeToNode(updatedPlan.Steps, options);

                var updatedDefinition = BotDefinition.Parse(root.ToJsonString());
                _documentEditor.Text = updatedDefinition.Serialize();
                UpdateStatus($"PRODUCTION PLAN UPDATED · {updatedPlan.Name} · {updatedPlan.Steps.Count} steps · validate and save the definition");
                dialog.DialogResult = true;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
            {
                MessageBox.Show(exception.Message, "Unable to apply production plan",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        cancel.Click += (_, _) => dialog.Close();
        dialog.ShowDialog();
    }

    private sealed class ProductionStepRow
    {
        public string Id { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Producer { get; set; } = string.Empty;
        public int DesiredCount { get; set; } = 1;
        public int EarliestGameLoop { get; set; }
        public int Priority { get; set; } = 50;
        public string DependsOn { get; set; } = string.Empty;
        public string Lane { get; set; } = "Units";
    }

    private static void EditGoals()
    {
        if (!TryGetDefinition(out var definition) || definition is null)
            return;

        var plan = definition.DefaultGoals;
        var id = new TextBox { Text = plan.Id, Margin = new Thickness(4), MinWidth = 220 };
        var name = new TextBox { Text = plan.Name, Margin = new Thickness(4), MinWidth = 280 };
        var role = new ComboBox
        {
            ItemsSource = Enum.GetNames<GoalActionRole>(),
            SelectedItem = plan.DefaultRole.ToString(),
            Margin = new Thickness(4),
            MinWidth = 150
        };
        var scoutCount = new TextBox { Text = plan.ScoutCount.ToString(CultureInfo.InvariantCulture), Margin = new Thickness(4), MinWidth = 100 };
        var fallback = new TextBox { Text = plan.FallbackHealthFraction.ToString(CultureInfo.InvariantCulture), Margin = new Thickness(4), MinWidth = 100 };
        var excludeEnemyStart = new CheckBox { Content = "Exclude enemy start sector from scouting", IsChecked = plan.ExcludeEnemyStartSector, Margin = new Thickness(4) };
        var stutterStep = new CheckBox { Content = "Enable stutter-step behavior", IsChecked = plan.StutterStepEnabled, Margin = new Thickness(4) };
        var engageThreats = new CheckBox { Content = "Engage visible threats", IsChecked = plan.EngageVisibleThreats, Margin = new Thickness(4) };

        var layout = new StackPanel { Margin = new Thickness(12) };
        void AddField(string label, Control control)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            var caption = new TextBlock
            {
                Text = label,
                Width = 190,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(caption, Dock.Left);
            row.Children.Add(caption);
            row.Children.Add(control);
            layout.Children.Add(row);
        }

        AddField("Goal plan ID", id);
        AddField("Display name", name);
        AddField("Default role", role);
        AddField("Scout count", scoutCount);
        AddField("Fallback health fraction (0–1)", fallback);
        layout.Children.Add(excludeEnemyStart);
        layout.Children.Add(stutterStep);
        layout.Children.Add(engageThreats);
        layout.Children.Add(new TextBlock
        {
            Text = "Existing explicit scout sectors are preserved. The parent BotDefinition remains the source of race/mode identity.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(4, 12, 4, 8),
            Opacity = 0.8
        });

        var apply = CreateButton("Apply Goals", (_, _) => { });
        var cancel = CreateButton("Cancel", (_, _) => { });
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(4, 12, 4, 4)
        };
        actions.Children.Add(apply);
        actions.Children.Add(cancel);
        layout.Children.Add(actions);

        var dialog = new Window
        {
            Title = "Strategic Goals · authoring",
            Width = 680,
            Height = 500,
            MinWidth = 560,
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = layout,
            SizeToContent = SizeToContent.Height
        };

        apply.Click += (_, _) =>
        {
            if (role.SelectedItem is not string selectedRole ||
                !Enum.TryParse<GoalActionRole>(selectedRole, out var parsedRole) ||
                !int.TryParse(scoutCount.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedScoutCount) ||
                !float.TryParse(fallback.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFallback) ||
                !float.IsFinite(parsedFallback))
            {
                MessageBox.Show("Enter a valid role, whole-number scout count, and finite fallback health fraction.",
                    "Goal validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var updatedGoals = new GoalActionPlanDefinition
            {
                Version = plan.Version,
                Id = id.Text.Trim(),
                Name = name.Text.Trim(),
                DefaultRole = parsedRole,
                ScoutCount = parsedScoutCount,
                ScoutSectors = plan.ScoutSectors.ToList(),
                ExcludeEnemyStartSector = excludeEnemyStart.IsChecked == true,
                StutterStepEnabled = stutterStep.IsChecked == true,
                FallbackHealthFraction = parsedFallback,
                EngageVisibleThreats = engageThreats.IsChecked == true
            };
            var errors = updatedGoals.Validate();
            if (errors.Count != 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, errors), "Goal validation failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var root = JsonNode.Parse(_documentEditor.Text)!.AsObject();
                var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
                options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                root["defaultGoals"] = JsonSerializer.SerializeToNode(updatedGoals, options);
                var updatedDefinition = BotDefinition.Parse(root.ToJsonString());
                _documentEditor.Text = updatedDefinition.Serialize();
                UpdateStatus($"GOALS UPDATED · {updatedGoals.Name} · {updatedGoals.DefaultRole} · validate and save the definition");
                dialog.DialogResult = true;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
            {
                MessageBox.Show(exception.Message, "Unable to apply goals",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        cancel.Click += (_, _) => dialog.Close();
        dialog.ShowDialog();
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
            _isDirty = false;
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
            AddLine(body, "command." + SafeId(action.Id.ToString(CultureInfo.InvariantCulture)),
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

    private static void SetDocumentText(string text)
    {
        _isLoadingDocument = true;
        try
        {
            _documentEditor.Text = text;
            _isDirty = false;
        }
        finally
        {
            _isLoadingDocument = false;
        }
    }

    private static void UpdateStatus(string message) => _status.Text = message;

    private static string SafeId(string value) =>
        string.Concat(value.Select(character =>
            char.IsAsciiLetterOrDigit(character) ? character : '-')).Trim('-').ToLowerInvariant();
}
