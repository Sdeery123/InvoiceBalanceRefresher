using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using System.Windows;
using System.Collections.Generic;
using System.Linq;

namespace InvoiceBalanceRefresher
{
    public class CredentialManager
    {
        private static readonly string _credentialsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InvoiceBalanceRefresher",
            "credentials.dat");

        private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("InvoiceBalancerCredentialProtection");

        /// <summary>
        /// Saves a credential set with the specified name
        /// </summary>
        public static void SaveCredentialSet(string name, string billerGUID, string webServiceKey)
        {
            try
            {
                // Create directory if it doesn't exist
                string? directory = Path.GetDirectoryName(_credentialsFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create or update credential set
                var credentialSet = new CredentialSet
                {
                    Name = name,
                    BillerGUID = billerGUID,
                    WebServiceKey = webServiceKey
                };

                // Get existing credentials
                var credentialSets = GetAllCredentialSets();

                // Remove existing credential with same name if exists
                credentialSets.RemoveAll(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                // Add the new credential set
                credentialSets.Add(credentialSet);

                SaveAllCredentialSets(credentialSets);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save credential set: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Loads a specific credential set by name
        /// </summary>
        public static CredentialSet? LoadCredentialSet(string name)
        {
            try
            {
                var credentialSets = GetAllCredentialSets();
                return credentialSets.FirstOrDefault(c =>
                    c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load credential set: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Gets all saved credential sets
        /// </summary>
        public static List<CredentialSet> GetAllCredentialSets()
        {
            try
            {
                if (!File.Exists(_credentialsFilePath))
                {
                    return new List<CredentialSet>();
                }

                // Read encrypted data from file
                byte[] encryptedData = File.ReadAllBytes(_credentialsFilePath);

                // Decrypt the data
                byte[] decryptedData = ProtectedData.Unprotect(
                    encryptedData,
                    _entropy,
                    DataProtectionScope.CurrentUser);

                string credentialsXml = Encoding.UTF8.GetString(decryptedData);

                // Deserialize XML to credentials list
                var serializer = new XmlSerializer(typeof(List<CredentialSet>));
                using (var stringReader = new StringReader(credentialsXml))
                {
                    var result = serializer.Deserialize(stringReader) as List<CredentialSet>;
                    return result ?? new List<CredentialSet>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load credential sets: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<CredentialSet>();
            }
        }

        /// <summary>
        /// Deletes a credential set by name
        /// </summary>
        public static bool DeleteCredentialSet(string name)
        {
            try
            {
                var credentialSets = GetAllCredentialSets();
                int initialCount = credentialSets.Count;
                credentialSets.RemoveAll(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (credentialSets.Count < initialCount)
                {
                    SaveAllCredentialSets(credentialSets);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete credential set: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// For backward compatibility - loads the default credential
        /// </summary>
        public static Credentials? LoadCredentials()
        {
            var credentialSets = GetAllCredentialSets();
            var defaultSet = credentialSets.FirstOrDefault();

            if (defaultSet != null)
            {
                return new Credentials
                {
                    BillerGUID = defaultSet.BillerGUID,
                    WebServiceKey = defaultSet.WebServiceKey
                };
            }

            return null;
        }

        /// <summary>
        /// For backward compatibility - saves to the default credential
        /// </summary>
        public static void SaveCredentials(string billerGUID, string webServiceKey)
        {
            SaveCredentialSet("Default", billerGUID, webServiceKey);
        }

        // Private helper method to save all credential sets
        private static void SaveAllCredentialSets(List<CredentialSet> credentialSets)
        {
            // Serialize credentials to XML
            var serializer = new XmlSerializer(typeof(List<CredentialSet>));
            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, credentialSets);
                string credentialsXml = stringWriter.ToString();

                // Encrypt the XML
                byte[] credentialsBytes = Encoding.UTF8.GetBytes(credentialsXml);
                byte[] encryptedData = ProtectedData.Protect(
                    credentialsBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser);

                // Save encrypted data to file
                File.WriteAllBytes(_credentialsFilePath, encryptedData);
            }
        }

        [Serializable]
        public class CredentialSet
        {
            public string Name { get; set; } = "";
            public string BillerGUID { get; set; } = "";
            public string WebServiceKey { get; set; } = "";

            // For displaying in UI
            public override string ToString() => Name;
        }

        // Keep the original Credentials class for backward compatibility
        [Serializable]
        public class Credentials
        {
            public string BillerGUID { get; set; } = string.Empty;
            public string WebServiceKey { get; set; } = string.Empty;
        }
    }
}
