using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InvoiceBalanceRefresher
{
    /// <summary>
    /// Service class for handling Invoice Cloud API communication
    /// </summary>
    public class InvoiceCloudApiService
    {
        private readonly Action<LogLevel, string> _logAction;
        private const int MaxRetries = 3;
        private const int InitialRetryDelayMs = 1000;

        // Rate limiting parameters
        private static readonly SemaphoreSlim _throttleSemaphore = new SemaphoreSlim(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static int _requestCount = 0;

        // Configuration settings - will be loaded from config
        // Declare the field as nullable to satisfy the compiler
        private static RateLimitingConfig? _rateLimitConfig;

        // Update the constructor to ensure the field is initialized if null
        public InvoiceCloudApiService(Action<LogLevel, string> logAction)
        {
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));

            // Load rate limiting config if not already loaded
            if (_rateLimitConfig == null)
            {
                _rateLimitConfig = RateLimitingConfig.Load();
            }
        }


        /// <summary>
        /// Updates the rate limiting configuration settings
        /// </summary>
        public static void UpdateRateLimitConfig(RateLimitingConfig config)
        {
            if (config != null)
            {
                _rateLimitConfig = config;
            }
        }

        /// <summary>
        /// Applies rate limiting to ensure we don't overload the API
        /// </summary>
        private async Task ApplyRateLimiting()
        {
            // Skip rate limiting if disabled
            if (_rateLimitConfig == null || !_rateLimitConfig.RateLimitingEnabled)
            {
                return;
            }

            await _throttleSemaphore.WaitAsync();
            try
            {
                // Calculate time since last request
                TimeSpan timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;

                // If we've made a recent request, wait until the minimum interval has passed
                if (timeSinceLastRequest < TimeSpan.FromMilliseconds(_rateLimitConfig.RequestIntervalMs))
                {
                    int delayMs = (int)(TimeSpan.FromMilliseconds(_rateLimitConfig.RequestIntervalMs) - timeSinceLastRequest).TotalMilliseconds;
                    _logAction(LogLevel.Debug, $"Rate limiting: Waiting {delayMs}ms before next request");
                    await Task.Delay(delayMs);
                }

                // Increment request counter and apply additional cooling period if threshold reached
                _requestCount++;
                if (_requestCount >= _rateLimitConfig.RequestCountThreshold)
                {
                    _logAction(LogLevel.Info,
                        $"Rate limiting: Reached {_rateLimitConfig.RequestCountThreshold} requests, " +
                        $"applying cooldown period of {_rateLimitConfig.ThresholdCooldownMs}ms");
                    await Task.Delay(_rateLimitConfig.ThresholdCooldownMs);
                    _requestCount = 0;
                }

                // Update last request time to now
                _lastRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _throttleSemaphore.Release();
            }
        }

        /// <summary>
        /// Retrieves invoice information by invoice number
        /// </summary>
        /// <param name="billerGUID">The biller GUID for authentication</param>
        /// <param name="webServiceKey">The web service key for authentication</param>
        /// <param name="invoiceNumber">The invoice number to look up</param>
        /// <returns>Formatted invoice information</returns>
        public async Task<string> GetInvoiceByNumber(string billerGUID, string webServiceKey, string invoiceNumber)
        {
            // Apply rate limiting before making the request
            await ApplyRateLimiting();

            string soapRequest = $@"
<soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>
    <soap12:Body>
        <ViewInvoiceByInvoiceNumber xmlns='https://www.invoicecloud.com/portal/webservices/CloudInvoicing/'>
            <Req>
                <BillerGUID>{billerGUID}</BillerGUID>
                <WebServiceKey>{webServiceKey}</WebServiceKey>
                <InvoiceNumber>{invoiceNumber}</InvoiceNumber>
            </Req>
        </ViewInvoiceByInvoiceNumber>
    </soap12:Body>
</soap12:Envelope>";

            // Configure the HttpClient with appropriate timeout
            using (HttpClient client = new HttpClient())
            {
                // Set reasonable timeout and headers
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.ExpectContinue = false; // Can help with some SOAP services

                // Use retry pattern to handle intermittent failures
                int currentRetry = 0;
                int retryDelayMs = InitialRetryDelayMs;

                while (true)
                {
                    try
                    {
                        _logAction(LogLevel.Debug, $"Sending request for invoice {invoiceNumber} (Attempt {currentRetry + 1} of {MaxRetries})");

                        var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");
                        var response = await client.PostAsync("https://www.invoicecloud.com/portal/webservices/CloudInvoicing.asmx", content);

                        // Check HTTP status code
                        if (!response.IsSuccessStatusCode)
                        {
                            _logAction(LogLevel.Warning, $"HTTP error: {(int)response.StatusCode} {response.StatusCode} for invoice {invoiceNumber}");

                            // For rate limiting specific error codes (429 Too Many Requests)
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                int retryDelay = _rateLimitConfig != null ? _rateLimitConfig.RateLimitRetryDelayMs : 5000;
                                _logAction(LogLevel.Warning, $"Rate limit hit (429). Adding additional delay of {retryDelay}ms before retry.");
                                await Task.Delay(retryDelay);
                                throw new HttpRequestException("Rate limit exceeded");
                            }

                            // For specific error codes that might be temporary, we'll retry
                            if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                            {
                                throw new HttpRequestException($"Server error: {response.StatusCode}");
                            }
                        }

                        var responseText = await response.Content.ReadAsStringAsync();

                        // Verify the response actually contains XML data
                        if (string.IsNullOrWhiteSpace(responseText) || !responseText.Contains("<"))
                        {
                            _logAction(LogLevel.Warning, $"Empty or non-XML response received for invoice {invoiceNumber}");
                            throw new FormatException("Invalid response format received");
                        }

                        _logAction(LogLevel.Debug, $"Received response for invoice {invoiceNumber}");
                        return ParseInvoiceResponse(responseText);
                    }
                    catch (Exception ex) when (ex is HttpRequestException ||
                                           ex is TaskCanceledException ||
                                           ex is TimeoutException ||
                                           ex is FormatException)
                    {
                        currentRetry++;
                        _logAction(LogLevel.Warning, $"API call attempt {currentRetry} failed: {ex.Message}");

                        if (currentRetry >= MaxRetries)
                        {
                            _logAction(LogLevel.Error, $"All retry attempts failed for invoice {invoiceNumber}. Last error: {ex.Message}");
                            throw new Exception($"Failed to retrieve invoice data after {MaxRetries} attempts. Last error: {ex.Message}", ex);
                        }

                        // Exponential backoff for retries
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2; // Double the delay for each retry
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves and updates customer record information by account number
        /// </summary>
        /// <param name="billerGUID">The biller GUID for authentication</param>
        /// <param name="webServiceKey">The web service key for authentication</param>
        /// <param name="accountNumber">The account number to look up</param>
        /// <returns>Formatted customer record information</returns>
        public async Task<string> GetCustomerRecord(string billerGUID, string webServiceKey, string accountNumber)
        {
            // Apply rate limiting before making the request
            await ApplyRateLimiting();

            string soapRequest = $@"
<soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>
  <soap12:Body>
    <ViewCustomerRecord xmlns='https://www.invoicecloud.com/portal/webservices/CloudManagement/'>
      <BillerGUID>{billerGUID}</BillerGUID>
      <WebServiceKey>{webServiceKey}</WebServiceKey>
      <AccountNumber>{accountNumber}</AccountNumber>
    </ViewCustomerRecord>
  </soap12:Body>
</soap12:Envelope>";

            // Configure the HttpClient with appropriate timeout
            using (HttpClient client = new HttpClient())
            {
                // Set reasonable timeout and headers
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.ExpectContinue = false;

                // Use retry pattern to handle intermittent failures
                int currentRetry = 0;
                int retryDelayMs = InitialRetryDelayMs;

                while (true)
                {
                    try
                    {
                        _logAction(LogLevel.Debug, $"Sending customer record request for account {accountNumber} (Attempt {currentRetry + 1} of {MaxRetries})");

                        var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");
                        var response = await client.PostAsync("https://www.invoicecloud.com/portal/webservices/CloudManagement.asmx", content);

                        // Check HTTP status code
                        if (!response.IsSuccessStatusCode)
                        {
                            _logAction(LogLevel.Warning, $"HTTP error: {(int)response.StatusCode} {response.StatusCode} for account {accountNumber}");

                            // For rate limiting specific error codes (429 Too Many Requests)
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                int retryDelay = _rateLimitConfig != null ? _rateLimitConfig.RateLimitRetryDelayMs : 5000;
                                _logAction(LogLevel.Warning, $"Rate limit hit (429). Adding additional delay of {retryDelay}ms before retry.");
                                await Task.Delay(retryDelay);
                                throw new HttpRequestException("Rate limit exceeded");
                            }

                            // For specific error codes that might be temporary, we'll retry
                            if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                            {
                                throw new HttpRequestException($"Server error: {response.StatusCode}");
                            }
                        }

                        var responseText = await response.Content.ReadAsStringAsync();

                        // Verify the response actually contains XML data
                        if (string.IsNullOrWhiteSpace(responseText) || !responseText.Contains("<"))
                        {
                            _logAction(LogLevel.Warning, $"Empty or non-XML response received for account {accountNumber}");
                            throw new FormatException("Invalid response format received");
                        }

                        _logAction(LogLevel.Debug, $"Received customer record response for account {accountNumber}");
                        return ParseCustomerRecordResponse(responseText);
                    }
                    catch (Exception ex) when (ex is HttpRequestException ||
                                           ex is TaskCanceledException ||
                                           ex is TimeoutException ||
                                           ex is FormatException)
                    {
                        currentRetry++;
                        _logAction(LogLevel.Warning, $"API call attempt {currentRetry} failed: {ex.Message}");

                        if (currentRetry >= MaxRetries)
                        {
                            _logAction(LogLevel.Error, $"All retry attempts failed for account {accountNumber}. Last error: {ex.Message}");
                            throw new Exception($"Failed to retrieve customer data after {MaxRetries} attempts. Last error: {ex.Message}", ex);
                        }

                        // Exponential backoff for retries
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2; // Double the delay for each retry
                    }
                }
            }
        }

        #region Response Parsing Methods

        private string ParseInvoiceResponse(string responseText)
        {
            try
            {
                // Check for top-level SOAP fault first
                if (responseText.Contains("<soap12:Fault") || responseText.Contains("<Fault"))
                {
                    _logAction(LogLevel.Warning, "SOAP Fault detected in response");

                    // Try to extract fault reason
                    string faultReason = "Unknown fault";
                    int reasonStart = responseText.IndexOf("<faultstring>") + "<faultstring>".Length;
                    int reasonEnd = responseText.IndexOf("</faultstring>");

                    if (reasonStart > 0 && reasonEnd > reasonStart)
                    {
                        faultReason = responseText.Substring(reasonStart, reasonEnd - reasonStart);
                        _logAction(LogLevel.Warning, $"Fault reason: {faultReason}");
                    }

                    return $"Error: {faultReason}";
                }

                // Check for success response
                if (responseText.Contains("<Success>true</Success>"))
                {
                    try
                    {
                        // Extract all available information from the response
                        StringBuilder result = new StringBuilder();

                        // Status
                        result.AppendLine("Status: Success");

                        // Invoice Number
                        string invoiceNumber = ExtractValue(responseText, "InvoiceNumber");
                        if (!string.IsNullOrEmpty(invoiceNumber))
                            result.AppendLine($"Invoice Number: {invoiceNumber}");

                        // Invoice Date
                        string invoiceDate = ExtractValue(responseText, "InvoiceDate");
                        if (!string.IsNullOrEmpty(invoiceDate))
                        {
                            if (DateTime.TryParse(invoiceDate, out DateTime parsedDate))
                                result.AppendLine($"Invoice Date: {parsedDate:MM/dd/yyyy}");
                            else
                                result.AppendLine($"Invoice Date: {invoiceDate}");
                        }

                        // Due Date
                        string dueDate = ExtractValue(responseText, "InvoiceDueDate");
                        if (!string.IsNullOrEmpty(dueDate))
                        {
                            if (DateTime.TryParse(dueDate, out DateTime parsedDate))
                                result.AppendLine($"Due Date: {parsedDate:MM/dd/yyyy}");
                            else
                                result.AppendLine($"Due Date: {dueDate}");
                        }

                        // AutoPay Collection Date
                        string autoPayDate = ExtractValue(responseText, "AutoPayCollectionDate");
                        if (!string.IsNullOrEmpty(autoPayDate) && autoPayDate != "1/1/0001")
                        {
                            if (DateTime.TryParse(autoPayDate, out DateTime parsedDate))
                                result.AppendLine($"AutoPay Date: {parsedDate:MM/dd/yyyy}");
                            else
                                result.AppendLine($"AutoPay Date: {autoPayDate}");
                        }

                        // Amount fields
                        string totalAmountDue = ExtractValue(responseText, "TotalAmountDue");
                        if (!string.IsNullOrEmpty(totalAmountDue))
                        {
                            if (decimal.TryParse(totalAmountDue, out decimal amount))
                                result.AppendLine($"Total Amount Due: {amount:C}");
                            else
                                result.AppendLine($"Total Amount Due: {totalAmountDue}");
                        }

                        string balanceDue = ExtractValue(responseText, "BalanceDue");
                        if (!string.IsNullOrEmpty(balanceDue))
                        {
                            if (decimal.TryParse(balanceDue, out decimal amount))
                                result.AppendLine($"Balance Due: {amount:C}");
                            else
                                result.AppendLine($"Balance Due: {balanceDue}");
                        }

                        string minimumAmountDue = ExtractValue(responseText, "MinimumAmountDue");
                        if (!string.IsNullOrEmpty(minimumAmountDue))
                        {
                            if (decimal.TryParse(minimumAmountDue, out decimal amount))
                                result.AppendLine($"Minimum Amount Due: {amount:C}");
                            else
                                result.AppendLine($"Minimum Amount Due: {minimumAmountDue}");
                        }

                        // Boolean fields
                        string allowPartialPayments = ExtractValue(responseText, "AllowPartialPayments");
                        if (!string.IsNullOrEmpty(allowPartialPayments))
                        {
                            string displayValue = allowPartialPayments.ToLower() == "true" ? "Yes" : "No";
                            result.AppendLine($"Allow Partial Payments: {displayValue}");
                        }

                        // Notification dates
                        string[] notificationFields = new[]
                        {
                            "FirstNotifyRequestedDate",
                            "SecondNotifyRequestedDate",
                            "ThirdNotifyRequestedDate"
                        };

                        foreach (string field in notificationFields)
                        {
                            string notifyDate = ExtractValue(responseText, field);
                            if (!string.IsNullOrEmpty(notifyDate) && notifyDate != "1/1/0001")
                            {
                                if (DateTime.TryParse(notifyDate, out DateTime parsedDate))
                                {
                                    string displayName = field.Replace("RequestedDate", "");
                                    result.AppendLine($"{displayName}: {parsedDate:MM/dd/yyyy}");
                                }
                            }
                        }

                        return result.ToString();
                    }
                    catch (Exception ex)
                    {
                        _logAction(LogLevel.Error, $"Error parsing data from success response: {ex.Message}");
                        return "Success: true, Error parsing detailed invoice data";
                    }
                }
                else if (responseText.Contains("<Success>false</Success>"))
                {
                    // Try to get error message if available
                    string errorMessage = "No details provided";
                    int messageStart = responseText.IndexOf("<ErrorMessage>") + "<ErrorMessage>".Length;
                    int messageEnd = responseText.IndexOf("</ErrorMessage>");

                    if (messageStart > 0 && messageEnd > messageStart)
                    {
                        errorMessage = responseText.Substring(messageStart, messageEnd - messageStart);
                    }

                    _logAction(LogLevel.Warning, $"API returned Success: false with message: {errorMessage}");
                    return $"Status: Failed\nReason: {errorMessage}";
                }
                else
                {
                    // Unexpected response format
                    _logAction(LogLevel.Warning, "Unexpected response format - could not determine success status");
                    return "Status: Unknown\nResponse did not contain expected format";
                }
            }
            catch (Exception ex)
            {
                _logAction(LogLevel.Error, $"Error parsing response: {ex.Message}");
                return $"Error parsing response: {ex.Message}";
            }
        }

        private string ParseCustomerRecordResponse(string responseText)
        {
            try
            {
                // Add debug logging to see the raw response
                _logAction(LogLevel.Debug, $"Raw customer record XML starts with: {responseText.Substring(0, Math.Min(200, responseText.Length))}");

                // Check for top-level SOAP fault first
                if (responseText.Contains("<soap12:Fault") || responseText.Contains("<Fault"))
                {
                    _logAction(LogLevel.Warning, "SOAP Fault detected in customer record response");

                    // Try to extract fault reason
                    string faultReason = "Unknown fault";
                    int reasonStart = responseText.IndexOf("<faultstring>") + "<faultstring>".Length;
                    int reasonEnd = responseText.IndexOf("</faultstring>");

                    if (reasonStart > 0 && reasonEnd > reasonStart)
                    {
                        faultReason = responseText.Substring(reasonStart, reasonEnd - reasonStart);
                        _logAction(LogLevel.Warning, $"Fault reason: {faultReason}");
                    }

                    return $"Error: {faultReason}";
                }

                // Look for customer data in the response
                if (responseText.Contains("<CustomerID>") || responseText.Contains("<AccountNumber>"))
                {
                    try
                    {
                        StringBuilder result = new StringBuilder();
                        result.AppendLine("CUSTOMER RECORD:");
                        result.AppendLine("---------------");

                        // Extract all customer fields
                        string[] fields = new[] {
                            "CustomerID", "AccountNumber", "CustomerName", "Address1", "Address2",
                            "City", "State", "Zip", "Phone", "EmailAddress", "AutoPay",
                            "PaperInvoices", "Registered", "RegistrationDate", "LoginName"
                        };

                        foreach (var field in fields)
                        {
                            string value = ExtractValue(responseText, field);
                            if (!string.IsNullOrEmpty(value))
                            {
                                // Format boolean values as Yes/No
                                if (field == "AutoPay" || field == "PaperInvoices" || field == "Registered" || field == "Active")
                                {
                                    value = value.ToLower() == "true" ? "Yes" : "No";
                                }
                                // Format dates
                                else if (field.Contains("Date") && DateTime.TryParse(value, out DateTime date) && date != DateTime.MinValue)
                                {
                                    value = date.ToString("MM/dd/yyyy");
                                }

                                result.AppendLine($"{FormatFieldName(field)}: {value}");
                            }
                        }

                        string finalResult = result.ToString();
                        _logAction(LogLevel.Debug, $"Formatted customer record result: {finalResult}");
                        return finalResult;
                    }
                    catch (Exception ex)
                    {
                        _logAction(LogLevel.Error, $"Error parsing customer data: {ex.Message}");
                        return "Error parsing customer data: " + ex.Message;
                    }
                }
                else if (responseText.Contains("ViewCustomerRecordResult") && !responseText.Contains("<CustomerID>"))
                {
                    _logAction(LogLevel.Warning, "No customer record found for this account number");
                    return "No customer record found for this account number.";
                }
                else
                {
                    _logAction(LogLevel.Warning, "Unexpected response format for customer record");
                    return "Unknown status: Response did not contain expected customer data format";
                }
            }
            catch (Exception ex)
            {
                _logAction(LogLevel.Error, $"Error parsing customer response: {ex.Message}");
                return $"Error parsing customer response: {ex.Message}";
            }
        }

        private string FormatFieldName(string fieldName)
        {
            // Insert spaces before capital letters in the middle of the string
            string result = string.Empty;
            for (int i = 0; i < fieldName.Length; i++)
            {
                if (i > 0 && char.IsUpper(fieldName[i]))
                {
                    result += " ";
                }
                result += fieldName[i];
            }
            return result;
        }

        private string ExtractValue(string xml, string tagName)
        {
            string openTag = $"<{tagName}>";
            string closeTag = $"</{tagName}>";

            int startIndex = xml.IndexOf(openTag);
            if (startIndex < 0)
                return string.Empty;

            startIndex += openTag.Length;
            int endIndex = xml.IndexOf(closeTag, startIndex);

            if (endIndex < 0)
                return string.Empty;

            return xml.Substring(startIndex, endIndex - startIndex).Trim();
        }

        #endregion
    }
}