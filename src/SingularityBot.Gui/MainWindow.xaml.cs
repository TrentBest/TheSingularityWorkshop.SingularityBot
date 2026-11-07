using System.Windows;
namespace TheSingularityWorkshop.SingularityBot.Gui;
public partial class MainWindow : Window
{
    private readonly ForgeWorkspaceViewModel _workspace;
    public MainWindow()
    {
        InitializeComponent();
        _workspace = new ForgeWorkspaceViewModel();
        DataContext = _workspace;
    }
    private void AddGroup_Click(object sender, RoutedEventArgs e) => _workspace.AddGroup();
    private void AddPhase_Click(object sender, RoutedEventArgs e) => _workspace.AddPhase();
    private void AddBehavior_Click(object sender, RoutedEventArgs e) => _workspace.AddBehavior();
    private void AddRule_Click(object sender, RoutedEventArgs e) => _workspace.AddRule();
    private void Save_Click(object sender, RoutedEventArgs e) => _workspace.Save();
    private void Load_Click(object sender, RoutedEventArgs e) => _workspace.Load();
}