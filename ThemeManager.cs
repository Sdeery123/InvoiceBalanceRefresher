using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;

namespace InvoiceBalanceRefresher
{
    public class ThemeManager
    {
        private readonly Window _window;
        private readonly Action<MainWindow.LogLevel, string> _logAction;
        private readonly System.Windows.Controls.TextBox? _batchResults;
        private readonly System.Windows.Controls.TextBlock? _singleResult;
        private readonly System.Windows.Controls.TextBlock? _customerResult;

        public ThemeManager(Window window, Action<MainWindow.LogLevel, string> logAction)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));

            // Find the UI elements if they're in the window
            if (_window is MainWindow mainWindow)
            {
                _batchResults = UIHelper.FindVisualChildren<System.Windows.Controls.TextBox>(_window)
                    .FirstOrDefault(tb => tb.Name == "BatchResults");
                _singleResult = UIHelper.FindVisualChildren<System.Windows.Controls.TextBlock>(_window)
                    .FirstOrDefault(tb => tb.Name == "SingleResult");
                _customerResult = UIHelper.FindVisualChildren<System.Windows.Controls.TextBlock>(_window)
                    .FirstOrDefault(tb => tb.Name == "CustomerResult");
            }
        }

        public void SetLightMode()
        {
            // Change resources to light mode
            System.Windows.Application.Current.Resources["BackgroundBrush"] = System.Windows.Application.Current.Resources["LightBackgroundBrush"];
            System.Windows.Application.Current.Resources["ForegroundBrush"] = System.Windows.Application.Current.Resources["LightForegroundBrush"];
            System.Windows.Application.Current.Resources["BorderBrush"] = System.Windows.Application.Current.Resources["LightBorderBrush"];
            System.Windows.Application.Current.Resources["GroupBackgroundBrush"] = System.Windows.Application.Current.Resources["LightGroupBackgroundBrush"];
            System.Windows.Application.Current.Resources["ConsoleBackgroundBrush"] = System.Windows.Application.Current.Resources["LightConsoleBackgroundBrush"];
            System.Windows.Application.Current.Resources["HighlightBrush"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8F4F7"));
            System.Windows.Application.Current.Resources["SeparatorBrush"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E0E0E0"));
            System.Windows.Application.Current.Resources["ConsoleHeaderBrush"] = System.Windows.Application.Current.Resources["LightConsoleHeaderBrush"];

            // Update button background and text color for light mode
            // Use MintGreenBrush for button backgrounds instead of hardcoded color
            foreach (System.Windows.Controls.Button button in UIHelper.FindVisualChildren<System.Windows.Controls.Button>(_window))
            {
                button.Background = (SolidColorBrush)System.Windows.Application.Current.Resources["MintGreenBrush"];
                button.Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["CharcoalBrush"];
                button.BorderBrush = (SolidColorBrush)System.Windows.Application.Current.Resources["MintGreenBrush"];
            }

            // Update controls that aren't automatically updated by resource changes
            UpdateControlsForLightMode();

            // Update the console header grid background - find it by position instead of name
            Grid? consoleHeaderGrid = FindConsoleHeaderGrid();
            if (consoleHeaderGrid != null)
            {
                consoleHeaderGrid.Background = System.Windows.Application.Current.Resources["LightConsoleHeaderBrush"] as System.Windows.Media.Brush;
            }

            _logAction(MainWindow.LogLevel.Info, "Switched to light mode");
        }

        public void SetDarkMode()
        {
            // Change resources to dark mode
            System.Windows.Application.Current.Resources["BackgroundBrush"] = System.Windows.Application.Current.Resources["DarkBackgroundBrush"];
            System.Windows.Application.Current.Resources["ForegroundBrush"] = System.Windows.Application.Current.Resources["DarkForegroundBrush"];
            System.Windows.Application.Current.Resources["BorderBrush"] = System.Windows.Application.Current.Resources["DarkBorderBrush"];
            System.Windows.Application.Current.Resources["GroupBackgroundBrush"] = System.Windows.Application.Current.Resources["DarkGroupBackgroundBrush"];
            System.Windows.Application.Current.Resources["ConsoleBackgroundBrush"] = System.Windows.Application.Current.Resources["DarkConsoleBackgroundBrush"];
            System.Windows.Application.Current.Resources["HighlightBrush"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A4A56"));
            System.Windows.Application.Current.Resources["SeparatorBrush"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#444444"));
            System.Windows.Application.Current.Resources["ConsoleHeaderBrush"] = System.Windows.Application.Current.Resources["DarkConsoleHeaderBrush"];

            // Update controls that aren't automatically updated by resource changes
            UpdateControlsForDarkMode();

            // Update the console header grid background - find it by position instead of name
            Grid? consoleHeaderGrid = FindConsoleHeaderGrid();
            if (consoleHeaderGrid != null)
            {
                consoleHeaderGrid.Background = System.Windows.Application.Current.Resources["DarkConsoleHeaderBrush"] as System.Windows.Media.Brush;
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
            foreach (System.Windows.Controls.TextBlock textBlock in UIHelper.FindVisualChildren<System.Windows.Controls.TextBlock>(_window))
            {
                // Only update if not part of a style that should keep its color
                if (!(textBlock.Parent is System.Windows.Controls.GroupBox) &&
                    !(textBlock.Parent is System.Windows.Controls.MenuItem) &&
                    !textBlock.Text.StartsWith("CONSOLE LOG") &&
                    !textBlock.Text.StartsWith("SYSTEM INFORMATION:") &&
                    !textBlock.Text.StartsWith("FEATURES:") &&
                    !textBlock.Text.StartsWith("TECHNICAL INFORMATION:"))
                {
                    textBlock.Foreground = System.Windows.Application.Current.Resources["DarkForegroundBrush"] as System.Windows.Media.Brush;
                }
            }

            // Apply dark mode to TextBox backgrounds (they often have hardcoded white backgrounds)
            foreach (System.Windows.Controls.TextBox textBox in UIHelper.FindVisualChildren<System.Windows.Controls.TextBox>(_window))
            {
                textBox.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252525"));
                textBox.Foreground = System.Windows.Application.Current.Resources["DarkForegroundBrush"] as System.Windows.Media.Brush;
                textBox.CaretBrush = System.Windows.Application.Current.Resources["DarkForegroundBrush"] as System.Windows.Media.Brush;
                textBox.SelectionBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"));
                textBox.BorderBrush = System.Windows.Application.Current.Resources["DarkBorderBrush"] as System.Windows.Media.Brush;
            }

            // Apply dark mode to any RichTextBox that may have hardcoded backgrounds
            foreach (System.Windows.Controls.RichTextBox richTextBox in UIHelper.FindVisualChildren<System.Windows.Controls.RichTextBox>(_window))
            {
                richTextBox.Background = new SolidColorBrush(Colors.Transparent);
                richTextBox.Foreground = System.Windows.Application.Current.Resources["DarkForegroundBrush"] as System.Windows.Media.Brush;
                richTextBox.BorderBrush = System.Windows.Application.Current.Resources["DarkBorderBrush"] as System.Windows.Media.Brush;

                // Update selection colors
                richTextBox.SelectionBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"));
                richTextBox.SelectionTextBrush = System.Windows.Media.Brushes.White;
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
                        border.Background = System.Windows.Application.Current.Resources["DarkGroupBackgroundBrush"] as System.Windows.Media.Brush;
                    }
                }

                border.BorderBrush = System.Windows.Application.Current.Resources["DarkBorderBrush"] as System.Windows.Media.Brush;
            }

            // Apply dark mode to BatchResults TextBox (special case with formatting)
            if (_batchResults != null)
            {
                _batchResults.Background = new SolidColorBrush(Colors.Transparent);
                _batchResults.Foreground = System.Windows.Application.Current.Resources["DarkForegroundBrush"] as System.Windows.Media.Brush;
            }

            // Update SingleResult and CustomerResult TextBlocks
            if (_singleResult != null)
                _singleResult.Foreground = System.Windows.Application.Current.Resources["DarkForegroundBrush"] as System.Windows.Media.Brush;

            if (_customerResult != null)
                _customerResult.Foreground = System.Windows.Application.Current.Resources["DarkForegroundBrush"] as System.Windows.Media.Brush;

            // Update GroupBox headers and backgrounds
            foreach (System.Windows.Controls.GroupBox groupBox in UIHelper.FindVisualChildren<System.Windows.Controls.GroupBox>(_window))
            {
                groupBox.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"));
                groupBox.Background = System.Windows.Application.Current.Resources["DarkGroupBackgroundBrush"] as System.Windows.Media.Brush;
            }

            // Update Menu and MenuItem styles
            foreach (System.Windows.Controls.Menu menu in UIHelper.FindVisualChildren<System.Windows.Controls.Menu>(_window))
            {
                menu.Background = System.Windows.Application.Current.Resources["DarkBackgroundBrush"] as System.Windows.Media.Brush;
                menu.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"));
                menu.BorderBrush = System.Windows.Application.Current.Resources["DarkBorderBrush"] as System.Windows.Media.Brush;
            }

            foreach (System.Windows.Controls.MenuItem menuItem in UIHelper.FindVisualChildren<System.Windows.Controls.MenuItem>(_window))
            {
                menuItem.Background = System.Windows.Application.Current.Resources["DarkBackgroundBrush"] as System.Windows.Media.Brush;
                menuItem.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"));
                menuItem.BorderBrush = System.Windows.Application.Current.Resources["DarkBorderBrush"] as System.Windows.Media.Brush;
            }

            // Update any ScrollViewer backgrounds
            foreach (System.Windows.Controls.ScrollViewer scrollViewer in UIHelper.FindVisualChildren<System.Windows.Controls.ScrollViewer>(_window))
            {
                scrollViewer.Background = new SolidColorBrush(Colors.Transparent);
            }

            // Update ProgressBar colors
            foreach (System.Windows.Controls.ProgressBar progressBar in UIHelper.FindVisualChildren<System.Windows.Controls.ProgressBar>(_window))
            {
                progressBar.Background = System.Windows.Application.Current.Resources["DarkConsoleBackgroundBrush"] as System.Windows.Media.Brush;
                progressBar.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"));
                progressBar.BorderBrush = System.Windows.Application.Current.Resources["DarkBorderBrush"] as System.Windows.Media.Brush;
            }

            // Update Buttons in dark mode
            foreach (System.Windows.Controls.Button button in UIHelper.FindVisualChildren<System.Windows.Controls.Button>(_window))
            {
                button.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#064557"));
                button.Foreground = System.Windows.Media.Brushes.White;
                button.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"));
            }

            // Update RadioButtons in dark mode
            foreach (System.Windows.Controls.RadioButton radioButton in UIHelper.FindVisualChildren<System.Windows.Controls.RadioButton>(_window))
            {
                radioButton.Foreground = System.Windows.Application.Current.Resources["DarkForegroundBrush"] as System.Windows.Media.Brush;
                radioButton.Background = System.Windows.Application.Current.Resources["DarkBackgroundBrush"] as System.Windows.Media.Brush;
            }

            // Update CheckBoxes in dark mode
            foreach (System.Windows.Controls.CheckBox checkBox in UIHelper.FindVisualChildren<System.Windows.Controls.CheckBox>(_window))
            {
                checkBox.Foreground = System.Windows.Application.Current.Resources["DarkForegroundBrush"] as System.Windows.Media.Brush;
                checkBox.Background = System.Windows.Application.Current.Resources["DarkBackgroundBrush"] as System.Windows.Media.Brush;
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
            foreach (System.Windows.Controls.TextBlock textBlock in UIHelper.FindVisualChildren<System.Windows.Controls.TextBlock>(_window))
            {
                // Only update if not part of a style that should keep its color
                if (!(textBlock.Parent is System.Windows.Controls.GroupBox) &&
                    !(textBlock.Parent is System.Windows.Controls.MenuItem) &&
                    !textBlock.Text.StartsWith("CONSOLE LOG") &&
                    !textBlock.Text.StartsWith("SYSTEM INFORMATION:") &&
                    !textBlock.Text.StartsWith("FEATURES:") &&
                    !textBlock.Text.StartsWith("TECHNICAL INFORMATION:"))
                {
                    textBlock.Foreground = System.Windows.Application.Current.Resources["LightForegroundBrush"] as System.Windows.Media.Brush;
                }
            }

            // Apply light mode to TextBox backgrounds
            foreach (System.Windows.Controls.TextBox textBox in UIHelper.FindVisualChildren<System.Windows.Controls.TextBox>(_window))
            {
                textBox.Background = new SolidColorBrush(Colors.White);
                textBox.Foreground = System.Windows.Application.Current.Resources["LightForegroundBrush"] as System.Windows.Media.Brush;
                textBox.CaretBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#085368"));
                textBox.SelectionBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8F4F7"));
                textBox.BorderBrush = System.Windows.Application.Current.Resources["LightBorderBrush"] as System.Windows.Media.Brush;
            }

            // Apply light mode to any RichTextBox
            foreach (System.Windows.Controls.RichTextBox richTextBox in UIHelper.FindVisualChildren<System.Windows.Controls.RichTextBox>(_window))
            {
                richTextBox.Background = new SolidColorBrush(Colors.Transparent);
                richTextBox.Foreground = System.Windows.Application.Current.Resources["LightForegroundBrush"] as System.Windows.Media.Brush;
                richTextBox.BorderBrush = System.Windows.Application.Current.Resources["LightBorderBrush"] as System.Windows.Media.Brush;

                // Update selection colors
                richTextBox.SelectionBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8F4F7"));
                richTextBox.SelectionTextBrush = System.Windows.Media.Brushes.Black;
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
                        border.Background = System.Windows.Application.Current.Resources["LightGroupBackgroundBrush"] as System.Windows.Media.Brush;
                    }
                }

                border.BorderBrush = System.Windows.Application.Current.Resources["LightBorderBrush"] as System.Windows.Media.Brush;
            }

            // Apply light mode to BatchResults TextBox
            if (_batchResults != null)
            {
                _batchResults.Background = new SolidColorBrush(Colors.Transparent);
                _batchResults.Foreground = System.Windows.Application.Current.Resources["LightForegroundBrush"] as System.Windows.Media.Brush;
            }

            // Update SingleResult and CustomerResult TextBlocks
            if (_singleResult != null)
                _singleResult.Foreground = System.Windows.Application.Current.Resources["LightForegroundBrush"] as System.Windows.Media.Brush;

            if (_customerResult != null)
                _customerResult.Foreground = System.Windows.Application.Current.Resources["LightForegroundBrush"] as System.Windows.Media.Brush;

            // Update GroupBox headers and backgrounds
            foreach (System.Windows.Controls.GroupBox groupBox in UIHelper.FindVisualChildren<System.Windows.Controls.GroupBox>(_window))
            {
                groupBox.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#085368"));
                groupBox.Background = System.Windows.Application.Current.Resources["LightGroupBackgroundBrush"] as System.Windows.Media.Brush;
            }

            // Update Menu and MenuItem styles
            foreach (System.Windows.Controls.Menu menu in UIHelper.FindVisualChildren<System.Windows.Controls.Menu>(_window))
            {
                menu.Background = System.Windows.Application.Current.Resources["LightBackgroundBrush"] as System.Windows.Media.Brush;
                menu.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#085368"));
                menu.BorderBrush = System.Windows.Application.Current.Resources["LightBorderBrush"] as System.Windows.Media.Brush;
            }

            foreach (System.Windows.Controls.MenuItem menuItem in UIHelper.FindVisualChildren<System.Windows.Controls.MenuItem>(_window))
            {
                menuItem.Background = System.Windows.Application.Current.Resources["LightBackgroundBrush"] as System.Windows.Media.Brush;
                menuItem.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#085368"));
                menuItem.BorderBrush = System.Windows.Application.Current.Resources["LightBorderBrush"] as System.Windows.Media.Brush;
            }

            // Update any ScrollViewer backgrounds
            foreach (System.Windows.Controls.ScrollViewer scrollViewer in UIHelper.FindVisualChildren<System.Windows.Controls.ScrollViewer>(_window))
            {
                scrollViewer.Background = new SolidColorBrush(Colors.Transparent);
            }

            // Update ProgressBar colors
            foreach (System.Windows.Controls.ProgressBar progressBar in UIHelper.FindVisualChildren<System.Windows.Controls.ProgressBar>(_window))
            {
                progressBar.Background = System.Windows.Application.Current.Resources["LightConsoleBackgroundBrush"] as System.Windows.Media.Brush;
                progressBar.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18B4E9"));
                progressBar.BorderBrush = System.Windows.Application.Current.Resources["LightBorderBrush"] as System.Windows.Media.Brush;
            }

            // Update Buttons in light mode - Use MintGreenBrush for the new style
            foreach (System.Windows.Controls.Button button in UIHelper.FindVisualChildren<System.Windows.Controls.Button>(_window))
            {
                button.Background = (SolidColorBrush)System.Windows.Application.Current.Resources["MintGreenBrush"];
                button.Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["CharcoalBrush"];
                button.BorderBrush = (SolidColorBrush)System.Windows.Application.Current.Resources["MintGreenBrush"];
            }

            // Update RadioButtons in light mode
            foreach (System.Windows.Controls.RadioButton radioButton in UIHelper.FindVisualChildren<System.Windows.Controls.RadioButton>(_window))
            {
                radioButton.Foreground = System.Windows.Application.Current.Resources["LightForegroundBrush"] as System.Windows.Media.Brush;
                radioButton.Background = System.Windows.Application.Current.Resources["LightBackgroundBrush"] as System.Windows.Media.Brush;
            }

            // Update CheckBoxes in light mode
            foreach (System.Windows.Controls.CheckBox checkBox in UIHelper.FindVisualChildren<System.Windows.Controls.CheckBox>(_window))
            {
                checkBox.Foreground = System.Windows.Application.Current.Resources["LightForegroundBrush"] as System.Windows.Media.Brush;
                checkBox.Background = System.Windows.Application.Current.Resources["LightBackgroundBrush"] as System.Windows.Media.Brush;
            }

            // Force visual refresh - we can't call InvalidateVisual directly since we're not a UIElement
            if (_window is FrameworkElement fe)
            {
                fe.InvalidateVisual();
            }
        }
    }
}
