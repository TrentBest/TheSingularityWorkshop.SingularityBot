using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Win32;
using TheSingularityWorkshop.SingularityBot.Gui.Authoring;

namespace TheSingularityWorkshop.SingularityBot.Gui;

public sealed class ForgeWorkspaceViewModel : INotifyPropertyChanged
{
    private GroupDraft? _selectedGroup; private UnitProfileDraft? _selectedProfile;
    private StrategyPhaseDraft? _selectedPhase; private BehaviorRuleDraft? _selectedRule;
    private string _status = "Synthetic example loaded · not connected to a runtime";
    public event PropertyChangedEventHandler? PropertyChanged;
    public BotDefinitionDraft Definition { get; private set; }
    public ObservableCollection<GroupDraft> Groups => Definition.Groups;
    public ObservableCollection<UnitProfileDraft> Profiles => Definition.UnitProfiles;
    public ObservableCollection<StrategyPhaseDraft> Phases => Definition.Phases;
    public ObservableCollection<BehaviorRuleDraft> Rules => Definition.Rules;
    public GroupDraft? SelectedGroup { get => _selectedGroup; set { _selectedGroup=value; OnPropertyChanged(); } }
    public UnitProfileDraft? SelectedProfile { get => _selectedProfile; set { _selectedProfile=value; OnPropertyChanged(); } }
    public StrategyPhaseDraft? SelectedPhase { get => _selectedPhase; set { _selectedPhase=value; OnPropertyChanged(); } }
    public BehaviorRuleDraft? SelectedRule { get => _selectedRule; set { _selectedRule=value; OnPropertyChanged(); } }
    public string Status { get => _status; private set { _status=value; OnPropertyChanged(); } }
    public string BotName { get => Definition.Bot.Name; set { Definition.Bot.Name=value; OnPropertyChanged(); } }
    public string Race { get => Definition.Bot.Race; set { Definition.Bot.Race=value; OnPropertyChanged(); } }

    public ForgeWorkspaceViewModel()
    {
        Definition=CreateSyntheticExample();
        SelectedGroup=Groups.FirstOrDefault(); SelectedProfile=Profiles.FirstOrDefault();
        SelectedPhase=Phases.FirstOrDefault(); SelectedRule=Rules.FirstOrDefault();
    }
    public void AddGroup() { var n=Groups.Count+1; var x=new GroupDraft($"group-{n}",$"New Group {n}","Choose a unit membership selector","Describe the group's shared objective","Describe coordination policy"); Groups.Add(x); SelectedGroup=x; Status="New group added"; }
    public void AddPhase() { var n=Phases.Count+1; var x=new StrategyPhaseDraft($"phase-{n}",$"New Phase {n}","Describe the phase objective",n); Phases.Add(x); SelectedPhase=x; Status="New phase added"; }
    public void AddRule() { var n=Rules.Count+1; var x=new BehaviorRuleDraft($"rule-{n}","retreat","unit.healthRatio","lessThan",0.35,"unit.retreat"); Rules.Add(x); SelectedRule=x; Status="Rule added · fact and intent identifiers are provisional pending runtime catalog confirmation"; }
    public void Save()
    {
        var dialog=new SaveFileDialog { Filter="Forge bot draft (*.forge.json)|*.forge.json|JSON (*.json)|*.json", FileName=$"{Definition.Bot.Id}.forge.json" };
        if(dialog.ShowDialog()!=true)return;
        File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(Definition,new JsonSerializerOptions{WriteIndented=true}));
        Status=$"Draft saved locally · {Path.GetFileName(dialog.FileName)} · not applied to a running bot";
    }
    public void Load()
    {
        var dialog=new OpenFileDialog { Filter="Forge bot draft (*.forge.json;*.json)|*.forge.json;*.json|All files (*.*)|*.*" };
        if(dialog.ShowDialog()!=true)return;
        try {
            var loaded=JsonSerializer.Deserialize<BotDefinitionDraft>(File.ReadAllText(dialog.FileName));
            if(loaded is null)throw new InvalidDataException("The file did not contain a Forge definition.");
            Definition=loaded; OnPropertyChanged(nameof(Definition)); OnPropertyChanged(nameof(Groups)); OnPropertyChanged(nameof(Profiles)); OnPropertyChanged(nameof(Phases)); OnPropertyChanged(nameof(Rules));
            SelectedGroup=Groups.FirstOrDefault(); SelectedProfile=Profiles.FirstOrDefault(); SelectedPhase=Phases.FirstOrDefault(); SelectedRule=Rules.FirstOrDefault();
            Status=$"Draft loaded · {Path.GetFileName(dialog.FileName)} · runtime validation not performed";
        } catch(Exception ex){Status=$"Could not load draft: {ex.Message}";}
    }
    private static BotDefinitionDraft CreateSyntheticExample()
    {
        var d=new BotDefinitionDraft { Bot=new("terran-safe-marine","Terran Safe Marine","Terran") };
        d.Phases.Add(new("opening","Opening","Protect the economy while establishing basic production.",10));
        d.Phases.Add(new("pressure","Pressure","Apply controlled pressure while preserving a retreat path.",20));
        d.Groups.Add(new("economy","Economy Group","Unit type is SCV","Gather resources and construct authored production.","Workers retain individual task state while serving the economy objective."));
        d.Groups.Add(new("marine-squad","Marine Squad","Unit type is Marine","Defend and apply coordinated pressure.","Members coordinate around a shared objective; each Marine retains its own profile."));
        d.UnitProfiles.Add(new("scv-worker","SCV","Worker","worker.default","provisional"));
        d.UnitProfiles.Add(new("marine-infantry","Marine","Infantry","marine.default","provisional"){RetreatHealthRatio=0.35});
        d.Behaviors.Add(new("gather","worker.gather","provisional")); d.Behaviors.Add(new("build","worker.build","provisional"));
        d.Behaviors.Add(new("defend","group.defend","provisional")); d.Behaviors.Add(new("pressure","combat.pressure","provisional")); d.Behaviors.Add(new("retreat","combat.retreat","provisional"));
        d.Rules.Add(new("marine-retreat-low-health","retreat","unit.healthRatio","lessThan",0.35,"unit.retreat"));
        return d;
    }
    private void OnPropertyChanged([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
}