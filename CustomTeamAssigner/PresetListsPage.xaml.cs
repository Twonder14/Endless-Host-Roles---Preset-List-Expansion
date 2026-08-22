using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CustomTeamAssigner;

public partial class PresetListsPage : Page
{
    private readonly MemoryStore _memory = new();
    private bool _busy;

    public PresetListsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
    }

    private void Reload()
    {
        try
        {
            _memory.LoadOrCreate();

            // Current is a preset-list NAME, not a slot number.
            CurrentText.Text =
                $"Current Options: {_memory.OptionsCurrent}\n" +
                $"Current English: {_memory.EnglishCurrent}";

            BuildPresetButtons();

            StatusText.Text =
                $"Loaded {_memory.GetVisiblePresets().Count} preset list(s). " +
                "Memory.txt is the source of truth.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Preset list error: " + ex.Message;
        }
    }

    private void BuildPresetButtons()
    {
        PresetPanel.Children.Clear();

        foreach (var item in _memory.GetVisiblePresets())
        {
            int slot = item.Slot;

            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var name = new TextBlock
            {
                Text = item.Preset,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            var slotText = new TextBlock
            {
                Text = item.IsCurrent ? "Current" : $"Slot {slot}",
                FontSize = 12,
                Foreground = Brushes.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };

            panel.Children.Add(name);
            panel.Children.Add(slotText);

            var button = new Button
            {
                Content = panel,
                Width = 245,
                MinHeight = 72,
                MaxHeight = 96,
                Margin = new Thickness(6),
                Padding = new Thickness(10, 7, 10, 7),
                Tag = slot,
                Background = item.IsCurrent
                    ? new SolidColorBrush(Color.FromRgb(70, 70, 70))
                    : new SolidColorBrush(Color.FromRgb(35, 35, 35)),
                BorderBrush = item.IsCurrent
                    ? Brushes.White
                    : new SolidColorBrush(Color.FromRgb(130, 130, 130)),
                BorderThickness = item.IsCurrent
                    ? new Thickness(2)
                    : new Thickness(1),
                ToolTip = item.IsCurrent
                    ? $"{item.Preset} is currently active."
                    : $"Activate {item.Preset} from Slot {slot}."
            };

            // Keep the current button enabled so WPF does not replace our
            // chosen colors with its low-contrast disabled-button styling.
            if (item.IsCurrent)
            {
                button.Click += (_, _) =>
                {
                    StatusText.Text =
                        $"{item.Preset} is already the current preset.";
                };
            }
            else
            {
                button.Click += async (_, _) => await Switch(slot);
            }

            PresetPanel.Children.Add(button);
        }

        if (PresetPanel.Children.Count == 0)
        {
            PresetPanel.Children.Add(new TextBlock
            {
                Text = "No preset lists found. Add a Current line and matching Slot lines to Memory.txt.",
                Foreground = Brushes.LightGray,
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10),
                MaxWidth = 500
            });
        }
    }

    private async Task Switch(int slot)
    {
        if (_busy)
            return;

        SetBusy(true);

        try
        {
            string result = await Task.Run(() => _memory.SwitchToSlot(slot));
            Reload();
            StatusText.Text = result;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Preset switch failed: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Random_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        SetBusy(true);

        try
        {
            string result = await Task.Run(() => _memory.ChooseRandom());
            Reload();
            StatusText.Text = result;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Random failed: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        SetBusy(true);

        try
        {
            using Process? process = _memory.OpenMemory();

            if (process != null)
                await process.WaitForExitAsync();

            _memory.Load();
            Reload();
            StatusText.Text = "Memory.txt saved and reloaded.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Sync failed: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void EditChat_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        SetBusy(true);

        try
        {
            _memory.EnsureChatSettings();

            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{_memory.ChatSettingsPath}\"",
                UseShellExecute = true
            });

            if (process != null)
                await process.WaitForExitAsync();

            StatusText.Text = "Chat message settings saved.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Chat settings error: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        SetBusy(true);

        try
        {
            StatusText.Text = await Task.Run(_memory.ValidateSlots);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Validation failed: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance.ShowHome();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;

        RandomButton.IsEnabled = !busy;
        SyncButton.IsEnabled = !busy;
        ChatButton.IsEnabled = !busy;
        ValidateButton.IsEnabled = !busy;
        BackButton.IsEnabled = !busy;

        foreach (var child in PresetPanel.Children)
        {
            if (child is Button button && button.Tag is int slot && slot != 0)
                button.IsEnabled = !busy;
        }
    }
}
