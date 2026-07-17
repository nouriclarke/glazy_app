using System;
using ASTEM_DB.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System.IO;
using System.Linq;

namespace ASTEM_DB.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
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

        private async void OnPickImageClicked(object? sender, RoutedEventArgs e)
        {
            var files = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a tile image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp" }
                    }
                }
            });

            if (files.Count == 0) return;

            var path = files[0].Path.LocalPath;

            if (this.DataContext is MainWindowViewModel vm)
            {
                vm.SelectedImagePath = path;

                // Load and show the preview image
                var preview = this.FindControl<Image>("ImagePreview");
                if (preview != null)
                {
                    await using var stream = File.OpenRead(path);
                    preview.Source = new Bitmap(stream);
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
                    Title = "Select a tile image",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Image files")
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
                vm.SetPendingAiSearchImage(imagePath);
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