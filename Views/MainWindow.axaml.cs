using System;
using ASTEM_DB.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace ASTEM_DB.Views
{
    public partial class MainWindow : Window
    {
        private bool _languageSelectorReady;

        public MainWindow()
        {
            InitializeComponent();
            var languageSelector = this.FindControl<ComboBox>("LanguageSelector")
                ?? throw new InvalidOperationException("Language selector was not loaded.");
            var chatScrollViewer = this.FindControl<ScrollViewer>("AiChatScrollViewer")
                ?? throw new InvalidOperationException("AI chat view was not loaded.");

            languageSelector.SelectedIndex = Localization.CurrentLanguageCode == "ja" ? 1 : 0;
            _languageSelectorReady = true;

            var viewModel = new MainWindowViewModel();
            DataContext = viewModel;
            viewModel.AiChatMessages.CollectionChanged += (_, _) =>
                DispatcherTimer.RunOnce(
                    () => chatScrollViewer.ScrollToEnd(),
                    TimeSpan.FromMilliseconds(1),
                    DispatcherPriority.Background
                );
        }

        private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_languageSelectorReady)
                return;

            if (sender is ComboBox languageSelector)
                Localization.SetCulture(languageSelector.SelectedIndex == 1 ? "ja" : "en");
        }

        private void OnCardClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is CardItemViewModel clickedCard)
            {
                if (this.DataContext is MainWindowViewModel vm)
                {
                    if (vm.SelectedCard == clickedCard)
                        vm.IsSidebarVisible = !vm.IsSidebarVisible;
                    else
                    {
                        vm.SelectedCard = clickedCard;
                        vm.IsSidebarVisible = true;
                    }
                }
            }
        }

        private async void OnAiImageButtonClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = Localization.Get("SelectTileImage"),
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType(Localization.Get("ImageFiles"))
                        {
                            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" },
                            MimeTypes = new[] { "image/png", "image/jpeg", "image/webp", "image/bmp" }
                        }
                    }
                });

                if (files.Count == 0)
                    return;

                var selected = files[0];
                var imagePath = selected.Path.IsFile ? selected.Path.LocalPath : selected.Name;
                await vm.SetPendingAiSearchImageAsync(imagePath);
            }
            catch (Exception ex)
            {
                vm.SetAiSearchImageSelectionError(ex.Message);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
