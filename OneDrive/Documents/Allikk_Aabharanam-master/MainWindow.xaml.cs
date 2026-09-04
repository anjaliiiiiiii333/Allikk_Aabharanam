using System;
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
        private AudioManager _audioManager;
        private DispatcherTimer _statusUpdateTimer;
        private ObservableCollection<DesktopItem> _desktopItems;

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
                _audioManager = new AudioManager();

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

            try
            {
                // Detach any existing keychain
                DetachKeychain();

                UpdateStatus($"Attaching keychain to '{selectedItem.Name}'...");

                // Load the keychain bitmap
                string assetPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets",
                    "keychain.png");

                if (!File.Exists(assetPath))
                {
                    UpdateStatus($"Keychain asset not found at {assetPath}");
                    return;
                }

                Bitmap keychainBitmap = new Bitmap(assetPath);

                // Create overlay
                _currentOverlay = new KeychainOverlay(keychainBitmap, offsetX: 10, offsetY: 10);

                // Place the overlay at the item's current desktop coordinates.
                Rect itemBounds = selectedItem.BoundingRectangle;
                _currentOverlay.SetPosition(
                    itemBounds.Left,
                    itemBounds.Top,
                    itemBounds.Width,
                    itemBounds.Height);

                bool attachAudioStarted = _audioManager.PlayOpen();
                Debug.WriteLine($"Attach audio diagnostic: Faaah.mp3 started={attachAudioStarted} item='{selectedItem.Name}'");

                _currentTracker = new KeychainTracker(_detector, _currentOverlay, selectedItem);
                _currentTracker.ItemLost += Tracker_ItemLost;
                _currentTracker.ItemFound += Tracker_ItemFound;
                _currentTracker.ActionTriggered += Tracker_ActionTriggered;
                _currentTracker.StartTracking();

                UpdateStatus(attachAudioStarted
                    ? $"Keychain attached to '{selectedItem.Name}'. Audio test started."
                    : $"Keychain attached to '{selectedItem.Name}', but audio failed. See audio.log.");
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
                    _currentTracker.ActionTriggered -= Tracker_ActionTriggered;
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

        private void Tracker_ActionTriggered(object sender, AudioActionEventArgs e)
        {
            if (_audioManager == null || _currentTracker == null)
                return;

            var action = e.Action;
            Debug.WriteLine($"[{DateTime.UtcNow:O}] [OPEN_TRIGGER_LOG] Tracker_ActionTriggered fired: action={action} trackedItem='{_currentTracker.TrackedItem?.Name}'");

            bool soundPlayed = action switch
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

            if (soundPlayed)
            {
                Debug.WriteLine($"[{DateTime.UtcNow:O}] [OPEN_TRIGGER_LOG] Audio triggered for attached desktop item: {action}");
            }
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
