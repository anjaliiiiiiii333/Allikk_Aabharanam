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
    /// <summary>
    /// Main application window for the Desktop Keychain Accessory.
    /// Provides UI for selecting desktop items and managing keychain overlays.
    /// </summary>
    public partial class MainWindow : Window
    {
        private DesktopIconDetector _detector;
        private KeychainTracker _currentTracker;
        private KeychainOverlay _currentOverlay;
        private DispatcherTimer _statusUpdateTimer;
        private ObservableCollection<DesktopItem> _desktopItems;
        private AccessoryAssignmentService _assignmentService;
        private IReadOnlyList<AccessoryAsset> _accessories;

        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        /// <summary>
        /// Initializes the application components.
        /// Generates the keychain asset if needed and sets up the detector.
        /// </summary>
        private void Initialize()
        {
            try
            {
                // Generate keychain asset
                string assetPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets",
                    "keychain.png");
                AssetGenerator.GenerateKeychainAsset(assetPath);

                // Initialize detector
                _detector = new DesktopIconDetector();
                _assignmentService = new AccessoryAssignmentService();
                _accessories = _assignmentService.LoadAvailableAccessories();

                // Initialize items collection
                _desktopItems = new ObservableCollection<DesktopItem>();
                DesktopItemsListBox.ItemsSource = _desktopItems;

                // Setup status update timer
                _statusUpdateTimer = new DispatcherTimer();
                _statusUpdateTimer.Interval = TimeSpan.FromSeconds(1);
                _statusUpdateTimer.Tick += (s, e) => UpdateStatus();

                UpdateStatus("Initialized. Ready to detect desktop items.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Initialization error: {ex.Message}");
                Debug.WriteLine($"Initialization error: {ex}");
            }
        }

        /// <summary>
        /// Refreshes the list of desktop items.
        /// Called when the user clicks the "Refresh Desktop Items" button.
        /// </summary>
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
                AttachButton.IsEnabled = false; // Disable until an item is selected
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error refreshing desktop items: {ex.Message}");
                Debug.WriteLine($"Error refreshing items: {ex}");
            }
        }

        /// <summary>
        /// Attaches a keychain overlay to the selected desktop item.
        /// Called when the user clicks the "Attach Keychain" button.
        /// </summary>
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
                // Detach any existing keychain
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

                // Create overlay
                _currentOverlay = new KeychainOverlay(keychainBitmap);

                // Place the overlay at the item's current desktop coordinates.
                _currentOverlay.SetPosition(
                    itemBounds.Left,
                    itemBounds.Top,
                    itemBounds.Width,
                    itemBounds.Height);

                _currentTracker = new KeychainTracker(_detector, _currentOverlay, selectedItem);
                _currentTracker.ItemLost += Tracker_ItemLost;
                _currentTracker.ItemFound += Tracker_ItemFound;
                _currentTracker.StartTracking();

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

        /// <summary>
        /// Detaches the keychain overlay from the current item.
        /// Called when the user clicks the "Detach Keychain" button.
        /// </summary>
        private void DetachButton_Click(object sender, RoutedEventArgs e)
        {
            DetachKeychain();
        }

        /// <summary>
        /// Internal method to detach the keychain and clean up resources.
        /// </summary>
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

        /// <summary>
        /// Handles the event when a tracked item is lost.
        /// </summary>
        private void Tracker_ItemLost(object sender, ItemLostEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateStatus($"Tracked item was lost: {e.Reason}");
                DetachKeychain();
            });
        }

        /// <summary>
        /// Handles the event when a tracked item is found again.
        /// </summary>
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

        /// <summary>
        /// Handles the ListBox selection changed event.
        /// Enables the Attach button when an item is selected.
        /// </summary>
        private void DesktopItemsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            AttachButton.IsEnabled = DesktopItemsListBox.SelectedItem != null;
        }

        /// <summary>
        /// Updates the status text block.
        /// </summary>
        private void UpdateStatus(string message = null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                StatusTextBlock.Text = message;
            }
        }

        /// <summary>
        /// Handles the window closing event.
        /// Ensures all overlays and trackers are cleaned up.
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _statusUpdateTimer?.Stop();
                DetachKeychain();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during window close: {ex}");
            }
        }
    }
}
