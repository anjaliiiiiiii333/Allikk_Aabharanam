using System;
using System.Collections.Generic;
using System.Windows;

namespace AccessoryPrototype
{
    public partial class PrototypeWindow : Window
    {
        private readonly IReadOnlyList<DatasetAccessory> _accessories;
        private PrototypeOverlayWindow _overlay;
        private PrototypeTracker _tracker;

        public PrototypeWindow()
        {
            InitializeComponent();
            _accessories = DatasetCatalog.Load();
            AccessoriesList.ItemsSource = _accessories;
            RefreshDesktopItems();
            DatasetStatus.Text = _accessories.Count == 0
                ? "No existing dataset image assets were found."
                : $"Loaded {_accessories.Count} existing image asset(s). Source files are read-only.";
        }

        private void RefreshDesktopButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDesktopItems();
        }

        private void RefreshDesktopItems()
        {
            DesktopItemsList.ItemsSource = DesktopIconProbe.ReadIcons();
            StatusText.Text = "Desktop icon list refreshed.";
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            var icon = DesktopItemsList.SelectedItem as DesktopIconInfo;
            var accessory = AccessoriesList.SelectedItem as DatasetAccessory;
            if (icon == null || accessory == null)
            {
                StatusText.Text = "Select a desktop item and an existing dataset accessory first.";
                return;
            }

            DetachPrototype();
            try
            {
                var image = AccessoryRenderer.RenderReferenceAccessory(accessory.AssetPath);
                bool isNormalAccessory = !string.Equals(
                    accessory.Category,
                    "keychain",
                    StringComparison.OrdinalIgnoreCase);
                _overlay = new PrototypeOverlayWindow(image, icon.Name, isNormalAccessory);
                _overlay.SetTarget(icon.Bounds);
                _overlay.Show();
                _tracker = new PrototypeTracker(icon.Name, _overlay);
                _tracker.Start();
                StatusText.Text = $"Prototype attached to '{icon.Name}' using '{accessory.Name}'.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not render the read-only reference: {ex.Message}";
                DetachPrototype();
            }
        }

        private void DetachButton_Click(object sender, RoutedEventArgs e)
        {
            DetachPrototype();
            StatusText.Text = "Prototype detached.";
        }

        private void DetachPrototype()
        {
            _tracker?.Dispose();
            _tracker = null;
            _overlay?.Close();
            _overlay = null;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DetachPrototype();
        }
    }
}
