using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceBalanceRefresher
{
    public class ThemeManager
    {
        private readonly Window _window;
        private readonly Action<MainWindow.LogLevel, string> _logAction;
        private readonly TextBox? _batchResults;
        private readonly TextBlock? _singleResult;
        private readonly TextBlock? _customerResult;

        public ThemeManager(Window window, Action<MainWindow.LogLevel, string> logAction)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));

            // Find the UI elements if they're in the window
            if (_window is MainWindow mainWindow)
            {
                _batchResults = UIHelper.FindVisualChildren<TextBox>(_window)
                    .FirstOrDefault(tb => tb.Name == "BatchResults");
                _singleResult = UIHelper.FindVisualChildren<TextBlock>(_window)
                    .FirstOrDefault(tb => tb.Name == "SingleResult");
                _customerResult = UIHelper.FindVisualChildren<TextBlock>(_window)
                    .FirstOrDefault(tb => tb.Name == "CustomerResult");
            }
        }

        public void SetLightMode()
        {
            // Change resources to light mode
            Application.Current.Resources["BackgroundBrush"] = Application.Current.Resources["LightBackgroundBrush"];
            Application.Current.Resources["ForegroundBrush"] = Application.Current.Resources["LightForegroundBrush"];
            Application.Current.Resources["BorderBrush"] = Application.Current.Resources["LightBorderBrush"];
            Application.Current.Resources["GroupBackgroundBrush"] = Application.Current.Resources["LightGroupBackgroundBrush"];
            Application.Current.Resources["ConsoleBackgroundBrush"] = Application.Current.Resources["LightConsoleBackgroundBrush"];
            Application.Current.Resources["HighlightBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F4F7"));
            Application.Current.Resources["SeparatorBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
            Application.Current.Resources["ConsoleHeaderBrush"] = Application.Current.Resources["LightConsoleHeaderBrush"];

            // Update button background and text color for light mode
            // Use MintGreenBrush for button backgrounds instead of hardcoded color
            foreach (Button button in UIHelper.FindVisualChildren<Button>(_window))
            {
                button.Background = (SolidColorBrush)Application.Current.Resources["MintGreenBrush"];
                button.Foreground = (SolidColorBrush)Application.Current.Resources["CharcoalBrush"];
                button.BorderBrush = (SolidColorBrush)Application.Current.Resources["MintGreenBrush"];
            }

            // Update controls that aren't automatically updated by resource changes
            UpdateControlsForLightMode();

            // Update the console header grid background - find it by position instead of name
            Grid? consoleHeaderGrid = FindConsoleHeaderGrid();
            if (consoleHeaderGrid != null)
            {
                consoleHeaderGrid.Background = Application.Current.Resources["LightConsoleHeaderBrush"] as Brush;
            }

            _logAction(MainWindow.LogLevel.Info, "Switched to light mode");
        }

        public void SetDarkMode()
        {
            // Change resources to dark mode
            Application.Current.Resources["BackgroundBrush"] = Application.Current.Resources["DarkBackgroundBrush"];
            Application.Current.Resources["ForegroundBrush"] = Application.Current.Resources["DarkForegroundBrush"];
            Application.Current.Resources["BorderBrush"] = Application.Current.Resources["DarkBorderBrush"];
            Application.Current.Resources["GroupBackgroundBrush"] = Application.Current.Resources["DarkGroupBackgroundBrush"];
            Application.Current.Resources["ConsoleBackgroundBrush"] = Application.Current.Resources["DarkConsoleBackgroundBrush"];
            Application.Current.Resources["HighlightBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A4A56"));
            Application.Current.Resources["SeparatorBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
            Application.Current.Resources["ConsoleHeaderBrush"] = Application.Current.Resources["DarkConsoleHeaderBrush"];

            // Update controls that aren't automatically updated by resource changes
            UpdateControlsForDarkMode();

            // Update the console header grid background - find it by position instead of name
            Grid? consoleHeaderGrid = FindConsoleHeaderGrid();
            if (consoleHeaderGrid != null)
            {
                consoleHeaderGrid.Background = Application.Current.Resources["DarkConsoleHeaderBrush"] as Brush;
            }

            _logAction(MainWindow.LogLevel.Info, "Switched to dark mode");
        }

        private Grid? FindConsoleHeaderGrid()
        {
            // The console header grid is in Grid.Row="2" of the main grid
            if (_window.Content is Grid mainGrid)
            {
                foreach (UIElement element in mainGrid.Children)
                {
                    if (element is Grid grid && Grid.GetRow(grid) == 2)
                    {
                        return grid;
                    }
                }
            }
            return null; // Return null if no matching grid is found
        }

        private void UpdateControlsForDarkMode()
        {
            // First update all TextBlocks to ensure they use the correct foreground color
            foreach (TextBlock textBlock in UIHelper.FindVisualChildren<TextBlock>(_window))
            {
                // Only update if not part of a style that should keep its color
                if (!(textBlock.Parent is GroupBox) &&
                    !(textBlock.Parent is MenuItem) &&
                    !textBlock.Text.StartsWith("CONSOLE LOG") &&
                    !textBlock.Text.StartsWith("SYSTEM INFORMATION:") &&
                    !textBlock.Text.StartsWith("FEATURES:") &&
                    !textBlock.Text.StartsWith("TECHNICAL INFORMATION:"))
                {
                    textBlock.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                }
            }

            // Apply dark mode to TextBox backgrounds (they often have hardcoded white backgrounds)
            foreach (TextBox textBox in UIHelper.FindVisualChildren<TextBox>(_window))
            {
                textBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525"));
                textBox.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                textBox.CaretBrush = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                textBox.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                textBox.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;
            }

            // Apply dark mode to any RichTextBox that may have hardcoded backgrounds
            foreach (RichTextBox richTextBox in UIHelper.FindVisualChildren<RichTextBox>(_window))
            {
                richTextBox.Background = new SolidColorBrush(Colors.Transparent);
                richTextBox.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                richTextBox.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;

                // Update selection colors
                richTextBox.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                richTextBox.SelectionTextBrush = Brushes.White;
            }

            // Apply dark mode to Borders including those in deeply nested controls
            foreach (Border border in UIHelper.FindVisualChildren<Border>(_window))
            {
                if (border.Background != null)
                {
                    // Update any white or light-colored backgrounds
                    var brush = border.Background as SolidColorBrush;
                    if (brush != null &&
                        (brush.Color == Colors.White ||
                         brush.Color.ToString() == "#FFF5F5F5" ||
                         brush.Color.ToString() == "#FFF0F0F0"))
                    {
                        border.Background = Application.Current.Resources["DarkGroupBackgroundBrush"] as Brush;
                    }
                }

                border.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;
            }

            // Apply dark mode to BatchResults TextBox (special case with formatting)
            if (_batchResults != null)
            {
                _batchResults.Background = new SolidColorBrush(Colors.Transparent);
                _batchResults.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
            }

            // Update SingleResult and CustomerResult TextBlocks
            if (_singleResult != null)
                _singleResult.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;

            if (_customerResult != null)
                _customerResult.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;

            // Update GroupBox headers and backgrounds
            foreach (GroupBox groupBox in UIHelper.FindVisualChildren<GroupBox>(_window))
            {
                groupBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                groupBox.Background = Application.Current.Resources["DarkGroupBackgroundBrush"] as Brush;
            }

            // Update Menu and MenuItem styles
            foreach (Menu menu in UIHelper.FindVisualChildren<Menu>(_window))
            {
                menu.Background = Application.Current.Resources["DarkBackgroundBrush"] as Brush;
                menu.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                menu.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;
            }

            foreach (MenuItem menuItem in UIHelper.FindVisualChildren<MenuItem>(_window))
            {
                menuItem.Background = Application.Current.Resources["DarkBackgroundBrush"] as Brush;
                menuItem.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                menuItem.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;
            }

            // Update any ScrollViewer backgrounds
            foreach (ScrollViewer scrollViewer in UIHelper.FindVisualChildren<ScrollViewer>(_window))
            {
                scrollViewer.Background = new SolidColorBrush(Colors.Transparent);
            }

            // Update ProgressBar colors
            foreach (ProgressBar progressBar in UIHelper.FindVisualChildren<ProgressBar>(_window))
            {
                progressBar.Background = Application.Current.Resources["DarkConsoleBackgroundBrush"] as Brush;
                progressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                progressBar.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;
            }

            // Update Buttons in dark mode
            foreach (Button button in UIHelper.FindVisualChildren<Button>(_window))
            {
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557"));
                button.Foreground = Brushes.White;
                button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
            }

            // Update RadioButtons in dark mode
            foreach (RadioButton radioButton in UIHelper.FindVisualChildren<RadioButton>(_window))
            {
                radioButton.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                radioButton.Background = Application.Current.Resources["DarkBackgroundBrush"] as Brush;
            }

            // Update CheckBoxes in dark mode
            foreach (CheckBox checkBox in UIHelper.FindVisualChildren<CheckBox>(_window))
            {
                checkBox.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                checkBox.Background = Application.Current.Resources["DarkBackgroundBrush"] as Brush;
            }

            // Force visual refresh - we can't call InvalidateVisual directly since we're not a UIElement
            if (_window is FrameworkElement fe)
            {
                fe.InvalidateVisual();
            }
        }

        private void UpdateControlsForLightMode()
        {
            // First update all TextBlocks to ensure they use the correct foreground color
            foreach (TextBlock textBlock in UIHelper.FindVisualChildren<TextBlock>(_window))
            {
                // Only update if not part of a style that should keep its color
                if (!(textBlock.Parent is GroupBox) &&
                    !(textBlock.Parent is MenuItem) &&
                    !textBlock.Text.StartsWith("CONSOLE LOG") &&
                    !textBlock.Text.StartsWith("SYSTEM INFORMATION:") &&
                    !textBlock.Text.StartsWith("FEATURES:") &&
                    !textBlock.Text.StartsWith("TECHNICAL INFORMATION:"))
                {
                    textBlock.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
                }
            }

            // Apply light mode to TextBox backgrounds
            foreach (TextBox textBox in UIHelper.FindVisualChildren<TextBox>(_window))
            {
                textBox.Background = new SolidColorBrush(Colors.White);
                textBox.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
                textBox.CaretBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#085368"));
                textBox.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F4F7"));
                textBox.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;
            }

            // Apply light mode to any RichTextBox
            foreach (RichTextBox richTextBox in UIHelper.FindVisualChildren<RichTextBox>(_window))
            {
                richTextBox.Background = new SolidColorBrush(Colors.Transparent);
                richTextBox.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
                richTextBox.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;

                // Update selection colors
                richTextBox.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F4F7"));
                richTextBox.SelectionTextBrush = Brushes.Black;
            }

            // Apply light mode to Borders
            foreach (Border border in UIHelper.FindVisualChildren<Border>(_window))
            {
                if (border.Background != null)
                {
                    // Update any dark backgrounds
                    var brush = border.Background as SolidColorBrush;
                    if (brush != null &&
                        (brush.Color.ToString() == "#FF252525" ||
                         brush.Color.ToString() == "#FF2D2D2D" ||
                         brush.Color.ToString() == "#FF1E1E1E"))
                    {
                        border.Background = Application.Current.Resources["LightGroupBackgroundBrush"] as Brush;
                    }
                }

                border.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;
            }

            // Apply light mode to BatchResults TextBox
            if (_batchResults != null)
            {
                _batchResults.Background = new SolidColorBrush(Colors.Transparent);
                _batchResults.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
            }

            // Update SingleResult and CustomerResult TextBlocks
            if (_singleResult != null)
                _singleResult.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;

            if (_customerResult != null)
                _customerResult.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;

            // Update GroupBox headers and backgrounds
            foreach (GroupBox groupBox in UIHelper.FindVisualChildren<GroupBox>(_window))
            {
                groupBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#085368"));
                groupBox.Background = Application.Current.Resources["LightGroupBackgroundBrush"] as Brush;
            }

            // Update Menu and MenuItem styles
            foreach (Menu menu in UIHelper.FindVisualChildren<Menu>(_window))
            {
                menu.Background = Application.Current.Resources["LightBackgroundBrush"] as Brush;
                menu.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#085368"));
                menu.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;
            }

            foreach (MenuItem menuItem in UIHelper.FindVisualChildren<MenuItem>(_window))
            {
                menuItem.Background = Application.Current.Resources["LightBackgroundBrush"] as Brush;
                menuItem.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#085368"));
                menuItem.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;
            }

            // Update any ScrollViewer backgrounds
            foreach (ScrollViewer scrollViewer in UIHelper.FindVisualChildren<ScrollViewer>(_window))
            {
                scrollViewer.Background = new SolidColorBrush(Colors.Transparent);
            }

            // Update ProgressBar colors
            foreach (ProgressBar progressBar in UIHelper.FindVisualChildren<ProgressBar>(_window))
            {
                progressBar.Background = Application.Current.Resources["LightConsoleBackgroundBrush"] as Brush;
                progressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                progressBar.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;
            }

            // Update Buttons in light mode - Use MintGreenBrush for the new style
            foreach (Button button in UIHelper.FindVisualChildren<Button>(_window))
            {
                button.Background = (SolidColorBrush)Application.Current.Resources["MintGreenBrush"];
                button.Foreground = (SolidColorBrush)Application.Current.Resources["CharcoalBrush"];
                button.BorderBrush = (SolidColorBrush)Application.Current.Resources["MintGreenBrush"];
            }

            // Update RadioButtons in light mode
            foreach (RadioButton radioButton in UIHelper.FindVisualChildren<RadioButton>(_window))
            {
                radioButton.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
                radioButton.Background = Application.Current.Resources["LightBackgroundBrush"] as Brush;
            }

            // Update CheckBoxes in light mode
            foreach (CheckBox checkBox in UIHelper.FindVisualChildren<CheckBox>(_window))
            {
                checkBox.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
                checkBox.Background = Application.Current.Resources["LightBackgroundBrush"] as Brush;
            }

            // Force visual refresh - we can't call InvalidateVisual directly since we're not a UIElement
            if (_window is FrameworkElement fe)
            {
                fe.InvalidateVisual();
            }
        }
    }
}
