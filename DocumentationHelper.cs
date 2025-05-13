using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Documents;

namespace InvoiceBalanceRefresher
{
    public static class DocumentationHelper
    {
        public static void ShowDocumentationWindow(Window owner, Action<LogLevel, string> logAction)
        {
            logAction(LogLevel.Info, "Documentation requested");

            // Create documentation window
            var docWindow = new Window
            {
                Title = "Documentation - Invoice Balance Refresher",
                Width = 900,
                Height = 700,
                Background = (SolidColorBrush)owner.FindResource("BackgroundBrush"),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner
            };

            // Main grid
            var mainGrid = new Grid();
            docWindow.Content = mainGrid;

            // Add scanlines overlay
            var scanlinesGrid = new Grid();
            scanlinesGrid.SetValue(System.Windows.Controls.Panel.ZIndexProperty, -1);
            scanlinesGrid.Background = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 2, 2),
                ViewportUnits = BrushMappingMode.Absolute,
                Opacity = 0.07,
                Drawing = new DrawingGroup
                {
                    Children =
                    {
                        new GeometryDrawing
                        {
                            Brush = System.Windows.Media.Brushes.Transparent,
                            Geometry = new RectangleGeometry(new Rect(0, 0, 2, 2))
                        },
                        new GeometryDrawing
                        {
                            Brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00FF00")),
                            Geometry = new RectangleGeometry(new Rect(0, 0, 2, 1))
                        }
                    }
                }
            };
            mainGrid.Children.Add(scanlinesGrid);

            // Header bar
            var headerBar = new Border
            {
                Height = 40,
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18536A")),
                VerticalAlignment = VerticalAlignment.Top,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#124050"))
            };

            var headerPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            headerBar.Child = headerPanel;

            var docHeaderIcon = new TextBlock
            {
                Text = "📚",
                FontSize = 20,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Update the docHeaderText to include version
            var docHeaderText = new TextBlock
            {
                Text = "INVOICE BALANCE REFRESHER DOCUMENTATION - V2.0.0",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B2F0FF")),
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(docHeaderIcon);
            headerPanel.Children.Add(docHeaderText);

            mainGrid.Children.Add(headerBar);

            // Content grid
            var contentGrid = new Grid { Margin = new Thickness(0, 40, 0, 0) };
            mainGrid.Children.Add(contentGrid);

            // Define columns for TOC and content
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // TOC panel
            var tocBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 0),
                BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#124050")),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 18, 83, 106))
            };
            Grid.SetColumn(tocBorder, 0);
            contentGrid.Children.Add(tocBorder);

            var tocPanel = new StackPanel { Margin = new Thickness(10, 15, 10, 10) };
            tocBorder.Child = tocPanel;

            // TOC Header
            var tocHeader = new TextBlock
            {
                Text = "TABLE OF CONTENTS",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
                FontWeight = FontWeights.Bold,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(5, 0, 0, 15),
                TextAlignment = TextAlignment.Left
            };
            tocPanel.Children.Add(tocHeader);

            // Main content area
            var contentBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 24, 180, 233))
            };
            Grid.SetColumn(contentBorder, 1);
            contentGrid.Children.Add(contentBorder);

            var contentScrollViewer = new ScrollViewer
            {
                Margin = new Thickness(20, 15, 20, 20),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            contentBorder.Child = contentScrollViewer;

            var contentStackPanel = new StackPanel();
            contentScrollViewer.Content = contentStackPanel;

            // Update TOC sections to add Maintenance Information section
            string[][] tocSections = new string[][]
            {
                new string[] { "1", "Overview", "#18B4E9", "📝" },
                new string[] { "2", "Credential Management", "#F0A030", "🔐" },
                new string[] { "3", "Single Invoice Processing", "#5BFF64", "📄" },
                new string[] { "4", "Batch Processing", "#5BFF64", "📚" },
                new string[] { "5", "Logging", "#F0A030", "📋" },
                new string[] { "6", "Sample CSV Generation", "#F0A030", "📁" },
                new string[] { "7", "API Format", "#E55555", "🔌" },
                new string[] { "8", "Rate Limiting", "#E55555", "⏱️" },
                new string[] { "9", "Frequently Asked Questions (FAQ)", "#18B4E9", "❓" },
                new string[] { "10", "Keyboard Shortcuts", "#F0A030", "⌨️" },
                new string[] { "11", "Troubleshooting", "#E55555", "🔧" },
                new string[] { "12", "System Requirements", "#18B4E9", "💻" },
                new string[] { "13", "Maintenance Information", "#18B4E9", "🛠️" }
            };

            // Document sections
            var docSections = new Dictionary<string, StackPanel>();

            // Create TOC entries and document sections
            foreach (var section in tocSections)
            {
                string sectionId = section[0];
                string sectionName = section[1];
                string sectionColor = section[2];
                string sectionIcon = section[3];

                // TOC entry
                var tocEntry = new Border
                {
                    Margin = new Thickness(0, 3, 0, 3),
                    Padding = new Thickness(8, 5, 8, 5),
                    CornerRadius = new CornerRadius(4),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var tocEntryPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                tocEntry.Child = tocEntryPanel;

                var tocEntryIcon = new TextBlock
                {
                    Text = sectionIcon,
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(sectionColor)),
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var tocEntryText = new TextBlock
                {
                    Text = $"{sectionId}. {sectionName}",
                    Foreground = (SolidColorBrush)owner.FindResource("ForegroundBrush"),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    TextWrapping = TextWrapping.Wrap
                };

                tocEntryPanel.Children.Add(tocEntryIcon);
                tocEntryPanel.Children.Add(tocEntryText);
                tocPanel.Children.Add(tocEntry);

                // Document section
                var sectionPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 25) };
                docSections[sectionId] = sectionPanel;
                contentStackPanel.Children.Add(sectionPanel);

                // Section heading
                var sectionHeading = new TextBlock
                {
                    Text = $"{sectionId}. {sectionName.ToUpper()}",
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(sectionColor)),
                    FontWeight = FontWeights.Bold,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 18,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                sectionPanel.Children.Add(sectionHeading);

                // Divider
                var sectionDivider = new System.Windows.Shapes.Rectangle
                {
                    Height = 2,
                    Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(sectionColor)),
                    Margin = new Thickness(0, 0, 0, 15),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    Opacity = 0.7
                };
                sectionPanel.Children.Add(sectionDivider);

                // TOC entry click handler
                tocEntry.MouseLeftButtonDown += (s, e) =>
                {
                    sectionPanel.BringIntoView();
                    e.Handled = true;
                };
            }

            // Add current timestamp to the documentation window
            var timestampBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 24, 80, 100)),
                Padding = new Thickness(5),
                Margin = new Thickness(10, 5, 10, 10)
            };

            var timestampText = new TextBlock
            {
                Text = $"Documentation generated: 2025-05-11 03:58:28 UTC | User: Sdeery123",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B2F0FF")),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10,
                TextAlignment = TextAlignment.Center
            };

            timestampBorder.Child = timestampText;
            tocPanel.Children.Add(timestampBorder);

            // Populate content
            PopulateDocumentationContent(docSections, owner);

            // Show the window
            docWindow.ShowDialog();
        }

        public static void PopulateDocumentationContent(Dictionary<string, StackPanel> sections, Window owner)
        {
            // SECTION 1: OVERVIEW
            var section1 = sections["1"];

            DocumentFormatHelper.AddParagraph(section1,
                "The Invoice Balance Refresher is a terminal-style application designed to help Invoice Cloud clients " +
                "quickly refresh and validate invoice balances through the secure SOAP API service.",
                isIntro: true);

            DocumentFormatHelper.AddParagraph(section1,
                "The application offers two primary modes of operation:");

            DocumentFormatHelper.AddBulletPoint(section1, "Single invoice processing with real-time feedback");
            DocumentFormatHelper.AddBulletPoint(section1, "Batch processing of multiple invoices via CSV files");

            DocumentFormatHelper.AddParagraph(section1,
                "All operations are logged in the console at the bottom of the application with searchable history.");

            DocumentFormatHelper.AddParagraph(section1,
                "The application includes a credential management system that allows you to securely save and reuse " +
                "multiple sets of API credentials for different environments or billers.");

            // SECTION 2: CREDENTIAL MANAGEMENT
            var section2 = sections["2"];

            DocumentFormatHelper.AddSubheading(section2, "Credential Management");

            DocumentFormatHelper.AddParagraph(section2,
                "The Credential Management system allows you to securely store and manage multiple sets of API credentials. " +
                "This feature makes it easy to switch between different environments (test/production) or different biller accounts " +
                "without needing to re-enter your credentials each time.");

            DocumentFormatHelper.AddParagraph(section2,
                "Credentials are encrypted and stored securely on your local machine. They are never transmitted " +
                "or stored in plain text.");

            DocumentFormatHelper.AddSteps(section2, "To save a new credential set:", new[]
            {
                "Enter the Biller GUID and Web Service Key in the respective fields",
                "Enter a descriptive name in the 'Save As' field (e.g., 'Production Account', 'Test Environment')",
                "Click the [Save] button next to the name field",
                "Your credentials will be encrypted and saved for future use"
            });

            DocumentFormatHelper.AddSteps(section2, "To use a saved credential set:", new[]
            {
                "Select the desired credential set from the dropdown at the top of the Single Invoice section",
                "The Biller GUID and Web Service Key fields will be automatically populated",
                "You can now process invoices with these credentials"
            });

            DocumentFormatHelper.AddSteps(section2, "To manage your credential sets:", new[]
            {
                "Click the [Manage] button next to the credential set dropdown, or",
                "Use the menu: [Credentials] > [Manage Credential Sets]",
                "In the Credential Management window, you can:",
                "   • Select a credential set to view or edit its details",
                "   • Click [New] to create a new credential set",
                "   • Click [Delete] to remove a selected credential set",
                "   • Edit the details and click [Save] to update a credential set",
                "   • Click [Close] when finished to return to the main window"
            });

            DocumentFormatHelper.AddNote(section2,
                "The credential set selected in the dropdown will be used for all operations, including single invoice processing, " +
                "batch processing, and customer record lookups.");

            // SECTION 3: SINGLE INVOICE PROCESSING
            var section3 = sections["3"];

            DocumentFormatHelper.AddParagraph(section3,
                "The Single Invoice Processing section allows you to refresh and check the balance of " +
                "an individual invoice, providing immediate feedback.");

            DocumentFormatHelper.AddSteps(section3, "To process a single invoice:", new[]
            {
                "Select a saved credential set from the dropdown, or enter credentials manually",
                "If entering credentials manually, you can save them using the 'Save As' field",
                "Enter the Account Number for the customer",
                "Enter the Invoice Number you wish to refresh",
                "Click [PROCESS INVOICE] to begin processing"
            });

            DocumentFormatHelper.AddParagraph(section3,
                "The result will appear in the output box below the button showing the refreshed invoice data " +
                "including status, invoice date, due date, balance due, and other key information.");

            DocumentFormatHelper.AddNote(section3,
                "The application will first refresh the customer balance, then retrieve the latest invoice information.");

            // SECTION 4: BATCH PROCESSING
            var section4 = sections["4"];

            DocumentFormatHelper.AddParagraph(section4,
                "Batch processing enables you to refresh multiple invoices at once using a CSV file. " +
                "This is particularly useful for end-of-day or scheduled balance updates.");

            DocumentFormatHelper.AddSteps(section4, "To process multiple invoices:", new[]
            {
                "Select a saved credential set from the dropdown, or enter credentials manually",
                "Create a CSV file with one invoice number per line (or account/invoice pairs)",
                "Select the appropriate CSV format option (Invoice Only or Account,Invoice format)",
                "Click [BROWSE] to select your CSV file",
                "Click [PROCESS CSV] to begin processing the batch"
            });

            DocumentFormatHelper.AddParagraph(section4, "CSV File Format Options:");

            DocumentFormatHelper.AddCodeBlock(section4,
                "Option 1: Invoice numbers only (one per line):\n" +
                "INV0001\n" +
                "INV0002\n" +
                "INV0003");

            DocumentFormatHelper.AddCodeBlock(section4,
                "Option 2: Account numbers and invoice numbers:\n" +
                "ACCT001,INV0001\n" +
                "ACCT002,INV0002\n" +
                "ACCT003,INV0003");

            DocumentFormatHelper.AddParagraph(section4,
                "Results will be saved to a file named 'InvoiceResults.csv' in the same directory as your input file. " +
                "This file will contain the processing status and updated balance information for each invoice.");

            // SECTION 4.1: SCHEDULING (UPDATED)
            DocumentFormatHelper.AddSubheading(section4, "Automated Scheduling");

            DocumentFormatHelper.AddParagraph(section4,
                "The application includes a comprehensive built-in scheduling system that allows you to automate batch invoice processing. " +
                "This powerful feature offers multiple frequency options to fit your specific needs, from minute-by-minute execution to " +
                "quarterly scheduling.");

            DocumentFormatHelper.AddSteps(section4, "To schedule a batch process:", new[]
            {
                "Open the [File] menu and select [Scheduler]",
                "In the Schedule Manager window, click [ADD NEW] to create a new scheduled task",
                "Fill in the task details, including name, description, and select a frequency option",
                "Configure the schedule options specific to your chosen frequency",
                "Select a saved credential set or enter credentials manually",
                "Specify the CSV file path and whether the CSV includes account numbers",
                "Click [SAVE] to add the schedule"
            });

            DocumentFormatHelper.AddSubheading(section4, "Available Scheduling Frequencies");

            DocumentFormatHelper.AddParagraph(section4,
                "The scheduler offers the following frequency options:");

            // Create a grid to display frequency options
            var frequencyGrid = new Grid();
            frequencyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            frequencyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Frequency descriptions
            string[][] frequencies = new string[][]
            {
                new string[] { "Once", "Run a single time at the specified date and time" },
                new string[] { "Custom (Minutes)", "Run every X minutes (e.g., every 15, 30, or 45 minutes)" },
                new string[] { "Hourly", "Run every X hours (e.g., every 1, 2, or 4 hours)" },
                new string[] { "Daily", "Run every X days at a specific time" },
                new string[] { "WorkDays", "Run every weekday (Monday through Friday) at a specific time" },
                new string[] { "Weekly", "Run on specific days of the week at a specific time" },
                new string[] { "BiWeekly", "Run on specific days of the week, alternating between even and odd weeks" },
                new string[] { "Monthly", "Run on specific days of selected months" },
                new string[] { "Quarterly", "Run on specific days in selected months of each quarter" },
                new string[] { "Multiple Times Daily", "Run multiple times per day at specified times" }
            };

            // Add rows for each frequency
            for (int i = 0; i < frequencies.Length; i++)
            {
                frequencyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var freqLabel = new TextBlock
                {
                    Text = frequencies[i][0],
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5BFF64")),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(15, 5, 15, 5)
                };

                var freqDesc = new TextBlock
                {
                    Text = frequencies[i][1],
                    Foreground = (SolidColorBrush)owner.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                Grid.SetRow(freqLabel, i);
                Grid.SetColumn(freqLabel, 0);
                Grid.SetRow(freqDesc, i);
                Grid.SetColumn(freqDesc, 1);

                frequencyGrid.Children.Add(freqLabel);
                frequencyGrid.Children.Add(freqDesc);
            }

            section4.Children.Add(frequencyGrid);

            DocumentFormatHelper.AddSubheading(section4, "Advanced Scheduling Options");

            DocumentFormatHelper.AddParagraph(section4,
                "Each scheduling frequency offers specific configuration options:");

            DocumentFormatHelper.AddBulletPoint(section4, "Daily scheduling: Set an interval in days (e.g., every 2 days)");
            DocumentFormatHelper.AddBulletPoint(section4, "Weekly scheduling: Select specific days of the week (e.g., Monday, Wednesday, Friday)");
            DocumentFormatHelper.AddBulletPoint(section4, "Monthly scheduling: Select specific days of the month (1-31) and specific months");
            DocumentFormatHelper.AddBulletPoint(section4, "Quarterly scheduling: Select which months in each quarter and which day of the month");
            DocumentFormatHelper.AddBulletPoint(section4, "BiWeekly scheduling: Alternate between even and odd weeks with specific days selected");
            DocumentFormatHelper.AddBulletPoint(section4, "Multiple Times Daily: Configure multiple specific times throughout the day");

            DocumentFormatHelper.AddParagraph(section4,
                "Scheduled tasks will appear in the Schedule Manager window, where you can edit, delete, or run them manually using the [RUN NOW] button. " +
                "The application will automatically execute enabled tasks at their scheduled times. The application automatically calculates the next run time " +
                "based on your configuration and the current date/time.");

            DocumentFormatHelper.AddNote(section4,
                "The application must be running for scheduled tasks to execute. For tasks that need to run when the application is not open, " +
                "consider creating a system-level scheduled task to launch the application at specific times.");

            // SECTION 5: LOGGING
            var section5 = sections["5"];

            DocumentFormatHelper.AddParagraph(section5,
                "The application maintains detailed logs of all operations to help with troubleshooting " +
                "and to provide an audit trail of balance refresh activities.");

            DocumentFormatHelper.AddSubheading(section5, "Log Features:");

            DocumentFormatHelper.AddBulletPoint(section5, "All operations are logged in the console at the bottom of the application");
            DocumentFormatHelper.AddBulletPoint(section5, "Session logs are automatically saved to the 'Logs' directory with timestamps");
            DocumentFormatHelper.AddBulletPoint(section5, "You can manually save the current console log by clicking [SAVE LOGS]");
            DocumentFormatHelper.AddBulletPoint(section5, "Clear the console display by clicking [CLEAR]");
            DocumentFormatHelper.AddBulletPoint(section5, "Search functionality allows you to find specific entries quickly");

            DocumentFormatHelper.AddParagraph(section5,
                "Log entries include timestamps, log levels (Info, Warning, Error, Debug), and detailed messages " +
                "about each operation performed by the application.");

            // SECTION 6: SAMPLE CSV GENERATION
            var section6 = sections["6"];

            DocumentFormatHelper.AddParagraph(section6,
                "To help you get started with batch processing, the application can generate a sample CSV file " +
                "with the correct format.");

            DocumentFormatHelper.AddSteps(section6, "To generate a sample CSV file:", new[]
            {
                "Click on [FILE] > Generate Sample CSV in the menu",
                "Choose where to save the file",
                "The file will be created with sample invoice numbers",
                "Edit the file with your actual invoice numbers before processing"
            });

            DocumentFormatHelper.AddNote(section6,
                "The currently selected credential set will be used when processing any CSV file.");

            // SECTION 7: API FORMAT
            var section7 = sections["7"];

            DocumentFormatHelper.AddParagraph(section7,
                "The application communicates with the Invoice Cloud SOAP API using a secure XML format. " +
                "Understanding this format may help when troubleshooting or developing integrations.");

            DocumentFormatHelper.AddSubheading(section7, "Invoice Request Format:");

            DocumentFormatHelper.AddCodeBlock(section7,
                "<soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>\n" +
                "    <soap12:Body>\n" +
                "        <ViewInvoiceByInvoiceNumber xmlns='https://www.invoicecloud.com/portal/webservices/CloudInvoicing/'>\n" +
                "            <Req>\n" +
                "                <BillerGUID>{billerGUID}</BillerGUID>\n" +
                "                <WebServiceKey>{webServiceKey}</WebServiceKey>\n" +
                "                <InvoiceNumber>{invoiceNumber}</InvoiceNumber>\n" +
                "            </Req>\n" +
                "        </ViewInvoiceByInvoiceNumber>\n" +
                "    </soap12:Body>\n" +
                "</soap12:Envelope>");

            DocumentFormatHelper.AddSubheading(section7, "Customer Record Request Format:");

            DocumentFormatHelper.AddCodeBlock(section7,
                "<soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>\n" +
                "  <soap12:Body>\n" +
                "    <ViewCustomerRecord xmlns='https://www.invoicecloud.com/portal/webservices/CloudManagement/'>\n" +
                "      <BillerGUID>{billerGUID}</BillerGUID>\n" +
                "      <WebServiceKey>{webServiceKey}</WebServiceKey>\n" +
                "      <AccountNumber>{accountNumber}</AccountNumber>\n" +
                "    </ViewCustomerRecord>\n" +
                "  </soap12:Body>\n" +
                "</soap12:Envelope>");

            DocumentFormatHelper.AddNote(section7,
                "The application handles all the API communication details for you, including authentication, " +
                "error handling, and retry logic for intermittent failures.");

            // SECTION 8: RATE LIMITING (UPDATED)
            var section8 = sections["8"];

            DocumentFormatHelper.AddParagraph(section8,
                "The Invoice Balance Refresher includes built-in rate limiting functionality to prevent API throttling and ensure " +
                "reliable operation when processing large batches of invoices. Rate limiting controls how quickly the application " +
                "sends requests to the Invoice Cloud API service.",
                isIntro: true);

            DocumentFormatHelper.AddSubheading(section8, "Rate Limiting Configuration");

            DocumentFormatHelper.AddParagraph(section8,
                "The application's rate limiting system has several configurable settings that determine how API requests are managed:");

            // Create a grid for rate limiting settings
            var rateLimitGrid = new Grid();
            rateLimitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rateLimitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            rateLimitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rateLimitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rateLimitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rateLimitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rateLimitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Setting labels and descriptions
            string[][] rateSettings = new string[][]
            {
                new string[] { "Request Interval:", "500ms (Default)" },
                new string[] { "Request Count Threshold:", "50 requests (Default)" },
                new string[] { "Threshold Cooldown:", "5000ms (Default)" },
                new string[] { "Rate Limit Retry Delay:", "5000ms (Default)" },
                new string[] { "Rate Limiting Enabled:", "Yes (Default)" }
            };

            for (int i = 0; i < rateSettings.Length; i++)
            {
                var settingLabel = new TextBlock
                {
                    Text = rateSettings[i][0],
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E55555")),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(15, 5, 15, 5)
                };

                var settingValue = new TextBlock
                {
                    Text = rateSettings[i][1],
                    Foreground = (SolidColorBrush)owner.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                Grid.SetRow(settingLabel, i);
                Grid.SetColumn(settingLabel, 0);
                Grid.SetRow(settingValue, i);
                Grid.SetColumn(settingValue, 1);

                rateLimitGrid.Children.Add(settingLabel);
                rateLimitGrid.Children.Add(settingValue);
            }

            section8.Children.Add(rateLimitGrid);

            DocumentFormatHelper.AddSubheading(section8, "Understanding Rate Limiting Settings");

            DocumentFormatHelper.AddParagraph(section8,
                "Each rate limiting setting controls a specific aspect of the application's API request behavior:");

            DocumentFormatHelper.AddBulletPoint(section8,
                "Request Interval: The minimum time in milliseconds between consecutive API requests. " +
                "This setting spreads out requests to avoid overwhelming the API service with sudden bursts of traffic.");

            DocumentFormatHelper.AddBulletPoint(section8,
                "Request Count Threshold: The maximum number of requests that will be sent before enforcing a cooldown period. " +
                "This prevents the application from sending too many requests in a short timeframe.");

            DocumentFormatHelper.AddBulletPoint(section8,
                "Threshold Cooldown: The duration in milliseconds that the application will pause after reaching the request count threshold. " +
                "This gives the API service time to process existing requests before sending more.");

            DocumentFormatHelper.AddBulletPoint(section8,
                "Rate Limit Retry Delay: The time in milliseconds the application will wait before retrying a request that received a " +
                "rate limit response (HTTP 429) from the API server.");

            DocumentFormatHelper.AddBulletPoint(section8,
                "Rate Limiting Enabled: Master toggle that enables or disables all rate limiting functionality. It's recommended to " +
                "keep this enabled to ensure reliable API communication.");

            DocumentFormatHelper.AddSubheading(section8, "Configuring Rate Limiting");

            DocumentFormatHelper.AddSteps(section8, "To adjust rate limiting settings:", new[]
            {
                "Open the application menu and select [Settings] > [Rate Limiting]",
                "The Rate Limiting Settings dialog will appear",
                "Enable or disable rate limiting using the checkbox at the top",
                "Adjust each setting according to your needs",
                "Click [Save] to apply your changes, or [Reset] to restore default values",
                "Click [Cancel] to close without saving changes"
            });

            DocumentFormatHelper.AddParagraph(section8,
                "For large batch processing operations, the application displays rate limiting status in the progress information. " +
                "If requests are being limited, the application will automatically adjust its behavior according to these settings.");

            DocumentFormatHelper.AddNote(section8,
                "If you experience frequent 'Rate Limit Exceeded' or '429 Too Many Requests' errors, try increasing the Request Interval " +
                "or reducing the Request Count Threshold. The default settings work well for most accounts, but some may require adjustment " +
                "based on specific API limits assigned to your Invoice Cloud account.");

            // RTDR Special Options Section
            DocumentFormatHelper.AddSubheading(section8, "RTDR Special Options");

            DocumentFormatHelper.AddParagraph(section8,
                "For billers using RTDR (Real-Time Data Refresh), the application provides special options to control how invoice " +
                "balances are refreshed under specific conditions:");

            // Option 1: RTDR - Override 6 Hour Previous RTDR Check
            DocumentFormatHelper.AddSubheading(section8, "RTDR - Override 6 Hour Previous RTDR Check");

            DocumentFormatHelper.AddParagraph(section8,
                "For billers with RTDR, the system normally does not pull an RTDR balance if a successful balance refresh occurred within the past 6 hours. " +
                "Enabling this option forces a refresh regardless of the 6-hour restriction.");

            DocumentFormatHelper.AddNote(section8,
                "This option is OFF by default. If you attempt to force a refresh while this option is disabled, the system will not refresh the balance " +
                "if a successful refresh occurred in the last 6 hours.");

            // Option 2: Disable 24-hour payment check on RTDR for AutoPay Payments
            DocumentFormatHelper.AddSubheading(section8, "Disable 24-hour payment check on RTDR for AutoPay Payments");

            DocumentFormatHelper.AddParagraph(section8,
                "When enabled, this option will override the 24-hour hold period for RTDR on invoices with recent AutoPay payment activity. " +
                "This allows balance refreshes to occur on invoices that have had AutoPay payments within the last 24 hours.");

            DocumentFormatHelper.AddNote(section8,
                "This option is OFF by default. To enable or disable this setting, you must contact Invoice Cloud support.");

            // Option 3: Disable 24-hour payment check for RTDR
            DocumentFormatHelper.AddSubheading(section8, "Disable 24-hour payment check for RTDR");

            DocumentFormatHelper.AddParagraph(section8,
                "RTDR will not refresh balances on invoices with any payment within the previous 24 hours. When enabled, " +
                "this option will bypass that check, allowing balance refreshes to occur regardless of recent payment activity.");

            DocumentFormatHelper.AddNote(section8,
                "This option is OFF by default. To enable or disable this setting, you must contact Invoice Cloud support.");

            // Add warning about RTDR options
            var warningBorder = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E55555")),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 229, 85, 85)),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 5, 0, 15),
                CornerRadius = new CornerRadius(4)
            };

            var warningPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

            var warningIcon = new TextBlock
            {
                Text = "⚠️",
                FontSize = 14,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var warningText = new TextBlock
            {
                Text = "Warning: Use these RTDR override options with caution. They are designed for specific business use cases where immediate " +
                       "balance refreshes are required even after recent payments. Enabling these options may cause balance discrepancies " +
                       "if payment posting is still in progress.",
                Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["ForegroundBrush"],
                TextWrapping = TextWrapping.Wrap
            };

            warningPanel.Children.Add(warningIcon);
            warningPanel.Children.Add(warningText);
            warningBorder.Child = warningPanel;
            section8.Children.Add(warningBorder);

            // SECTION 9: FAQ
            var section9 = sections["9"];

            DocumentFormatHelper.AddFAQItem(section9,
                "Where do I get my Biller GUID and Web Service Key?",
                "These credentials are provided by Invoice Cloud. Contact your account " +
                "representative or system administrator if you don't have them.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "How do I know if an invoice balance was successfully refreshed?",
                "After processing, the status will show 'Success' and display the updated " +
                "balance information. The console log will also show a success message.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "Can I process invoices with different account numbers in batch mode?",
                "Yes. Select the 'Account Number,Invoice Number Format' option and format your CSV file with " +
                "both account number and invoice number on each line separated by a comma:\n" +
                "ACCT001,INV0001\n" +
                "ACCT002,INV0002",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "How can I search for specific invoices in batch results?",
                "Use the search box above the batch results. It will filter results to show " +
                "only invoices that contain your search text.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "What do I do if I get a 'Server error' message?",
                "The application automatically retries server errors up to 3 times. If it still " +
                "fails, verify your network connection and check that the Invoice Cloud service " +
                "is operating normally.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "Where are logs stored?",
                "Logs are automatically saved in the 'Logs' directory within the application folder. " +
                "Each session creates a new log file with a timestamp in the filename.",
                owner);

            // FAQ items for rate limiting
            DocumentFormatHelper.AddFAQItem(section9,
                "What happens if I disable rate limiting?",
                "Disabling rate limiting removes all restrictions on API request frequency from the application side. This may lead to " +
                "faster processing initially, but could trigger API throttling or temporary blocks from the Invoice Cloud API if requests " +
                "exceed their service limits. It's generally recommended to keep rate limiting enabled.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "How do rate limiting settings affect batch processing time?",
                "More restrictive rate limiting settings (higher interval, lower threshold) will increase the total time needed to " +
                "process large batches, but will ensure more reliable processing without API throttling. For example, with the default " +
                "500ms interval, the application can process approximately 120 requests per minute at maximum throughput.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "Does rate limiting apply to scheduled tasks?",
                "Yes. Scheduled tasks use the rate limiting settings that are active when the task runs. If you modify your rate limiting " +
                "settings, all future scheduled task executions will use the updated settings without requiring any changes to the schedule.",
                owner);

            // New FAQ items for credential management
            DocumentFormatHelper.AddFAQItem(section9,
                "How secure are my saved credentials?",
                "Credentials are encrypted using Windows Data Protection API (DPAPI) before being stored on your local machine. " +
                "This encryption is tied to your Windows user account, meaning only you can decrypt the stored credentials. " +
                "Credentials are never stored in plain text or transmitted outside your computer.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "Can I use this application with multiple Biller GUIDs?",
                "Yes. The credential management system allows you to save multiple credential sets with different names. " +
                "You can easily switch between different Biller GUIDs by selecting a different credential set from the dropdown.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "Where are my credential sets stored?",
                "Credential sets are stored in an encrypted file in your local application data folder: " +
                "%LocalAppData%\\InvoiceBalanceRefresher\\credentials.dat. This file is encrypted and can only be " +
                "accessed by your Windows user account.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "Can I transfer my saved credential sets to another computer?",
                "No. For security reasons, credential sets are encrypted using the Windows Data Protection API, which ties " +
                "the encryption to your specific Windows user account. You'll need to re-create your credential sets " +
                "when moving to a new computer or user account.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "Is there a way to export my credential sets for backup?",
                "The current version doesn't support direct export/import of credential sets due to security constraints of the Windows DPAPI encryption. " +
                "For backup purposes, we recommend taking note of your credential information separately in a secure password manager.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "Can I use keyboard shortcuts for common operations?",
                "Yes, the application supports various keyboard shortcuts. See the Keyboard Shortcuts section of this documentation for a complete list. " +
                "Common shortcuts include Ctrl+S to save credentials, Ctrl+P to process a single invoice, and Ctrl+B to process a batch.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "Does the application support multiple users on the same computer?",
                "Yes. Each Windows user account has its own separate credential storage. Credentials saved by one user are not accessible to other users " +
                "on the same computer, ensuring security and privacy between different users.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "What happens if I forget a credential set name?",
                "You can open the Credential Management window and view all your saved credential sets. From there, you can select any set to view " +
                "its details. The Biller GUID and Web Service Key will be displayed (but still securely stored).",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "How do I switch between light and dark mode?",
                "Use the Theme menu and select either 'Light Mode' or 'Dark Mode'.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "What happens if the CSV file has invalid invoice numbers?",
                "The application will process valid invoice numbers and log errors for invalid ones. " +
                "The results CSV will include error details for failed invoices.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "Is there a limit to how many invoices I can process in batch mode?",
                "There is no hard limit in the application, but processing very large batches " +
                "may take significant time. Consider breaking very large batches into smaller files.",
                owner);

            // Updated FAQ item for scheduling
            DocumentFormatHelper.AddFAQItem(section9,
                "How does scheduling work?",
                "The application includes a built-in scheduler that can run batch processes at predetermined times. " +
                "You can create schedules with various frequencies including once, hourly, daily, weekly, monthly, and more. " +
                "For each schedule, you define the frequency, run time, and batch processing settings. The application checks " +
                "for due scheduled tasks every minute while it's running. For each task that's due to run, it will automatically " +
                "execute the specified batch process and calculate the next run time.",
                owner);

            // FAQ items for maintenance
            DocumentFormatHelper.AddFAQItem(section9,
                "What happens during maintenance?",
                "During maintenance, the application performs several cleanup tasks: it removes log files older than the configured retention period, " +
                "limits the number of session files per day to prevent disk space issues, and performs other housekeeping tasks to keep " +
                "the application running efficiently without manual intervention.",
                owner);

            DocumentFormatHelper.AddFAQItem(section9,
                "How often should I run maintenance?",
                "For most users, running maintenance on every startup (the default setting) is recommended. If you use the application very frequently, " +
                "you might prefer a less frequent schedule like weekly or monthly. The maintenance tasks are lightweight and quick to execute, " +
                "so they shouldn't impact normal application usage.",
                owner);

            // SECTION 10: KEYBOARD SHORTCUTS
            var section10 = sections["10"];

            DocumentFormatHelper.AddParagraph(section10,
                "The Invoice Balance Refresher includes keyboard shortcuts to help you work more efficiently.");

            DocumentFormatHelper.AddSubheading(section10, "Global Shortcuts:");

            var shortcuts = new Dictionary<string, string>
            {
                { "Ctrl+S", "Save credentials with current name" },
                { "Ctrl+O", "Open CSV file for batch processing" },
                { "Ctrl+P", "Process current single invoice" },
                { "Ctrl+B", "Process current batch" },
                { "Ctrl+L", "Clear console log" },
                { "Ctrl+F", "Focus search box" },
                { "F1", "Show this documentation" },
                { "Ctrl+M", "Open credential manager" },
                { "Ctrl+R", "Open rate limiting settings" },
                { "Alt+1", "Switch to Light Mode" },
                { "Alt+2", "Switch to Dark Mode" },
                { "Ctrl+Tab", "Cycle through credential sets" }
            };

            var shortcutGrid = new Grid();
            shortcutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            shortcutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;
            foreach (var shortcut in shortcuts)
            {
                shortcutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var keyText = new TextBlock
                {
                    Text = shortcut.Key,
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 3, 20, 3)
                };

                var descText = new TextBlock
                {
                    Text = shortcut.Value,
                    Foreground = owner.FindResource("ForegroundBrush") as SolidColorBrush,
                    Margin = new Thickness(0, 3, 0, 3)
                };

                Grid.SetRow(keyText, row);
                Grid.SetColumn(keyText, 0);
                Grid.SetRow(descText, row);
                Grid.SetColumn(descText, 1);

                shortcutGrid.Children.Add(keyText);
                shortcutGrid.Children.Add(descText);
                row++;
            }

            section10.Children.Add(shortcutGrid);

            DocumentFormatHelper.AddNote(section10,
                "Keyboard shortcuts can be customized in the application settings. (Future Update)");

            // SECTION 11: TROUBLESHOOTING
            var section11 = sections["11"];

            DocumentFormatHelper.AddParagraph(section11,
                "If you encounter issues while using the Invoice Balance Refresher, this section provides guidance on common problems and their solutions.");

            DocumentFormatHelper.AddSubheading(section11, "Common Issues and Solutions:");

            // Using the FAQItem format for troubleshooting items
            DocumentFormatHelper.AddFAQItem(section11,
                "Application fails to start",
                "Ensure you have the latest .NET Framework installed. Try running as administrator or reinstalling the application. Check Windows Event Log for details.",
                owner);

            DocumentFormatHelper.AddFAQItem(section11,
                "Error 'Cannot connect to service'",
                "Verify your internet connection. Check if you can access other websites. The Invoice Cloud API may be temporarily unavailable - try again later or contact support.",
                owner);

            DocumentFormatHelper.AddFAQItem(section11,
                "Rate limit errors during batch processing",
                "If you receive 'Rate Limit Exceeded' errors, adjust your rate limiting settings by increasing the Request Interval and/or Threshold Cooldown values. For very large batches, consider breaking them into smaller files to process sequentially. The application's rate limiting functionality is designed to prevent these errors, but may need adjustment for your specific account limits.",
                owner);

            DocumentFormatHelper.AddFAQItem(section11,
                "Credential sets are not saving",
                "Ensure you have write permissions to %LocalAppData%\\InvoiceBalanceRefresher. Try running the application as administrator. Check that Windows DPAPI is functioning correctly on your system.",
                owner);

            DocumentFormatHelper.AddFAQItem(section11,
                "Batch processing is very slow",
                "Large batches may take significant time. Consider splitting your batch into smaller files. Close other applications to free up system resources. Ensure your network connection is stable. Check your rate limiting settings - they may be unnecessarily restrictive for your account type.",
                owner);

            DocumentFormatHelper.AddFAQItem(section11,
                "CSV file format errors",
                "Ensure your CSV file is properly formatted according to the selected option (Invoice Only or Account,Invoice format). Verify there are no extra spaces, quotes, or special characters. Try opening and resaving the file in a text editor to ensure proper encoding.",
                owner);

            DocumentFormatHelper.AddFAQItem(section11,
                "Scheduled tasks not running",
                "Check that the application is running at the scheduled time, as tasks only execute when the application is active. Verify that the task is enabled in the Schedule Manager. Ensure the credentials used have not expired and the CSV file path is still valid.",
                owner);

            DocumentFormatHelper.AddSubheading(section11, "Error Logs and Diagnostics:");

            DocumentFormatHelper.AddParagraph(section11,
                "When contacting support, please provide the following information:");

            DocumentFormatHelper.AddBulletPoint(section11, "Application version (displayed in About window)");
            DocumentFormatHelper.AddBulletPoint(section11, "Full error message text");
            DocumentFormatHelper.AddBulletPoint(section11, "Log files from the Logs directory");
            DocumentFormatHelper.AddBulletPoint(section11, "Current rate limiting settings");
            DocumentFormatHelper.AddBulletPoint(section11, "Steps to reproduce the issue");
            DocumentFormatHelper.AddBulletPoint(section11, "Operating system version");

            DocumentFormatHelper.AddNote(section11,
                "For security reasons, never share your Biller GUID or Web Service Key. Support staff will never ask for these credentials.");

            // SECTION 12: SYSTEM REQUIREMENTS
            var section12 = sections["12"];

            DocumentFormatHelper.AddParagraph(section12,
                "The Invoice Balance Refresher is designed to work efficiently on most modern Windows computers. " +
                "Below are the minimum and recommended system requirements.");

            DocumentFormatHelper.AddSubheading(section12, "Minimum Requirements:");

            DocumentFormatHelper.AddBulletPoint(section12, "Operating System: Windows 10 (64-bit) or newer");
            DocumentFormatHelper.AddBulletPoint(section12, ".NET Framework: 8.0 or later");
            DocumentFormatHelper.AddBulletPoint(section12, "Processor: Dual-core 2.0 GHz or higher");
            DocumentFormatHelper.AddBulletPoint(section12, "Memory: 4 GB RAM");
            DocumentFormatHelper.AddBulletPoint(section12, "Disk Space: 200 MB available");
            DocumentFormatHelper.AddBulletPoint(section12, "Internet Connection: 1 Mbps or faster, reliable connection");
            DocumentFormatHelper.AddBulletPoint(section12, "Display: 1366x768 resolution or higher");

            DocumentFormatHelper.AddSubheading(section12, "Recommended Specifications:");

            DocumentFormatHelper.AddBulletPoint(section12, "Operating System: Windows 11 (64-bit)");
            DocumentFormatHelper.AddBulletPoint(section12, "Processor: Quad-core 2.5 GHz or higher");
            DocumentFormatHelper.AddBulletPoint(section12, "Memory: 8 GB RAM");
            DocumentFormatHelper.AddBulletPoint(section12, "Disk Space: 500 MB available");
            DocumentFormatHelper.AddBulletPoint(section12, "Internet Connection: 5 Mbps or faster, stable connection");
            DocumentFormatHelper.AddBulletPoint(section12, "Display: 1920x1080 resolution or higher");

            DocumentFormatHelper.AddSubheading(section12, "Additional Notes:");

            DocumentFormatHelper.AddParagraph(section12,
                "For batch processing of large CSV files (over 1000 invoices), additional RAM is recommended for improved performance. " +
                "If you need to run scheduled tasks when the computer is unattended, consider setting up an auto-start option for the application.");

            DocumentFormatHelper.AddNote(section12,
                "Microsoft Excel or compatible spreadsheet software is recommended for editing CSV files, " +
                "but not required for application functionality.");

            // SECTION 13: MAINTENANCE INFORMATION
            var section13 = sections["13"];

            DocumentFormatHelper.AddParagraph(section13,
                "The Invoice Balance Refresher includes built-in maintenance features to help keep the application running smoothly. " +
                "These features handle cleanup of old log files, management of session files, and other housekeeping tasks.",
                isIntro: true);

            DocumentFormatHelper.AddSubheading(section13, "Maintenance Features");

            DocumentFormatHelper.AddBulletPoint(section13, "Log Cleanup: Automatically removes log files older than the configured retention period");
            DocumentFormatHelper.AddBulletPoint(section13, "Session File Management: Limits the number of session files created per day");
            DocumentFormatHelper.AddBulletPoint(section13, "Data Optimization: Performs various optimizations to ensure smooth application performance");

            DocumentFormatHelper.AddSubheading(section13, "Maintenance Frequency Options");

            // Create a grid for maintenance frequency options
            var maintenanceFreqGrid = new Grid();
            maintenanceFreqGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            maintenanceFreqGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            maintenanceFreqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            maintenanceFreqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            maintenanceFreqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            maintenanceFreqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Setting labels and descriptions
            string[][] freqSettings = new string[][]
            {
                new string[] { "Every Startup:", "Run maintenance tasks each time the application starts" },
                new string[] { "Daily:", "Run maintenance tasks once per day" },
                new string[] { "Weekly:", "Run maintenance tasks once per week" },
                new string[] { "Monthly:", "Run maintenance tasks once per month" }
            };

            for (int i = 0; i < freqSettings.Length; i++)
            {
                var freqLabel = new TextBlock
                {
                    Text = freqSettings[i][0],
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(15, 5, 15, 5)
                };

                var freqDesc = new TextBlock
                {
                    Text = freqSettings[i][1],
                    Foreground = (SolidColorBrush)owner.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                Grid.SetRow(freqLabel, i);
                Grid.SetColumn(freqLabel, 0);
                Grid.SetRow(freqDesc, i);
                Grid.SetColumn(freqDesc, 1);

                maintenanceFreqGrid.Children.Add(freqLabel);
                maintenanceFreqGrid.Children.Add(freqDesc);
            }

            section13.Children.Add(maintenanceFreqGrid);

            DocumentFormatHelper.AddSubheading(section13, "Configuration Options");

            DocumentFormatHelper.AddParagraph(section13,
                "The maintenance system provides several configuration options that you can adjust according to your needs:");

            DocumentFormatHelper.AddBulletPoint(section13, "Log Retention Days: Number of days to keep log files before deletion (default: 30 days)");
            DocumentFormatHelper.AddBulletPoint(section13, "Max Session Files Per Day: Maximum number of session log files to keep per day (default: 10 files)");
            DocumentFormatHelper.AddBulletPoint(section13, "Enable Log Cleanup: Toggle log file cleanup feature (default: enabled)");
            DocumentFormatHelper.AddBulletPoint(section13, "Enable Periodic Maintenance: Automatically run maintenance according to schedule (default: disabled)");
            DocumentFormatHelper.AddBulletPoint(section13, "Maintenance Frequency: How often maintenance should run (default: Every Startup)");

            DocumentFormatHelper.AddSteps(section13, "To configure maintenance settings:", new[]
            {
                "Open the application menu and select [Settings] > [Maintenance Settings]",
                "The Maintenance Settings dialog will appear",
                "Adjust settings according to your needs",
                "Enable or disable specific maintenance tasks",
                "Set the maintenance frequency",
                "Click [Save] to apply your changes, or [Cancel] to close without saving"
            });

            DocumentFormatHelper.AddNote(section13,
                "You can also manually trigger maintenance at any time by clicking the [Run Maintenance] button in the main window.");
        }
    }
}
