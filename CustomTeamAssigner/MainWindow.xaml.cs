using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace CustomTeamAssigner
{
    public partial class MainWindow
    {
        public static MainWindow Instance { get; private set; } = null!;

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
        }

        void ImportPlaySet(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new()
            {
                Filter = "Text files (*.txt)|*.txt",
                RestoreDirectory = true
            };
            if (ofd.ShowDialog() == true)
            {
                Utils.Teams.Clear();
                File.ReadAllLines(ofd.FileName).Do(line => new Team(line.Split(';')[0]).Import(line));
                Navigator.Visibility = Visibility.Visible;
            Navigator.NavigationService.Navigate(new PlaySetListerPage());
                Utils.SetMainWindowContents(Visibility.Collapsed);
            }
        }

        void CreateNewPlaySet(object sender, RoutedEventArgs e)
        {
            Utils.Teams.Clear();
            Navigator.Visibility = Visibility.Visible;
            Navigator.NavigationService.Navigate(new PlaySetListerPage());
            Utils.SetMainWindowContents(Visibility.Collapsed);
        }

        private void OpenPresetLists(object sender, RoutedEventArgs e)
        {
            Utils.SetMainWindowContents(Visibility.Collapsed);
            Navigator.Visibility = Visibility.Visible;
            Navigator.Navigate(new PresetListsPage());
        }
        public void ShowHome()
        {
            Navigator.Content = null;
            Navigator.Visibility = Visibility.Hidden;
            Utils.SetMainWindowContents(Visibility.Visible);
        }


        private void QMQuestionsClick(object sender, RoutedEventArgs e)
        {
            Utils.SetMainWindowContents(Visibility.Collapsed);
            Navigator.Visibility = Visibility.Visible;
            Navigator.NavigationService.Navigate(new QMQuestions());
        }

        private void OpenRoleDescFinder(object sender, RoutedEventArgs e)
        {
            Utils.SetMainWindowContents(Visibility.Collapsed);
            Navigator.Visibility = Visibility.Visible;
            Navigator.NavigationService.Navigate(new RoleDescFinder());
        }

        private void OpenTemplateCreator(object sender, RoutedEventArgs e)
        {
            Utils.SetMainWindowContents(Visibility.Collapsed);
            Navigator.Visibility = Visibility.Visible;
            Navigator.NavigationService.Navigate(new RichTextEditor());
        }
    }
}
