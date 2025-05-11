using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace InvoiceBalanceRefresher
{
    public partial class CredentialManagementWindow : Window
    {
        private List<CredentialManager.CredentialSet> _credentialSets;
        private bool _isEditing = false;

        public CredentialManagementWindow()
        {
            InitializeComponent();
            _credentialSets = new List<CredentialManager.CredentialSet>(); // Initialize the field
            LoadCredentialSets();
        }


        private void LoadCredentialSets()
        {
            _credentialSets = CredentialManager.GetAllCredentialSets();
            CredentialSetListBox.ItemsSource = null;
            CredentialSetListBox.ItemsSource = _credentialSets;
            
            // Clear input fields
            CredentialNameTextBox.Text = string.Empty;
            BillerGUIDTextBox.Text = string.Empty;
            WebServiceKeyTextBox.Text = string.Empty;
            
            // Disable editing controls until a credential is selected or "New" is clicked
            SetControlsEnabled(false);
        }
        
        private void SetControlsEnabled(bool enabled)
        {
            CredentialNameTextBox.IsEnabled = enabled;
            BillerGUIDTextBox.IsEnabled = enabled;
            WebServiceKeyTextBox.IsEnabled = enabled;
            SaveButton.IsEnabled = enabled;
        }
        
        private void CredentialSetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCredentialSet = CredentialSetListBox.SelectedItem as CredentialManager.CredentialSet;
            if (selectedCredentialSet != null)
            {
                // Load the selected credential set into the editor
                CredentialNameTextBox.Text = selectedCredentialSet.Name;
                BillerGUIDTextBox.Text = selectedCredentialSet.BillerGUID;
                WebServiceKeyTextBox.Text = selectedCredentialSet.WebServiceKey;
                
                _isEditing = true;
                SetControlsEnabled(true);
            }
        }
        
        private void NewCredentialSet_Click(object sender, RoutedEventArgs e)
        {
            // Clear input fields for a new credential set
            CredentialNameTextBox.Text = string.Empty;
            BillerGUIDTextBox.Text = string.Empty;
            WebServiceKeyTextBox.Text = string.Empty;
            
            CredentialSetListBox.SelectedItem = null;
            _isEditing = false;
            SetControlsEnabled(true);
            CredentialNameTextBox.Focus();
        }
        
        private void DeleteCredentialSet_Click(object sender, RoutedEventArgs e)
        {
            var selectedCredentialSet = CredentialSetListBox.SelectedItem as CredentialManager.CredentialSet;
            if (selectedCredentialSet == null)
            {
                MessageBox.Show("Please select a credential set to delete.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Confirm deletion
            var result = MessageBox.Show($"Are you sure you want to delete the credential set '{selectedCredentialSet.Name}'?",
                "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                // Delete the credential set
                if (CredentialManager.DeleteCredentialSet(selectedCredentialSet.Name))
                {
                    LoadCredentialSets();
                }
            }
        }
        
        private void SaveCredentialSet_Click(object sender, RoutedEventArgs e)
        {
            string name = CredentialNameTextBox.Text.Trim();
            string billerGUID = BillerGUIDTextBox.Text.Trim();
            string webServiceKey = WebServiceKeyTextBox.Text.Trim();
            
            // Validate inputs
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a name for this credential set.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!ValidationHelper.ValidateGUID(billerGUID))
            {
                MessageBox.Show("Please enter a valid Biller GUID.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!ValidationHelper.ValidateGUID(webServiceKey))
            {
                MessageBox.Show("Please enter a valid Web Service Key.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Check for duplicate name if adding new
            if (!_isEditing)
            {
                bool nameExists = _credentialSets.Exists(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (nameExists)
                {
                    MessageBox.Show($"A credential set with the name '{name}' already exists. Please choose a different name.",
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            
            // Save the credential set
            CredentialManager.SaveCredentialSet(name, billerGUID, webServiceKey);
            
            // Refresh the list
            LoadCredentialSets();
            
            MessageBox.Show("Credential set saved successfully.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}