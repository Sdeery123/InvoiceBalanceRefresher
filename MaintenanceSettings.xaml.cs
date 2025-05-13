using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32; // For OpenFileDialog

namespace InvoiceBalanceRefresher
{
    public partial class MaintenanceSettings : Window
    {
        private readonly MaintenanceConfig _config;
        private readonly Action<MainWindow.LogLevel, string> _logAction;

        public MaintenanceSettings(MaintenanceConfig config, Action<MainWindow.LogLevel, string> logAction)
        {
            InitializeComponent();

            _config = config ?? new MaintenanceConfig();
            _logAction = logAction;

            // Load config values into UI
            LoadConfigToUI();
        }

        private void LoadConfigToUI()
        {
            LogRetentionDaysTextBox.Text = _config.LogRetentionDays.ToString();
            LogDirectoryTextBox.Text = _config.LogDirectory;
            MaxSessionFilesTextBox.Text = _config.MaxSessionFilesPerDay.ToString();
            EnableLogCleanupCheckBox.IsChecked = _config.EnableLogCleanup;
            EnableOrphanedTaskCleanupCheckBox.IsChecked = _config.EnableOrphanedTaskCleanup;
            EnablePeriodicMaintenanceCheckBox.IsChecked = _config.EnablePeriodicMaintenance;

            // Set frequency dropdown
            switch (_config.MaintenanceFrequency)
            {
                case MaintenanceFrequency.Daily:
                    MaintenanceFrequencyComboBox.SelectedIndex = 1;
                    break;
                case MaintenanceFrequency.Weekly:
                    MaintenanceFrequencyComboBox.SelectedIndex = 2;
                    break;
                case MaintenanceFrequency.Monthly:
                    MaintenanceFrequencyComboBox.SelectedIndex = 3;
                    break;
                case MaintenanceFrequency.EveryStartup:
                default:
                    MaintenanceFrequencyComboBox.SelectedIndex = 0;
                    break;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
                return;

            // Update config object with values from UI
            _config.LogRetentionDays = int.Parse(LogRetentionDaysTextBox.Text);
            _config.LogDirectory = LogDirectoryTextBox.Text;
            _config.MaxSessionFilesPerDay = int.Parse(MaxSessionFilesTextBox.Text);
            _config.EnableLogCleanup = EnableLogCleanupCheckBox.IsChecked ?? false;
            _config.EnableOrphanedTaskCleanup = EnableOrphanedTaskCleanupCheckBox.IsChecked ?? false;
            _config.EnablePeriodicMaintenance = EnablePeriodicMaintenanceCheckBox.IsChecked ?? false;

            // Save frequency setting
            // Save frequency setting
            if (MaintenanceFrequencyComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tagValue && !string.IsNullOrEmpty(tagValue))
            {
                _config.MaintenanceFrequency = (MaintenanceFrequency)Enum.Parse(typeof(MaintenanceFrequency), tagValue);
            }
            else
            {
                _logAction?.Invoke(MainWindow.LogLevel.Warning, "Invalid or missing maintenance frequency selection.");
            }


            // Save config to file
            if (_config.Save())
            {
                _logAction?.Invoke(MainWindow.LogLevel.Info, "Maintenance settings saved successfully.");
            }
            else
            {
                _logAction?.Invoke(MainWindow.LogLevel.Warning, "Failed to save maintenance settings file.");
            }

            DialogResult = true;
            Close();
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Use a helper method to show a folder browser dialog
            string selectedPath = ShowFolderBrowserDialog(LogDirectoryTextBox.Text);

            if (!string.IsNullOrEmpty(selectedPath))
            {
                LogDirectoryTextBox.Text = selectedPath;
            }
        }

        private string ShowFolderBrowserDialog(string initialFolder)
        {
            var dialog = new System.Windows.Controls.TextBox
            {
                Text = initialFolder
            };

            // We'll use the CommonOpenFileDialog from Microsoft.WindowsAPICodePack.Dialogs
            // Since we don't want to add external dependencies, let's create our own simple folder browser

            // Create a new instance of SaveFileDialog - we'll use this as a workaround
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Select Log Directory",
                FileName = "FolderSelection", // This will be ignored since we're just using it to select a folder
                Filter = "Folder|*.folder",
                CheckPathExists = true,
                OverwritePrompt = false,
                ValidateNames = false
            };

            // Set initial directory if it exists
            if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
            {
                saveDialog.InitialDirectory = initialFolder;
            }

            if (saveDialog.ShowDialog() == true && !string.IsNullOrEmpty(saveDialog.FileName))
            {
                // Return the directory path, not the file path
                return Path.GetDirectoryName(saveDialog.FileName) ?? string.Empty;
            }

            return string.Empty;

        }

        private bool ValidateInputs()
        {
            // Validate Log Retention Days
            if (!int.TryParse(LogRetentionDaysTextBox.Text, out int retentionDays) || retentionDays < 1)
            {
                System.Windows.MessageBox.Show("Please enter a valid number for log retention days (minimum 1).",
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                LogRetentionDaysTextBox.Focus();
                return false;
            }

            // Validate Log Directory
            if (string.IsNullOrWhiteSpace(LogDirectoryTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please enter a valid log directory path.",
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                LogDirectoryTextBox.Focus();
                return false;
            }

            // Create directory if it doesn't exist
            try
            {
                if (!Directory.Exists(LogDirectoryTextBox.Text))
                {
                    Directory.CreateDirectory(LogDirectoryTextBox.Text);
                    _logAction?.Invoke(MainWindow.LogLevel.Info, $"Created log directory: {LogDirectoryTextBox.Text}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not create log directory: {ex.Message}",
                    "Directory Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogDirectoryTextBox.Focus();
                return false;
            }

            // Validate Max Session Files
            if (!int.TryParse(MaxSessionFilesTextBox.Text, out int maxFiles) || maxFiles < 1)
            {
                System.Windows.MessageBox.Show("Please enter a valid number for maximum session files per day (minimum 1).",
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                MaxSessionFilesTextBox.Focus();
                return false;
            }

            return true;
        }
    }
}
