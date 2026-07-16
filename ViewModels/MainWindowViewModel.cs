using ASTEM_DB.Services;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ColorMine.ColorSpaces;
using ColorMine.ColorSpaces.Comparisons;
using Avalonia.Media;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASTEM_DB.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly DatabaseService _db = new();
        private readonly SearchService _searchService = new();

        private ObservableCollection<CardItemViewModel> _cardItems = new ObservableCollection<CardItemViewModel>();
        public ObservableCollection<CardItemViewModel> CardItems
        {
            get => _cardItems;
            set => this.RaiseAndSetIfChanged(ref _cardItems, value);
        }

        public ObservableCollection<string> GlazeTypes { get; } = new();
        public ObservableCollection<string> SurfaceConditions { get; } = new();

        private string? _selectedGlazeType;
        public string? SelectedGlazeType
        {
            get => _selectedGlazeType;
            set => this.RaiseAndSetIfChanged(ref _selectedGlazeType, value);
        }

        private string? _selectedSurfaceCondition;
        public string? SelectedSurfaceCondition
        {
            get => _selectedSurfaceCondition;
            set => this.RaiseAndSetIfChanged(ref _selectedSurfaceCondition, value);
        }

        public ObservableCollection<string> FiringTypes { get; } = new();

        private string? _selectedFiringType;
        public string? SelectedFiringType
        {
            get => _selectedFiringType;
            set => this.RaiseAndSetIfChanged(ref _selectedFiringType, value);
        }

        private CardItemViewModel? _selectedCard;
        public CardItemViewModel? SelectedCard
        {
            get => _selectedCard;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedCard, value);
                IsSidebarVisible = value != null;
            }
        }

        private bool _sortByNameChecked;
        public bool SortByNameChecked
        {
            get => _sortByNameChecked;
            set => this.RaiseAndSetIfChanged(ref _sortByNameChecked, value);
        }

        private bool _isSidebarVisible;
        public bool IsSidebarVisible
        {
            get => _isSidebarVisible;
            set => this.RaiseAndSetIfChanged(ref _isSidebarVisible, value);
        }

        private int _red;
        public int Red
        {
            get => _red;
            set
            {
                var clamped = Math.Clamp(value, 0, 255);
                if (_red == clamped) return;
                _red = clamped;
                this.RaisePropertyChanged(nameof(Red));
                UpdateSelectedColor();
                labConversion();
            }
        }

        private int _green;
        public int Green
        {
            get => _green;
            set
            {
                var clamped = Math.Clamp(value, 0, 255);
                if (_green == clamped) return;
                _green = clamped;
                this.RaisePropertyChanged(nameof(Green));
                UpdateSelectedColor();
                labConversion();
            }
        }

        private int _blue;
        public int Blue
        {
            get => _blue;
            set
            {
                var clamped = Math.Clamp(value, 0, 255);
                if (_blue == clamped) return;
                _blue = clamped;
                this.RaisePropertyChanged(nameof(Blue));
                UpdateSelectedColor();
                labConversion();
            }
        }

        private double _lightness;
        public double Lightness
        {
            get => _lightness;
            set => this.RaiseAndSetIfChanged(ref _lightness, value);
        }

        private double _redGreen;
        public double RedGreen
        {
            get => _redGreen;
            set => this.RaiseAndSetIfChanged(ref _redGreen, value);
        }

        private double _blueYellow;
        public double BlueYellow
        {
            get => _blueYellow;
            set => this.RaiseAndSetIfChanged(ref _blueYellow, value);
        }

        // --- Image Search ---

        private string? _selectedImagePath;
        public string? SelectedImagePath
        {
            get => _selectedImagePath;
            set => this.RaiseAndSetIfChanged(ref _selectedImagePath, value);
        }

        private bool _isImageSearching;
        public bool IsImageSearching
        {
            get => _isImageSearching;
            set => this.RaiseAndSetIfChanged(ref _isImageSearching, value);
        }

        private string _imageSearchStatus = "Pick an image to find similar tiles";
        public string ImageSearchStatus
        {
            get => _imageSearchStatus;
            set => this.RaiseAndSetIfChanged(ref _imageSearchStatus, value);
        }

        public async void ImageSearchCommand()
        {
            if (string.IsNullOrEmpty(SelectedImagePath)) return;

            IsImageSearching = true;
            ImageSearchStatus = "Searching...";
            CardItems.Clear();
            IsSidebarVisible = false;

            try
            {
                var matches = await _searchService.SearchByImageAsync(SelectedImagePath);

                if (!matches.Any())
                {
                    ImageSearchStatus = "No matches found. Make sure tiles have been uploaded.";
                    IsFilterEmpty = true;
                    return;
                }

                IsFilterEmpty = false;
                ImageSearchStatus = $"Found {matches.Count} similar tile{(matches.Count != 1 ? "s" : "")}";

                foreach (var match in matches)
                {
                    var card = await _db.GetCardItemByIdAsync(match.TileId);
                    if (card != null)
                        CardItems.Add(card);
                }
            }
            catch (Exception ex)
            {
                ImageSearchStatus = $"Search failed: {ex.Message}";
                Console.WriteLine($"Image search error: {ex}");
            }
            finally
            {
                IsImageSearching = false;
            }
        }

        // --- Existing glaze search ---

        private CancellationTokenSource? _searchCts;

        public MainWindowViewModel()
        {
            LoadData();
            Red = 185;
            Green = 145;
            Blue = 117;
            labConversion();
        }

        public async void SearchCommand()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            try
            {
                await FilterCardItemsAsync(_searchCts.Token);
            }
            catch (OperationCanceledException) { }
        }

        private bool _isFilterEmpty;
        public bool IsFilterEmpty
        {
            get => _isFilterEmpty;
            set => this.RaiseAndSetIfChanged(ref _isFilterEmpty, value);
        }

        private bool _filterByString;
        public bool FilterByString
        {
            get => _filterByString;
            set => this.RaiseAndSetIfChanged(ref _filterByString, value);
        }

        private async Task FilterCardItemsAsync(CancellationToken cancellationToken)
        {
            var allItems = await _db.GetFilteredCardItemMetadataAsync(SelectedGlazeType, SelectedSurfaceCondition, SelectedFiringType);
            var selectedLab = new Lab { L = Lightness, A = RedGreen, B = BlueYellow };
            double threshold = 25.0;

            foreach (var item in allItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lab = new Lab { L = item.ColorL, A = item.ColorA, B = item.ColorB };
                item.ColorName = GetColorName(lab);
            }

            var filtered = allItems.Where(item =>
            {
                if (FilterByString)
                    return item.ColorName == SelectedColorPalette;
                else if (!FilterByColor)
                    return true;
                else
                {
                    var lab = new Lab { L = item.ColorL, A = item.ColorA, B = item.ColorB };
                    double deltaE = selectedLab.Compare(lab, new Cie1976Comparison());
                    return deltaE <= threshold;
                }
            }).ToList();

            CardItems.Clear();
            IsFilterEmpty = !filtered.Any();

            foreach (var item in filtered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                item.Image = await _db.GetImageByIdAsync(item.Id);
                CardItems.Add(item);
            }
        }

        public ObservableCollection<string> ColorPalettes { get; } = new();

        private string? _selectedColorPalette;
        public string? SelectedColorPalette
        {
            get => _selectedColorPalette;
            set => this.RaiseAndSetIfChanged(ref _selectedColorPalette, value);
        }

        private async void LoadData()
        {
            var db = new DatabaseService();

            GlazeTypes.Clear();
            GlazeTypes.Add("All");
            foreach (var type in await db.GetGlazeTypesAsync())
                GlazeTypes.Add(type);

            SurfaceConditions.Clear();
            SurfaceConditions.Add("All");
            foreach (var sc in await db.GetSurfaceCondition())
                SurfaceConditions.Add(sc);

            FiringTypes.Clear();
            FiringTypes.Add("All");
            foreach (var ft in await db.GetFiringType())
                FiringTypes.Add(ft);

            ColorPalettes.Clear();
            foreach (var c in new[] { "Black", "White", "Cream", "Red", "Green", "Yellow", "Blue", "Cyan", "Magenta", "Pink", "Brown" })
                ColorPalettes.Add(c);

            SelectedGlazeType = "All";
            SelectedSurfaceCondition = "All";
            SelectedFiringType = "All";
            SelectedColorPalette = "Red";
        }

        private void labConversion()
        {
            var rgb = new Rgb { R = Red, G = Green, B = Blue };
            var lab = rgb.To<Lab>();
            Lightness = lab.L;
            RedGreen = lab.A;
            BlueYellow = lab.B;
        }

        private bool _filterByColor = true;
        public bool FilterByColor
        {
            get => _filterByColor;
            set => this.RaiseAndSetIfChanged(ref _filterByColor, value);
        }

        private Color _selectedColor;
        public Color SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (value == _selectedColor) return;
                _selectedColor = value;
                this.RaisePropertyChanged(nameof(SelectedColor));
                _red = value.R;
                _green = value.G;
                _blue = value.B;
                this.RaisePropertyChanged(nameof(Red));
                this.RaisePropertyChanged(nameof(Green));
                this.RaisePropertyChanged(nameof(Blue));
                labConversion();
            }
        }

        private void UpdateSelectedColor()
        {
            var newColor = Color.FromRgb((byte)Red, (byte)Green, (byte)Blue);
            if (_selectedColor == newColor) return;
            _selectedColor = newColor;
            this.RaisePropertyChanged(nameof(SelectedColor));
        }

        private static readonly Dictionary<string, Lab> BasicColors = new()
        {
            { "Black",   new Lab { L = 0,   A = 0,   B = 0    } },
            { "White",   new Lab { L = 100, A = 0,   B = 0    } },
            { "Cream",   new Lab { L = 95,  A = -2,  B = 18   } },
            { "Red",     new Lab { L = 53,  A = 80,  B = 67   } },
            { "Green",   new Lab { L = 87,  A = -86, B = 83   } },
            { "Blue",    new Lab { L = 32,  A = 79,  B = -108 } },
            { "Yellow",  new Lab { L = 97,  A = -21, B = 94   } },
            { "Cyan",    new Lab { L = 91,  A = -48, B = -14  } },
            { "Magenta", new Lab { L = 60,  A = 98,  B = -60  } },
            { "Brown",   new Lab { L = 37,  A = 23,  B = 17   } },
            { "Pink",    new Lab { L = 81,  A = 15,  B = 6    } }
        };

        public static string GetColorName(Lab inputLab)
        {
            string colorName = "Unknown";
            double minDeltaE = double.MaxValue;
            foreach (var (name, lab) in BasicColors)
            {
                double deltaE = inputLab.Compare(lab, new CieDe2000Comparison());
                if (deltaE < minDeltaE)
                {
                    minDeltaE = deltaE;
                    colorName = name;
                }
            }
            return minDeltaE <= 30 ? colorName : "Other";
        }
    }
}