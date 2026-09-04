using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DesktopKeychainApp
{
    public partial class MainWindow : Window
    {
        private DesktopIconDetector _detector;
        private KeychainTracker _currentTracker;
        private KeychainOverlay _currentOverlay;
        private AudioManager _audioManager;
        private SystemActionAudioLayer _systemActionAudioLayer;
        private DispatcherTimer _statusUpdateTimer;
        private ObservableCollection<DesktopItem> _desktopItems;
        private AccessoryAssignmentService _assignmentService;
        private IReadOnlyList<AccessoryAsset> _accessories;
        private bool _audioSystemEnabled = true;
        private bool _audioSystemHealthy = true;

        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                string assetPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets",
                    "keychain.png");
                AssetGenerator.GenerateKeychainAsset(assetPath);

                _detector = new DesktopIconDetector();
                _assignmentService = new AccessoryAssignmentService();
                _accessories = _assignmentService.LoadAvailableAccessories();
                _audioManager = new AudioManager();
                _systemActionAudioLayer = new SystemActionAudioLayer(_detector);
                _systemActionAudioLayer.ActionDetected += SystemActionAudioLayer_ActionDetected;
                _systemActionAudioLayer.Start();

                _desktopItems = new ObservableCollection<DesktopItem>();
                DesktopItemsListBox.ItemsSource = _desktopItems;

                _statusUpdateTimer = new DispatcherTimer();
                _statusUpdateTimer.Interval = TimeSpan.FromSeconds(1);
                _statusUpdateTimer.Tick += (s, e) => UpdateStatus();

                UpdateAudioStatus();
                UpdateStatus("Initialized. Ready to detect desktop items.");
            }
            catch (Exception ex)
            {
                _audioSystemHealthy = false;
                UpdateAudioStatus();
                UpdateStatus($"Initialization error: {ex.Message}");
                Debug.WriteLine($"Initialization error: {ex}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Scanning desktop for items...");

                var items = _detector.GetDesktopItems();

                _desktopItems.Clear();
                foreach (var item in items)
                {
                    _desktopItems.Add(item);
                }

                UpdateStatus($"Found {items.Count} desktop items.");
                AttachButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error refreshing desktop items: {ex.Message}");
                Debug.WriteLine($"Error refreshing items: {ex}");
            }
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = DesktopItemsListBox.SelectedItem as DesktopItem;
            if (selectedItem == null)
            {
                UpdateStatus("Please select an item first.");
                return;
            }

            AttachAssignedAccessory(selectedItem);
        }

        private void AssignmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (_desktopItems.Count == 0)
            {
                UpdateStatus("Refresh Desktop Items before assigning accessories.");
                return;
            }

            var assignmentWindow = new AccessoryAssignmentWindow(
                _assignmentService,
                _accessories,
                _desktopItems)
            {
                Owner = this
            };

            if (assignmentWindow.ShowDialog() == true && assignmentWindow.AssignedItem != null)
            {
                DesktopItemsListBox.SelectedItem = assignmentWindow.AssignedItem;
                AttachAssignedAccessory(assignmentWindow.AssignedItem);
            }
        }

        private void AttachAssignedAccessory(DesktopItem selectedItem)
        {
            AccessoryAssignment assignment = _assignmentService.GetAssignment(selectedItem.Name);
            AccessoryAsset accessory = assignment == null
                ? null
                : _assignmentService.FindAccessory(assignment.Accessory, _accessories);
            if (accessory == null || !File.Exists(accessory.AssetPath))
            {
                UpdateStatus($"No dataset accessory is assigned to '{selectedItem.Name}'. Open Accessory Assignment first.");
                return;
            }

            try
            {
                DetachKeychain();

                UpdateStatus($"Attaching {accessory.AccessoryName} to '{selectedItem.Name}'...");

                Rect itemBounds = selectedItem.BoundingRectangle;
                var neighboringIcons = new List<Rectangle>();
                foreach (DesktopItem item in _detector.GetDesktopItems())
                {
                    if (ReferenceEquals(item, selectedItem))
                        continue;

                    neighboringIcons.Add(new Rectangle(
                        (int)Math.Round(item.BoundingRectangle.Left - itemBounds.Left),
                        (int)Math.Round(item.BoundingRectangle.Top - itemBounds.Top),
                        (int)Math.Round(item.BoundingRectangle.Width),
                        (int)Math.Round(item.BoundingRectangle.Height)));
                }

                Bitmap keychainBitmap = DatasetAccessoryRenderer.CreateOverlayBitmap(
                    accessory.AssetPath,
                    (int)Math.Round(itemBounds.Width),
                    (int)Math.Round(itemBounds.Height),
                    neighboringIcons);

                _currentOverlay = new KeychainOverlay(keychainBitmap);
                _currentOverlay.SetPosition(
                    itemBounds.Left,
                    itemBounds.Top,
                    itemBounds.Width,
                    itemBounds.Height);

                _currentTracker = new KeychainTracker(_detector, _currentOverlay, selectedItem);
                _currentTracker.ItemLost += Tracker_ItemLost;
                _currentTracker.ItemFound += Tracker_ItemFound;
                _currentTracker.StartTracking();

                _audioSystemEnabled = true;
                _audioSystemHealthy = _audioManager != null;
                UpdateAudioStatus();
                UpdateStatus($"{accessory.AccessoryName} attached to '{selectedItem.Name}'. Tracking its position.");
                AttachButton.IsEnabled = false;
                DetachButton.IsEnabled = true;
                DesktopItemsListBox.IsEnabled = false;
                RefreshButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error attaching keychain: {ex.Message}");
                Debug.WriteLine($"Error attaching keychain: {ex}");
            }
        }

        private void DetachButton_Click(object sender, RoutedEventArgs e)
        {
            DetachKeychain();
        }

        private void DetachKeychain()
        {
            try
            {
                _statusUpdateTimer.Stop();

                if (_currentTracker != null)
                {
                    _currentTracker.ItemLost -= Tracker_ItemLost;
                    _currentTracker.ItemFound -= Tracker_ItemFound;
                    _currentTracker.Dispose();
                    _currentTracker = null;
                }

                if (_currentOverlay != null)
                {
                    _currentOverlay.Dispose();
                    _currentOverlay = null;
                }

                _audioSystemEnabled = false;
                _audioSystemHealthy = _audioManager != null;
                UpdateAudioStatus();
                UpdateStatus("Keychain detached. Ready to select another item.");
                AttachButton.IsEnabled = true;
                DetachButton.IsEnabled = false;
                DesktopItemsListBox.IsEnabled = true;
                RefreshButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error detaching keychain: {ex.Message}");
                Debug.WriteLine($"Error detaching keychain: {ex}");
            }
        }

        private void Tracker_ItemLost(object sender, ItemLostEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateStatus($"Tracked item was lost: {e.Reason}");
                DetachKeychain();
            });
        }

        private void Tracker_ItemFound(object sender, ItemFoundEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentTracker != null)
                {
                    UpdateStatus($"Tracking '{_currentTracker.TrackedItem.Name}' - keychain is following!");
                }
            });
        }

        private void SystemActionAudioLayer_ActionDetected(object sender, AudioActionEventArgs e)
        {
            if (_audioManager == null || _currentTracker == null || string.IsNullOrWhiteSpace(e.ItemName))
            {
                _audioSystemHealthy = false;
                UpdateAudioStatus();
                return;
            }

            string trackedName = _currentTracker.TrackedItem?.Name;
            if (!string.Equals(trackedName, e.ItemName, StringComparison.OrdinalIgnoreCase) ||
                _assignmentService == null || _assignmentService.GetAssignment(e.ItemName) == null)
            {
                return;
            }

            _audioSystemEnabled = true;
            bool soundPlayed = e.Action switch
            {
                DesktopAudioAction.Open => _audioManager.PlayOpen(),
                DesktopAudioAction.Rename => _audioManager.PlayRename(),
                DesktopAudioAction.Delete => _audioManager.PlayDelete(),
                DesktopAudioAction.Copy => _audioManager.PlayCopy(),
                DesktopAudioAction.Paste => _audioManager.PlayPaste(),
                DesktopAudioAction.Move => _audioManager.PlayMove(),
                DesktopAudioAction.DragStart => _audioManager.PlayDrag(),
                DesktopAudioAction.Drop => _audioManager.PlayDrop(),
                _ => false,
            };

            _audioSystemHealthy = soundPlayed;
            UpdateAudioStatus();

            if (soundPlayed)
            {
                Debug.WriteLine($"Audio triggered for attached item '{trackedName}' action={e.Action}");
            }
        }

        private void DesktopItemsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            AttachButton.IsEnabled = DesktopItemsListBox.SelectedItem != null;
        }

        private void UpdateStatus(string message = null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                StatusTextBlock.Text = message;
            }
        }

        private void UpdateAudioStatus()
        {
            string state = _audioSystemEnabled && _audioSystemHealthy ? "Enabled | Working" : _audioSystemEnabled ? "Enabled | Waiting" : "Disabled";
            AudioStatusTextBlock.Text = $"Audio: {state}";
            AudioStatusTextBlock.Foreground = _audioSystemEnabled && _audioSystemHealthy
                ? System.Windows.Media.Brushes.DarkGreen
                : _audioSystemEnabled
                    ? System.Windows.Media.Brushes.DarkOrange
                    : System.Windows.Media.Brushes.DarkRed;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _statusUpdateTimer?.Stop();
                DetachKeychain();
                if (_systemActionAudioLayer != null)
                {
                    _systemActionAudioLayer.ActionDetected -= SystemActionAudioLayer_ActionDetected;
                    _systemActionAudioLayer.Dispose();
                    _systemActionAudioLayer = null;
                }
                _audioManager?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during window close: {ex}");
            }
        }
    }
}
