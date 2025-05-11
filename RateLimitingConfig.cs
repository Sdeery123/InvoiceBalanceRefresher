using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace InvoiceBalanceRefresher
{
    /// <summary>
    /// Stores configuration settings for API rate limiting
    /// </summary>
    [Serializable]
    public class RateLimitingConfig
    {
        // Default values
        private const int DEFAULT_INTERVAL_MS = 500;
        private const int DEFAULT_THRESHOLD = 50;
        private const int DEFAULT_COOLDOWN_MS = 5000;
        private const string CONFIG_FILENAME = "ratelimit_config.xml";

        // Request interval settings
        public int RequestIntervalMs { get; set; } = DEFAULT_INTERVAL_MS;
        
        // Threshold settings
        public int RequestCountThreshold { get; set; } = DEFAULT_THRESHOLD;
        public int ThresholdCooldownMs { get; set; } = DEFAULT_COOLDOWN_MS;
        
        // Retry settings for 429 responses
        public int RateLimitRetryDelayMs { get; set; } = 5000;
        
        // Flag to enable/disable rate limiting
        public bool RateLimitingEnabled { get; set; } = true;

        /// <summary>
        /// Loads the rate limiting configuration from file
        /// </summary>
        /// <returns>A RateLimitingConfig object with saved or default settings</returns>
        public static RateLimitingConfig Load()
        {
            try
            {
                if (File.Exists(CONFIG_FILENAME))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(RateLimitingConfig));
                    using (FileStream fs = new FileStream(CONFIG_FILENAME, FileMode.Open))
                    {
                        var config = serializer.Deserialize(fs) as RateLimitingConfig;
                        return config ?? new RateLimitingConfig();
                    }
                }
            }
            catch (Exception)
            {
                // If loading fails, we'll return a default config
            }

            return new RateLimitingConfig();
        }


        /// <summary>
        /// Saves the rate limiting configuration to file
        /// </summary>
        public async Task SaveAsync()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(RateLimitingConfig));
                using (FileStream fs = new FileStream(CONFIG_FILENAME, FileMode.Create))
                {
                    await Task.Run(() => serializer.Serialize(fs, this));
                }
            }
            catch (Exception)
            {
                // Log error or notify user if saving fails
            }
        }


        /// <summary>
        /// Resets all settings to default values
        /// </summary>
        public void ResetToDefaults()
        {
            RequestIntervalMs = DEFAULT_INTERVAL_MS;
            RequestCountThreshold = DEFAULT_THRESHOLD;
            ThresholdCooldownMs = DEFAULT_COOLDOWN_MS;
            RateLimitRetryDelayMs = 5000;
            RateLimitingEnabled = true;
        }
    }
}