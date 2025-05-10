using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace InvoiceBalanceRefresher
{
    public class AboutDialog
    {
        private readonly Window _ownerWindow;
        private readonly string _appVersion;
        private readonly Action<LogLevel, string> _logAction;

        public AboutDialog(Window ownerWindow, string appVersion, Action<LogLevel, string> logAction)
        {
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            _appVersion = appVersion ?? throw new ArgumentNullException(nameof(appVersion));
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
        }

        public void Show()
        {
            _logAction(LogLevel.Info, "About dialog requested");

            // Create custom About window with enhanced styling
            var aboutWindow = new Window
            {
                Title = "About Invoice Balance Refresher",
                Width = 700,
                Height = 650, // Increased height for schedule info
                Background = (SolidColorBrush)_ownerWindow.FindResource("BackgroundBrush"),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = _ownerWindow,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            // Create a Grid as the main container
            var mainGrid = new Grid();
            aboutWindow.Content = mainGrid;

            // Add scanlines overlay (same as main window but a bit more visible)
            var scanlinesGrid = new Grid();
            scanlinesGrid.SetValue(Panel.ZIndexProperty, -1);
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
                            Brush = Brushes.Transparent,
                            Geometry = new RectangleGeometry(new Rect(0, 0, 2, 2))
                        },
                        new GeometryDrawing
                        {
                            Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00")),
                            Geometry = new RectangleGeometry(new Rect(0, 0, 2, 1))
                        }
                    }
                }
            };
            mainGrid.Children.Add(scanlinesGrid);

            // Add a "terminal status" line at the top
            var statusBar = new Border
            {
                Height = 25,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18536A")),
                VerticalAlignment = VerticalAlignment.Top,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#124050"))
            };

            var statusPanel = new StackPanel { Orientation = Orientation.Horizontal };
            statusBar.Child = statusPanel;

            statusPanel.Children.Add(new TextBlock
            {
                Text = "SYSTEM: ONLINE",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B2F0FF")),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            });

            // Add a "terminal" blinking cursor effect
            var cursorBorder = new Border
            {
                Width = 8,
                Height = 15,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Add animation for blinking cursor
            var blinkAnimation = new Storyboard();
            var opacityAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.8)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard.SetTarget(opacityAnimation, cursorBorder);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
            blinkAnimation.Children.Add(opacityAnimation);

            statusPanel.Children.Add(cursorBorder);
            blinkAnimation.Begin();

            mainGrid.Children.Add(statusBar);

            // Create a ScrollViewer to contain all content
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(20, 35, 20, 20)
            };
            mainGrid.Children.Add(scrollViewer);

            // Create a StackPanel for the content
            var contentPanel = new StackPanel
            {
                Margin = new Thickness(0)
            };
            scrollViewer.Content = contentPanel;

            // Add "computer readout" header above the title
            var headerBlock = new TextBlock
            {
                Text = "// SYSTEM INFORMATION READOUT //",
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            contentPanel.Children.Add(headerBlock);

            // Add logo/title with enhanced glow effect
            var titleBlock = new TextBlock
            {
                Text = ">> INVOICE BALANCE REFRESHER <<",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5),
                Effect = new DropShadowEffect
                {
                    Color = ((SolidColorBrush)_ownerWindow.FindResource("AccentBrush")).Color,
                    ShadowDepth = 0,
                    BlurRadius = 20,
                    Opacity = 0.8
                }
            };
            contentPanel.Children.Add(titleBlock);

            // Add version info with build date
            var versionBlock = new TextBlock
            {
                Text = $"Version {_appVersion} (Build: {DateTime.Now:MMM dd, yyyy})",
                FontSize = 14,
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush2"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            contentPanel.Children.Add(versionBlock);

            // Add divider with slightly more visual appeal
            var divider1 = new Rectangle
            {
                Height = 2,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop((Color)ColorConverter.ConvertFromString("#105062"), 0.0),
                        new GradientStop((Color)ColorConverter.ConvertFromString("#18B4E9"), 0.5),
                        new GradientStop((Color)ColorConverter.ConvertFromString("#105062"), 1.0)
                    }
                },
                Margin = new Thickness(50, 0, 50, 15),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            contentPanel.Children.Add(divider1);

            // Build company info with enhanced terminal-style
            var infoBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(40, 18, 180, 233)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 10, 15)
            };
            contentPanel.Children.Add(infoBorder);

            var infoPanel = new StackPanel
            {
                Margin = new Thickness(0)
            };
            infoBorder.Child = infoPanel;

            // Add description section with terminal styling and improved header
            var descriptionBlock = new TextBlock
            {
                Text = "[ SYSTEM INFORMATION ]",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            infoPanel.Children.Add(descriptionBlock);

            // Application description with typewriter-style text
            var appDescriptionBlock = new TextBlock
            {
                Text = "A terminal-style application built for Invoice Cloud clients to efficiently refresh and validate invoice balances through the secure SOAP API service. The application features both single and batch processing capabilities with comprehensive logging and error handling.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 0, 10, 10)
            };
            infoPanel.Children.Add(appDescriptionBlock);

            // Add "System Check" display with simulated diagnostics
            var systemCheck = new TextBlock
            {
                Text = "SYSTEM CHECK: ALL COMPONENTS OPERATIONAL",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5BFF64")),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            infoPanel.Children.Add(systemCheck);

            // Add copyright info with enhanced styling
            var copyrightBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(60, 24, 180, 233)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(5),
                Margin = new Thickness(60, 10, 60, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var copyrightBlock = new TextBlock
            {
                Text = $"© {DateTime.Now.Year} Invoice Cloud, Inc. All Rights Reserved.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextAlignment = TextAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11
            };
            copyrightBorder.Child = copyrightBlock;
            infoPanel.Children.Add(copyrightBorder);

            // Add second divider with improved visual appeal
            var divider2 = new Rectangle
            {
                Height = 2,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop((Color)ColorConverter.ConvertFromString("#105062"), 0.0),
                        new GradientStop((Color)ColorConverter.ConvertFromString("#18B4E9"), 0.5),
                        new GradientStop((Color)ColorConverter.ConvertFromString("#105062"), 1.0)
                    }
                },
                Margin = new Thickness(50, 5, 50, 15),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            contentPanel.Children.Add(divider2);

            // Features border with enhanced terminal styling
            var featuresBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = (SolidColorBrush)_ownerWindow.FindResource("GroupBackgroundBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 10, 15)
            };
            contentPanel.Children.Add(featuresBorder);

            // Features stack panel
            var featuresPanel = new StackPanel();
            featuresBorder.Child = featuresPanel;

            // Features header with enhanced styling
            var featuresHeader = new TextBlock
            {
                Text = "[ FEATURES ]",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            featuresPanel.Children.Add(featuresHeader);

            // Add feature list with enhanced bullet points
            string[] features = new string[]
            {
                "Single invoice processing with real-time balance updates",
                "Batch processing of multiple invoices via CSV with account number support",
                "Comprehensive logging system with session history and search",
                "Terminal-inspired UI with light/dark mode support",
                "Secure invoice balance checking via SOAP API integration",
                "CSV sample generation for easier batch processing setup",
                "Advanced error handling with automatic retry mechanisms",
                "Customer record lookup and balance refresh capabilities",
                "Automated batch scheduling with built-in Schedule Manager",
                "Windows Task Scheduler integration for background execution"
            };

            // Create a two-column grid for features
            var featuresGrid = new Grid();
            featuresGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            featuresGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftPanel = new StackPanel();
            var rightPanel = new StackPanel();

            Grid.SetColumn(leftPanel, 0);
            Grid.SetColumn(rightPanel, 1);

            featuresGrid.Children.Add(leftPanel);
            featuresGrid.Children.Add(rightPanel);

            featuresPanel.Children.Add(featuresGrid);

            // Split features between two columns
            for (int i = 0; i < features.Length; i++)
            {
                var bulletIcon = i % 2 == 0 ? "›" : "»"; // Alternate bullet style
                var color = i % 2 == 0 ?
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")) :
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5BFF64"));

                var featureBlock = new TextBlock
                {
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(5, 0, 5, 8)
                };

                // Create bullet with color
                var bulletRun = new Run(bulletIcon + " ")
                {
                    Foreground = color,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14
                };

                // Create feature text
                var textRun = new Run(features[i]);

                featureBlock.Inlines.Add(bulletRun);
                featureBlock.Inlines.Add(textRun);

                // Add to appropriate column
                if (i < features.Length / 2)
                    leftPanel.Children.Add(featureBlock);
                else
                    rightPanel.Children.Add(featureBlock);
            }

            // SCHEDULE INFO SECTION
            var scheduleBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(30, 24, 180, 233)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 10, 15)
            };
            contentPanel.Children.Add(scheduleBorder);

            var schedulePanel = new StackPanel();
            scheduleBorder.Child = schedulePanel;

            var scheduleHeader = new TextBlock
            {
                Text = "[ SCHEDULE MANAGER & AUTOMATION ]",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            schedulePanel.Children.Add(scheduleHeader);

            var scheduleDesc = new TextBlock
            {
                Text = "Automate batch invoice processing with the built-in Schedule Manager. Create, edit, and manage scheduled tasks to run batch jobs at specific times (once, daily, weekly, or monthly). Optionally, enable Windows Task Scheduler integration to run tasks even when the application is closed.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            schedulePanel.Children.Add(scheduleDesc);

            var scheduleFeatures = new string[]
            {
                "Add, edit, delete, enable/disable scheduled batch jobs",
                "Flexible scheduling: Once, Daily, Weekly, Monthly",
                "Manual [RUN NOW] option for any scheduled task",
                "Track last run time, result, and status for each task",
                "Windows Task Scheduler integration for background execution",
                "All schedule activity is logged for audit and troubleshooting"
            };

            foreach (var feat in scheduleFeatures)
            {
                var featPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 2) };
                featPanel.Children.Add(new TextBlock
                {
                    Text = "•",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                    Margin = new Thickness(0, 0, 5, 0),
                    FontWeight = FontWeights.Bold
                });
                featPanel.Children.Add(new TextBlock
                {
                    Text = feat,
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap
                });
                schedulePanel.Children.Add(featPanel);
            }

            // Technical info with enhanced styling
            var techInfoBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = (SolidColorBrush)_ownerWindow.FindResource("ConsoleBackgroundBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 10, 15)
            };
            contentPanel.Children.Add(techInfoBorder);

            // Technical info stack panel
            var techInfoPanel = new StackPanel();
            techInfoBorder.Child = techInfoPanel;

            // Technical info header with enhanced styling
            var techInfoHeader = new TextBlock
            {
                Text = "[ TECHNICAL SPECIFICATIONS ]",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            techInfoPanel.Children.Add(techInfoHeader);

            // Technical details with improved formatting using a Grid
            var techGrid = new Grid();
            techGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            techGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            techGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            techGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            techGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            techGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            techGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            techGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            string[][] techSpecs = new string[][]
            {
                new string[] { "Framework:", ".NET 8" },
                new string[] { "Language:", "C# 12.0" },
                new string[] { "UI Framework:", "WPF" },
                new string[] { "API:", "SOAP XML" },
                new string[] { "Created:", "May 2025" },
                new string[] { "Build:", $"{_appVersion}.0" }
            };

            for (int i = 0; i < techSpecs.Length; i++)
            {
                var label = new TextBlock
                {
                    Text = techSpecs[i][0],
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(10, 2, 5, 2),
                    FontWeight = FontWeights.Bold
                };

                var value = new TextBlock
                {
                    Text = techSpecs[i][1],
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(0, 2, 0, 2)
                };

                Grid.SetRow(label, i);
                Grid.SetColumn(label, 0);
                Grid.SetRow(value, i);
                Grid.SetColumn(value, 1);

                techGrid.Children.Add(label);
                techGrid.Children.Add(value);
            }

            techInfoPanel.Children.Add(techGrid);

            // Add version check info
            var versionCheckBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 24, 180, 233)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8),
                Margin = new Thickness(10, 10, 10, 0)
            };

            var versionCheckPanel = new StackPanel();
            versionCheckBorder.Child = versionCheckPanel;

            var versionCheckText = new TextBlock
            {
                Text = "VERSION STATUS: CURRENT",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5BFF64")),
                FontFamily = new FontFamily("Consolas"),
                TextAlignment = TextAlignment.Center,
                FontSize = 11
            };
            versionCheckPanel.Children.Add(versionCheckText);

            techInfoPanel.Children.Add(versionCheckBorder);

            // Add bottom buttons with enhanced styling
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var buttonStyle = new Style(typeof(Button));
            buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18536A"))));
            buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            buttonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(15, 8, 15, 8)));
            buttonStyle.Setters.Add(new Setter(Button.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"))));
            buttonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
            buttonStyle.Setters.Add(new Setter(Button.FontFamilyProperty, new FontFamily("Consolas")));
            buttonStyle.Setters.Add(new Setter(Button.MarginProperty, new Thickness(5)));
            buttonStyle.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.Bold));
            buttonStyle.Setters.Add(new Setter(Button.EffectProperty, new DropShadowEffect
            {
                Color = (Color)ColorConverter.ConvertFromString("#18B4E9"),
                ShadowDepth = 0,
                BlurRadius = 10,
                Opacity = 0.6
            }));

            var closeButton = new Button
            {
                Content = "[ CLOSE ]",
                Width = 150,
                Style = buttonStyle
            };
            closeButton.Click += (s, e) => aboutWindow.Close();

            var websiteButton = new Button
            {
                Content = "[ VISIT WEBSITE ]",
                Width = 150,
                Style = buttonStyle
            };
            websiteButton.Click += (s, e) =>
            {
                try
                {
                    // Launch browser to website
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://www.invoicecloud.com",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to open website: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            buttonPanel.Children.Add(websiteButton);
            buttonPanel.Children.Add(closeButton);
            contentPanel.Children.Add(buttonPanel);

            // Show the about window
            aboutWindow.ShowDialog();
        }
    }

    // Include the LogLevel enum in case it's not accessible
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
