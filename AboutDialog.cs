﻿using System;
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
                Height = 700, // Increased height for credential manager info
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

            // Add a "terminal status" line at the top
            var statusBar = new Border
            {
                Height = 25,
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18536A")),
                VerticalAlignment = VerticalAlignment.Top,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#124050"))
            };

            var statusPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            statusBar.Child = statusPanel;

            statusPanel.Children.Add(new TextBlock
            {
                Text = "SYSTEM: ONLINE",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B2F0FF")),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            });

            // Add version and user info
            statusPanel.Children.Add(new TextBlock
            {
                Text = $" • TIME: 2025-05-11 03:53:14 UTC • USER: Sdeery123",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B2F0FF")),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10
            });

            // Add a "terminal" blinking cursor effect
            var cursorBorder = new Border
            {
                Width = 8,
                Height = 15,
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
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
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
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
            var divider1 = new System.Windows.Shapes.Rectangle
            {
                Height = 2,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#105062"), 0.0),
                        new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"), 0.5),
                        new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#105062"), 1.0)
                    }
                },
                Margin = new Thickness(50, 0, 50, 15),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            contentPanel.Children.Add(divider1);

            // Build company info with enhanced terminal-style
            var infoBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 18, 180, 233)),
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
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            infoPanel.Children.Add(descriptionBlock);

            // Application description with typewriter-style text - Updated to mention new features
            var appDescriptionBlock = new TextBlock
            {
                Text = "A terminal-style application built for Invoice Cloud clients to efficiently refresh and validate invoice balances through the secure SOAP API service. Features include single invoice processing, batch processing via CSV files, secure credential management, enhanced RTDR options, automated maintenance, comprehensive scheduling options, and configurable rate limiting to ensure optimal API interactions.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 0, 10, 10)
            };
            infoPanel.Children.Add(appDescriptionBlock);

            // Add "System Check" display with simulated diagnostics
            var systemCheck = new TextBlock
            {
                Text = "SYSTEM CHECK: ALL COMPONENTS OPERATIONAL",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5BFF64")),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            infoPanel.Children.Add(systemCheck);

            // Add copyright info with enhanced styling
            var copyrightBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 24, 180, 233)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(5),
                Margin = new Thickness(60, 10, 60, 10),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };

            var copyrightBlock = new TextBlock
            {
                Text = $"© {DateTime.Now.Year} Invoice Cloud, Inc. All Rights Reserved.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextAlignment = TextAlignment.Center,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11
            };
            copyrightBorder.Child = copyrightBlock;
            infoPanel.Children.Add(copyrightBorder);

            // Add second divider with improved visual appeal
            var divider2 = new System.Windows.Shapes.Rectangle
            {
                Height = 2,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#105062"), 0.0),
                        new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"), 0.5),
                        new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#105062"), 1.0)
                    }
                },
                Margin = new Thickness(50, 5, 50, 15),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            contentPanel.Children.Add(divider2);

            // Features border with enhanced terminal styling
            // Features border with enhanced terminal styling
            var featuresBorder = new Border
            {
                BorderBrush = _ownerWindow.FindResource("BorderBrush") as Brush,
                BorderThickness = new Thickness(1),
                Background = _ownerWindow.FindResource("GroupBackgroundBrush") as Brush,
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
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            featuresPanel.Children.Add(featuresHeader);

            // Add feature list with enhanced bullet points - Updated to remove Windows Task Scheduler
            string[] features = new string[]
            {
                "Single invoice processing with real-time balance updates",
                "Batch processing of multiple invoices via CSV with account number support",
                "Secure credential management with multiple credential set support",
                "Comprehensive logging system with session history and search",
                "Terminal-inspired UI with light/dark mode support",
                "Secure invoice balance checking via SOAP API integration",
                "CSV sample generation for easier batch processing setup",
                "Advanced error handling with automatic retry mechanisms",
                "Customer record lookup and balance refresh capabilities",
                "Comprehensive batch scheduling with multiple frequency options",
                "Flexible scheduling options (minutely, hourly, daily, weekly, monthly, etc.)",
                "Configurable API rate limiting to prevent throttling",
                "Enhanced RTDR balance check options",
                "Automated log maintenance and data optimization"
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
                var bulletIcon = i % 2 == 0 ? "►" : "•"; // Alternate bullet style
                var color = i % 2 == 0 ?
                    new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")) :
                    new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5BFF64"));

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
                if (i < features.Length / 2 + (features.Length % 2))
                    leftPanel.Children.Add(featureBlock);
                else
                    rightPanel.Children.Add(featureBlock);
            }

            // CREDENTIAL MANAGER SECTION (NEW)
            var credentialBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 24, 180, 233)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 10, 15)
            };
            contentPanel.Children.Add(credentialBorder);

            var credentialPanel = new StackPanel();
            credentialBorder.Child = credentialPanel;

            var credentialHeader = new TextBlock
            {
                Text = "[ CREDENTIAL MANAGER ]",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            credentialPanel.Children.Add(credentialHeader);

            var credentialDesc = new TextBlock
            {
                Text = "The Credential Manager provides secure storage and management of multiple API credential sets. Save and switch between different billers or environments (test/production) without re-entering your credentials each time.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            credentialPanel.Children.Add(credentialDesc);

            var credentialFeatures = new string[]
            {
                "Store multiple named credential sets securely",
                "Switch between environments with a single click",
                "DPAPI encryption tied to Windows user account",
                "Dedicated credential management interface",
                "Integration with batch processing and scheduling",
                "Save, edit, delete credential sets with user-friendly UI"
            };

            foreach (var feat in credentialFeatures)
            {
                var featPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 2) };
                featPanel.Children.Add(new TextBlock
                {
                    Text = "•",
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
                    Margin = new Thickness(0, 0, 5, 0),
                    FontWeight = FontWeights.Bold
                });
                featPanel.Children.Add(new TextBlock
                {
                    Text = feat,
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap
                });
                credentialPanel.Children.Add(featPanel);
            }

            // Security status indicator
            var securityStatusBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 91, 255, 100)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8),
                Margin = new Thickness(10, 10, 10, 0)
            };

            var securityStatus = new TextBlock
            {
                Text = "SECURITY STATUS: CREDENTIALS ENCRYPTED",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5BFF64")),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                TextAlignment = TextAlignment.Center,
                FontSize = 11
            };
            securityStatusBorder.Child = securityStatus;
            credentialPanel.Children.Add(securityStatusBorder);

            // RATE LIMITING SECTION (NEW)
            var rateLimitBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 24, 180, 233)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 10, 15)
            };
            contentPanel.Children.Add(rateLimitBorder);

            var rateLimitPanel = new StackPanel();
            rateLimitBorder.Child = rateLimitPanel;

            var rateLimitHeader = new TextBlock
            {
                Text = "[ RATE LIMITING ]",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            rateLimitPanel.Children.Add(rateLimitHeader);

            var rateLimitDesc = new TextBlock
            {
                Text = "The application includes configurable rate limiting to prevent API throttling and ensure reliable performance when processing large batches of invoices. Rate limiting controls the frequency and timing of API requests to optimize throughput while respecting server limits.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            rateLimitPanel.Children.Add(rateLimitDesc);

            // Create a grid for rate limit settings
            var rateLimitGrid = new Grid();
            rateLimitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rateLimitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            rateLimitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rateLimitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rateLimitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rateLimitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rateLimitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            string[][] rateSettings = new string[][]
            {
                new string[] { "Request Interval:", "500ms (Default)" },
                new string[] { "Request Threshold:", "50 requests (Default)" },
                new string[] { "Cooldown Period:", "5000ms (Default)" },
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
                    Margin = new Thickness(15, 3, 15, 3)
                };

                var settingValue = new TextBlock
                {
                    Text = rateSettings[i][1],
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 3)
                };

                Grid.SetRow(settingLabel, i);
                Grid.SetColumn(settingLabel, 0);
                Grid.SetRow(settingValue, i);
                Grid.SetColumn(settingValue, 1);

                rateLimitGrid.Children.Add(settingLabel);
                rateLimitGrid.Children.Add(settingValue);
            }

            rateLimitPanel.Children.Add(rateLimitGrid);

            var rateLimitFeatures = new string[]
            {
                "Configurable request interval to control API request frequency",
                "Adjustable request threshold to manage API load",
                "Automatic cooldown periods to prevent request bursts",
                "Intelligent retry logic for rate-limited requests",
                "Enable/disable option for testing environments"
            };

            var rateLimitFeaturesHeading = new TextBlock
            {
                Text = "Key Features:",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush2"),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 5)
            };
            rateLimitPanel.Children.Add(rateLimitFeaturesHeading);

            foreach (var feat in rateLimitFeatures)
            {
                var featPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 2) };
                featPanel.Children.Add(new TextBlock
                {
                    Text = "•",
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E55555")),
                    Margin = new Thickness(0, 0, 5, 0),
                    FontWeight = FontWeights.Bold
                });
                featPanel.Children.Add(new TextBlock
                {
                    Text = feat,
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap
                });
                rateLimitPanel.Children.Add(featPanel);
            }

            // RTDR Special Options Header
            var rtdrHeader = new TextBlock
            {
                Text = "RTDR Special Options:",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush2"),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 15, 0, 5)
            };
            rateLimitPanel.Children.Add(rtdrHeader);

            // Add RTDR options
            var rtdrOptions = new string[]
            {
                "RTDR - Override 6 Hour Previous RTDR Check: Forces refresh regardless of recent successful refreshes",
                "Disable 24-hour payment check on RTDR for AutoPay Payments: Overrides hold period for AutoPay invoices",
                "Disable 24-hour payment check for RTDR: Allows balance refresh on invoices with recent payment activity"
            };

            foreach (var option in rtdrOptions)
            {
                var optionPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 2) };
                optionPanel.Children.Add(new TextBlock
                {
                    Text = "•",
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E55555")),
                    Margin = new Thickness(0, 0, 5, 0),
                    FontWeight = FontWeights.Bold
                });
                optionPanel.Children.Add(new TextBlock
                {
                    Text = option,
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap
                });
                rateLimitPanel.Children.Add(optionPanel);
            }

            // Add a note about RTDR options
            var rtdrNote = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 229, 85, 85)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8),
                Margin = new Thickness(10, 5, 10, 5)
            };

            var rtdrNoteText = new TextBlock
            {
                Text = "Note: All RTDR options are OFF by default and require Invoice Cloud support to enable.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic,
                FontSize = 11
            };
            rtdrNote.Child = rtdrNoteText;
            rateLimitPanel.Children.Add(rtdrNote);

            // Rate limit status indicator
            var rateLimitStatusBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 229, 85, 85)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8),
                Margin = new Thickness(10, 10, 10, 0)
            };

            var rateLimitStatus = new TextBlock
            {
                Text = "STATUS: RATE LIMITING ACTIVE",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E55555")),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                TextAlignment = TextAlignment.Center,
                FontSize = 11
            };
            rateLimitStatusBorder.Child = rateLimitStatus;
            rateLimitPanel.Children.Add(rateLimitStatusBorder);

            // SCHEDULE INFO SECTION - UPDATED
            var scheduleBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 24, 180, 233)),
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
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            schedulePanel.Children.Add(scheduleHeader);

            var scheduleDesc = new TextBlock
            {
                Text = "Automate batch invoice processing with the built-in Schedule Manager. Create, edit, and manage scheduled tasks using a comprehensive set of scheduling options, from minute-by-minute execution to quarterly scheduling.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            schedulePanel.Children.Add(scheduleDesc);

            // Add a grid for frequency options
            var scheduleFreqGrid = new Grid();
            scheduleFreqGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            scheduleFreqGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            scheduleFreqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            scheduleFreqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            scheduleFreqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            scheduleFreqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            scheduleFreqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            string[][] freqOptions = new string[][]
            {
                new string[] { "Minutes:", "Run every X minutes (e.g., every 15, 30, or 45 minutes)" },
                new string[] { "Hourly:", "Run every X hours (e.g., every 1, 2, or 4 hours)" },
                new string[] { "Daily:", "Run every X days at specific time" },
                new string[] { "Weekly:", "Run on specific days of week at specific time" },
                new string[] { "Monthly:", "Run on specific days of selected months" }
            };

            for (int i = 0; i < freqOptions.Length; i++)
            {
                var freqLabel = new TextBlock
                {
                    Text = freqOptions[i][0],
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(15, 3, 15, 3)
                };

                var freqDesc = new TextBlock
                {
                    Text = freqOptions[i][1],
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 3)
                };

                Grid.SetRow(freqLabel, i);
                Grid.SetColumn(freqLabel, 0);
                Grid.SetRow(freqDesc, i);
                Grid.SetColumn(freqDesc, 1);

                scheduleFreqGrid.Children.Add(freqLabel);
                scheduleFreqGrid.Children.Add(freqDesc);
            }

            schedulePanel.Children.Add(scheduleFreqGrid);

            var scheduleText = new TextBlock
            {
                Text = "Advanced scheduling options:",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush2"),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 5)
            };
            schedulePanel.Children.Add(scheduleText);

            var scheduleFeatures = new string[]
            {
                "BiWeekly: Run on specific days, alternating between even and odd weeks",
                "Quarterly: Run on specific days in selected months of each quarter",
                "WorkDays: Run on weekdays only (Monday-Friday)",
                "Once: Run a single time at specified date/time",
                "Multiple Times Daily: Configure multiple specific times throughout the day",
                "Add, edit, delete, enable/disable scheduled batch jobs",
                "Select saved credential set for each scheduled task",
                "Manual [RUN NOW] option for immediate execution of any task",
                "Track last run time, result, and status for each scheduled task",
                "All schedule activity is logged for audit and troubleshooting"
            };

            foreach (var feat in scheduleFeatures)
            {
                var featPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 2) };
                featPanel.Children.Add(new TextBlock
                {
                    Text = "•",
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
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

            // Add a note about application required for execution
            var scheduleNote = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 24, 180, 233)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8),
                Margin = new Thickness(10, 10, 10, 0)
            };

            var scheduleNoteText = new TextBlock
            {
                Text = "Note: The application must be running for scheduled tasks to execute. Consider using auto-start options for unattended execution.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic,
                FontSize = 11
            };
            scheduleNote.Child = scheduleNoteText;
            schedulePanel.Children.Add(scheduleNote);

            // MAINTENANCE SYSTEM SECTION (NEW)
            var maintenanceBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 24, 180, 233)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 10, 15)
            };
            contentPanel.Children.Add(maintenanceBorder);

            var maintenancePanel = new StackPanel();
            maintenanceBorder.Child = maintenancePanel;

            var maintenanceHeader = new TextBlock
            {
                Text = "[ MAINTENANCE SYSTEM ]",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            maintenancePanel.Children.Add(maintenanceHeader);

            var maintenanceDesc = new TextBlock
            {
                Text = "The application includes an automated maintenance system that helps keep the application running smoothly by managing log files and performing data optimization tasks.",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            maintenancePanel.Children.Add(maintenanceDesc);

            var maintenanceFeatures = new string[]
            {
                "Automatic log cleanup with configurable retention period",
                "Session file management to prevent disk space issues",
                "Data optimization for improved application performance",
                "Configurable maintenance frequency (Every Startup, Daily, Weekly, Monthly)",
                "Manual maintenance option with detailed logging",
                "Comprehensive maintenance settings interface"
            };

            foreach (var feat in maintenanceFeatures)
            {
                var featPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 2) };
                featPanel.Children.Add(new TextBlock
                {
                    Text = "•",
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5BFF64")),
                    Margin = new Thickness(0, 0, 5, 0),
                    FontWeight = FontWeights.Bold
                });
                featPanel.Children.Add(new TextBlock
                {
                    Text = feat,
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap
                });
                maintenancePanel.Children.Add(featPanel);
            }

            // Add maintenance status indicator
            var maintenanceStatusBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 91, 255, 100)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8),
                Margin = new Thickness(10, 10, 10, 0)
            };

            var maintenanceStatus = new TextBlock
            {
                Text = "STATUS: MAINTENANCE SYSTEM ACTIVE",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5BFF64")),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                TextAlignment = TextAlignment.Center,
                FontSize = 11
            };
            maintenanceStatusBorder.Child = maintenanceStatus;
            maintenancePanel.Children.Add(maintenanceStatusBorder);

            // Technical info with enhanced styling
            var techInfoBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = _ownerWindow.FindResource("ConsoleBackgroundBrush") as Brush,
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
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
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
            techGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            string[][] techSpecs = new string[][]
            {
                new string[] { "Framework:", ".NET 8" },
                new string[] { "Language:", "C# 12.0" },
                new string[] { "UI Framework:", "WPF" },
                new string[] { "API:", "SOAP XML" },
                new string[] { "Encryption:", "Windows DPAPI" },
                new string[] { "Created:", "May 2025" },
                new string[] { "Build:", $"{_appVersion}.0" }
            };

            for (int i = 0; i < techSpecs.Length; i++)
            {
                var label = new TextBlock
                {
                    Text = techSpecs[i][0],
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Margin = new Thickness(10, 2, 5, 2),
                    FontWeight = FontWeights.Bold
                };

                var value = new TextBlock
                {
                    Text = techSpecs[i][1],
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
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
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 24, 180, 233)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8),
                Margin = new Thickness(10, 10, 10, 0)
            };

            var versionCheckPanel = new StackPanel();
            versionCheckBorder.Child = versionCheckPanel;

            var versionCheckText = new TextBlock
            {
                Text = "VERSION STATUS: CURRENT",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5BFF64")),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                TextAlignment = TextAlignment.Center,
                FontSize = 11
            };
            versionCheckPanel.Children.Add(versionCheckText);

            techInfoPanel.Children.Add(versionCheckBorder);

            // Add current system info
            techGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            techGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var osLabel = new TextBlock
            {
                Text = "OS:",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(10, 2, 5, 2),
                FontWeight = FontWeights.Bold
            };

            var osValue = new TextBlock
            {
                Text = Environment.OSVersion.ToString(),
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(0, 2, 0, 2)
            };

            Grid.SetRow(osLabel, techSpecs.Length);
            Grid.SetColumn(osLabel, 0);
            Grid.SetRow(osValue, techSpecs.Length);
            Grid.SetColumn(osValue, 1);

            techGrid.Children.Add(osLabel);
            techGrid.Children.Add(osValue);

            var userLabel = new TextBlock
            {
                Text = "Current User:",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(10, 2, 5, 2),
                FontWeight = FontWeights.Bold
            };

            var userValue = new TextBlock
            {
                Text = Environment.UserName,
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(0, 2, 0, 2)
            };

            Grid.SetRow(userLabel, techSpecs.Length + 1);
            Grid.SetColumn(userLabel, 0);
            Grid.SetRow(userValue, techSpecs.Length + 1);
            Grid.SetColumn(userValue, 1);

            techGrid.Children.Add(userLabel);
            techGrid.Children.Add(userValue);

            // Add a "Powered by" section under the feature section
            var poweredByBorder = new Border
            {
                BorderBrush = (SolidColorBrush)_ownerWindow.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 24, 180, 233)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(10, 0, 10, 15)
            };
            contentPanel.Children.Add(poweredByBorder);

            var poweredByPanel = new StackPanel();
            poweredByBorder.Child = poweredByPanel;

            var poweredByHeader = new TextBlock
            {
                Text = "[ POWERED BY ]",
                Foreground = (SolidColorBrush)_ownerWindow.FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            poweredByPanel.Children.Add(poweredByHeader);

            // Create a grid for technology icons
            var techStackGrid = new Grid();
            for (int i = 0; i < 6; i++)
            {
                techStackGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            poweredByPanel.Children.Add(techStackGrid);

            // Add technology icons/labels
            string[] technologies = { ".NET 8", "C# 12", "WPF", "XAML", "XML", "DPAPI" };
            for (int i = 0; i < technologies.Length; i++)
            {
                var techPanel = new StackPanel { Margin = new Thickness(5) };
                var techLabel = new TextBlock
                {
                    Text = technologies[i],
                    Foreground = (SolidColorBrush)_ownerWindow.FindResource("ForegroundBrush"),
                    TextAlignment = TextAlignment.Center,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 12,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                techPanel.Children.Add(techLabel);

                Grid.SetColumn(techPanel, i);
                techStackGrid.Children.Add(techPanel);
            }

            // Add bottom buttons with enhanced styling
            var buttonPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var buttonStyle = new Style(typeof(System.Windows.Controls.Button));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18536A"))));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.ForegroundProperty, System.Windows.Media.Brushes.White));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.PaddingProperty, new Thickness(15, 8, 15, 8)));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.BorderBrushProperty, new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"))));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.BorderThicknessProperty, new Thickness(1)));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas")));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.MarginProperty, new Thickness(5)));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.FontWeightProperty, FontWeights.Bold));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.EffectProperty, new DropShadowEffect
            {
                Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"),
                ShadowDepth = 0,
                BlurRadius = 10,
                Opacity = 0.6
            }));

            var closeButton = new System.Windows.Controls.Button
            {
                Content = "[ CLOSE ]",
                Width = 150,
                Style = buttonStyle
            };
            closeButton.Click += (s, e) => aboutWindow.Close();

            var websiteButton = new System.Windows.Controls.Button
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
                    System.Windows.MessageBox.Show($"Unable to open website: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
