using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace DesktopKeychainApp
{
    public partial class AccessoryAssignmentWindow : Window
    {
        private readonly AccessoryAssignmentService _assignmentService;
        private readonly IReadOnlyList<AccessoryAsset> _accessories;
        private readonly IReadOnlyList<DesktopItem> _desktopItems;
        private AccessoryAsset _selectedAccessory;

        public AccessoryAssignmentWindow(
            AccessoryAssignmentService assignmentService,
            IReadOnlyList<AccessoryAsset> accessories,
            IReadOnlyList<DesktopItem> desktopItems)
        {
            InitializeComponent();
            _assignmentService = assignmentService;
            _accessories = accessories;
            _desktopItems = desktopItems;
            DesktopItemComboBox.ItemsSource = desktopItems;
            BuildAccessoryButtons();
            ShowManualPanel();
        }

        public DesktopItem AssignedItem { get; private set; }

        private void BuildAccessoryButtons()
        {
            foreach (AccessoryAsset accessory in _accessories)
            {
                var button = new Button
                {
                    Tag = accessory,
                    Width = 130,
                    Height = 150,
                    Margin = new Thickness(5),
                    Padding = new Thickness(5),
                    ToolTip = accessory.AssetPath
                };

                var panel = new StackPanel();
                var image = new Image
                {
                    Width = 90,
                    Height = 100,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Source = LoadThumbnail(accessory.AssetPath)
                };
                panel.Children.Add(image);
                panel.Children.Add(new TextBlock
                {
                    Text = accessory.AccessoryName,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(2, 5, 2, 0)
                });
                button.Content = panel;
                button.Click += AccessoryButton_Click;
                AccessoryPanel.Children.Add(button);
            }

            StatusText.Text = _accessories.Count == 0
                ? "No real accessory image assets were found in the dataset."
                : $"{_accessories.Count} real dataset accessory image(s) available.";
        }

        private void AccessoryButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedAccessory = (AccessoryAsset)((Button)sender).Tag;
            StatusText.Text = $"Selected: {_selectedAccessory.AccessoryName}. Choose a desktop item and assign it.";
        }

        private void AssignSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var item = DesktopItemComboBox.SelectedItem as DesktopItem;
            if (_selectedAccessory == null || item == null)
            {
                StatusText.Text = "Select both an accessory and a desktop item first.";
                return;
            }

            _assignmentService.SetManualAssignment(item.Name, _selectedAccessory);
            AssignedItem = item;
            StatusText.Text = $"Assigned {_selectedAccessory.AccessoryName} to {item.Name}.";
            DialogResult = true;
        }

        private void RemoveAssignmentButton_Click(object sender, RoutedEventArgs e)
        {
            var item = DesktopItemComboBox.SelectedItem as DesktopItem;
            if (item == null)
            {
                StatusText.Text = "Choose a desktop item first.";
                return;
            }

            _assignmentService.RemoveAssignment(item.Name);
            StatusText.Text = $"Removed the accessory assignment from {item.Name}.";
        }

        private void GenerateAssignmentsButton_Click(object sender, RoutedEventArgs e)
        {
            _assignmentService.GenerateAutomaticAssignments(_desktopItems, _accessories);
            AutomaticStatusText.Text = "Automatic assignments generated and saved. Manual assignments were preserved.";
        }

        private void ManualModeButton_Click(object sender, RoutedEventArgs e) => ShowManualPanel();

        private void AutomaticModeButton_Click(object sender, RoutedEventArgs e)
        {
            ManualPanel.Visibility = Visibility.Collapsed;
            AutomaticPanel.Visibility = Visibility.Visible;
        }

        private void ShowManualPanel()
        {
            ManualPanel.Visibility = Visibility.Visible;
            AutomaticPanel.Visibility = Visibility.Collapsed;
        }

        private static BitmapImage LoadThumbnail(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 100;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}