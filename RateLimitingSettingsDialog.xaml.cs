using System;
using System.Windows;
using System.Windows.Controls;

namespace InvoiceBalanceRefresher
{
    public partial class RateLimitingSettingsDialog : Window
    {
        private RateLimitingConfig _config;

        public RateLimitingSettingsDialog(RateLimitingConfig config)
        {
            InitializeComponent();
            _config = config ?? new RateLimitingConfig();
            
            // Populate UI with current settings
            EnableRateLimitingCheckBox.IsChecked = _config.RateLimitingEnabled;
            RequestIntervalTextBox.Text = _config.RequestIntervalMs.ToString();
            RequestThresholdTextBox.Text = _config.RequestCountThreshold.ToString();
            CooldownPeriodTextBox.Text = _config.ThresholdCooldownMs.ToString();
            RateLimitRetryDelayTextBox.Text = _config.RateLimitRetryDelayMs.ToString();
            
            UpdateControlStates();
        }

        private void EnableRateLimitingCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateControlStates();
        }

        private void UpdateControlStates()
        {
            bool enabled = EnableRateLimitingCheckBox.IsChecked ?? false;
            RequestIntervalTextBox.IsEnabled = enabled;
            RequestThresholdTextBox.IsEnabled = enabled;
            CooldownPeriodTextBox.IsEnabled = enabled;
            RateLimitRetryDelayTextBox.IsEnabled = enabled;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate and save settings
                _config.RateLimitingEnabled = EnableRateLimitingCheckBox.IsChecked ?? true;

                if (int.TryParse(RequestIntervalTextBox.Text, out int interval) && interval >= 0)
                {
                    _config.RequestIntervalMs = interval;
                }

                if (int.TryParse(RequestThresholdTextBox.Text, out int threshold) && threshold > 0)
                {
                    _config.RequestCountThreshold = threshold;
                }

                if (int.TryParse(CooldownPeriodTextBox.Text, out int cooldown) && cooldown >= 0)
                {
                    _config.ThresholdCooldownMs = cooldown;
                }

                if (int.TryParse(RateLimitRetryDelayTextBox.Text, out int retryDelay) && retryDelay >= 0)
                {
                    _config.RateLimitRetryDelayMs = retryDelay;
                }

                // Save config to file
                await _config.SaveAsync();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _config.ResetToDefaults();
            
            // Update UI with default values
            EnableRateLimitingCheckBox.IsChecked = _config.RateLimitingEnabled;
            RequestIntervalTextBox.Text = _config.RequestIntervalMs.ToString();
            RequestThresholdTextBox.Text = _config.RequestCountThreshold.ToString();
            CooldownPeriodTextBox.Text = _config.ThresholdCooldownMs.ToString();
            RateLimitRetryDelayTextBox.Text = _config.RateLimitRetryDelayMs.ToString();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}