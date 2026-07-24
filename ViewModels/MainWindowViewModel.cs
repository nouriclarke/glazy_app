using ASTEM_DB.Services;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ColorMine.ColorSpaces;
using ColorMine.ColorSpaces.Comparisons;
using Avalonia.Media;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Input;
// using System.Diagnostics;
// using Microsoft.VisualBasic.FileIO;

namespace ASTEM_DB.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private const int MaxAiResults = 10;
        private const int CombinedImageCandidateCount = 50;
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

        // --- Existing glaze search ---

        private CancellationTokenSource? _searchCts;
        private CancellationTokenSource? _aiSearchCts;

        public ICommand SearchCommand { get; }
        public ICommand AiSearchCommand { get; }
        public ICommand ResetAiConversationCommand { get; }
        public ICommand MarkAiGoodCommand { get; }
        public ICommand MarkAiBadCommand { get; }
        public ICommand MarkAiWrongColorCommand { get; }
        public ICommand MarkAiHasDarkEdgesCommand { get; }
        public ICommand TrainAiRankingCommand { get; }

        public MainWindowViewModel()
        {
            SearchCommand = new AsyncCommand(ExecuteSearchCommandAsync);
            AiSearchCommand = new AsyncCommand(ExecuteAiSearchCommandAsync);
            ResetAiConversationCommand = new RelayCommand(ResetAiConversation);
            MarkAiGoodCommand = new AsyncCommand(() => SaveSelectedAiFeedbackAsync("good", "good_match"));
            MarkAiBadCommand = new AsyncCommand(() => SaveSelectedAiFeedbackAsync("bad", "bad_match"));
            MarkAiWrongColorCommand = new AsyncCommand(() => SaveSelectedAiFeedbackAsync("bad", "wrong_color"));
            MarkAiHasDarkEdgesCommand = new AsyncCommand(() => SaveSelectedAiFeedbackAsync("bad", "has_dark_edges"));
            TrainAiRankingCommand = new AsyncCommand(TrainAiRankingAsync);
            Localization.CultureChanged += (_, _) => RefreshLocalization();

            LoadData();
            Red = 185;
            Green = 145;
            Blue = 117;
            labConversion();
        }

        private async Task ExecuteSearchCommandAsync()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            try
            {
                await FilterCardItemsAsync(_searchCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Search was canceled, do nothing
            }
            catch (Exception ex)
            {
                CardItems.Clear();
                IsFilterEmpty = true;
                AiSearchStatus = Localization.Format(
                    "StatusDatabaseUnavailable",
                    CleanProcessMessage(ex.Message)
                );
            }
        }

        private string _aiSearchPrompt = string.Empty;
        public string AiSearchPrompt
        {
            get => _aiSearchPrompt;
            set => this.RaiseAndSetIfChanged(ref _aiSearchPrompt, value);
        }

        private string _aiSearchStatus = Localization.Get("StatusAiReady");
        public string AiSearchStatus
        {
            get => _aiSearchStatus;
            set => this.RaiseAndSetIfChanged(ref _aiSearchStatus, value);
        }

        private string _aiResolvedSearchPrompt = string.Empty;
        public string AiResolvedSearchPrompt
        {
            get => _aiResolvedSearchPrompt;
            set => this.RaiseAndSetIfChanged(ref _aiResolvedSearchPrompt, value);
        }

        private string _aiSearchImagePath = string.Empty;
        public string AiSearchImagePath
        {
            get => _aiSearchImagePath;
            set => this.RaiseAndSetIfChanged(ref _aiSearchImagePath, value);
        }

        private string _aiSearchImageLabel = string.Empty;
        public string AiSearchImageLabel
        {
            get => _aiSearchImageLabel;
            set => this.RaiseAndSetIfChanged(ref _aiSearchImageLabel, value);
        }

        private bool _isAiSearchLoading;
        public bool IsAiSearchLoading
        {
            get => _isAiSearchLoading;
            set => this.RaiseAndSetIfChanged(ref _isAiSearchLoading, value);
        }

        private double _aiStrictnessLevelIndex;
        public double AiStrictnessLevelIndex
        {
            get => _aiStrictnessLevelIndex;
            set
            {
                var snapped = Math.Clamp(Math.Round(value), 0, 2);
                if (_aiStrictnessLevelIndex == snapped)
                    return;

                _aiStrictnessLevelIndex = snapped;
                this.RaisePropertyChanged(nameof(AiStrictnessLevelIndex));
                this.RaisePropertyChanged(nameof(AiStrictnessLabel));
                this.RaisePropertyChanged(nameof(AiMinimumMatchPercent));
                this.RaisePropertyChanged(nameof(AiMinimumMatchLabel));
            }
        }

        public double AiMinimumMatchScore => AiStrictnessLevelIndex switch
        {
            >= 2 => 0.75,
            >= 1 => 0.60,
            _ => 0.40
        };

        public int AiMinimumMatchPercent => (int)Math.Round(AiMinimumMatchScore * 100);
        public string AiMinimumMatchLabel => Localization.Format("MatchMinimum", AiMinimumMatchPercent);

        public string AiStrictnessLabel => AiStrictnessLevelIndex switch
        {
            >= 2 => Localization.Get("StrictnessStrict"),
            >= 1 => Localization.Get("StrictnessBalanced"),
            _ => Localization.Get("StrictnessLoose")
        };

        public ObservableCollection<AiChatMessageViewModel> AiChatMessages { get; } = new();

        private readonly List<string> _aiConversationColors = new();
        private readonly List<string> _aiConversationModifiers = new();
        private readonly List<string> _aiConversationFreeTerms = new();
        private readonly List<string> _aiConversationConstraints = new();

        private string _aiTrainingStatus = Localization.Get("TrainingSelectResult");
        public string AiTrainingStatus
        {
            get => _aiTrainingStatus;
            set => this.RaiseAndSetIfChanged(ref _aiTrainingStatus, value);
        }
        //here
        private async Task ExecuteAiSearchCommandAsync()
        {
            var prompt = AiSearchPrompt?.Trim();
            var imagePath = AiSearchImagePath?.Trim();

            bool hasText = !string.IsNullOrWhiteSpace(prompt);
            bool hasImage = !string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath);

            if (!hasText && !hasImage)
            {
                AiSearchStatus = string.IsNullOrWhiteSpace(AiSearchImagePath)
                    ? Localization.Get("StatusEnterPrompt")
                    : Localization.Get("StatusEnterRefinement");
                return;
            }

            _aiSearchCts?.Cancel();
            _aiSearchCts = new CancellationTokenSource();
            var cancellationToken = _aiSearchCts.Token;
            var normalizedPrompt = hasText ? NormalizeJapanesePrompt(prompt!) : null;

            // Stop the ordinary database grid from continuing to append thousands
            // of cards after the AI result set has replaced it.
            _searchCts?.Cancel();
            IsAiSearchLoading = true;
            CardItems.Clear();
            SelectedCard = null;
            IsSidebarVisible = false;

            try
            {
                if (hasImage && hasText)
                    await ExecuteCombinedSearchAsync(normalizedPrompt!, imagePath!, cancellationToken);
                else if (hasImage)
                    await ExecuteClipSearchAsync(imagePath!, cancellationToken);
                else
                    await ExecuteTextSearchAsync(normalizedPrompt!, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                AiSearchStatus = Localization.Get("StatusAiCanceled");
            }
            catch (Exception ex)
            {
                AiSearchStatus = Localization.Format("StatusAiFailed", CleanProcessMessage(ex.Message));
                AiChatMessages.Add(new AiChatMessageViewModel("Glazy", AiSearchStatus));
            }
            finally
            {
                IsAiSearchLoading = false;
                AiSearchImagePath = string.Empty;
                AiSearchImageLabel = string.Empty;
            }
        }

        private async Task ExecuteClipSearchAsync(string imagePath, CancellationToken cancellationToken)
        {
            AiSearchStatus = "Searching by image similarity...";
            AiChatMessages.Add(new AiChatMessageViewModel("You", "[Image search]"));

            var matches = await _searchService.SearchByImageAsync(
                imagePath,
                cancellationToken,
                CombinedImageCandidateCount
            );

            if (!matches.Any())
            {
                AiSearchStatus = "No similar tiles found. Make sure tiles have been uploaded.";
                IsFilterEmpty = true;
                return;
            }

            var filteredMatches = matches
                .Select(match => new
                {
                    match.TileId,
                    MatchScore = ImageDistanceToMatchScore(match.Score)
                })
                .Where(match => match.MatchScore >= AiMinimumMatchScore)
                .GroupBy(match => match.TileId)
                .Select(group => group.OrderByDescending(match => match.MatchScore).First())
                .OrderByDescending(match => match.MatchScore)
                .Take(MaxAiResults)
                .ToList();
            var scoreById = filteredMatches.ToDictionary(
                match => match.TileId,
                match => match.MatchScore
            );
            var items = await _db.GetCardItemsByIdsAsync(
                filteredMatches.Select(match => match.TileId)
            );
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var item in items)
            {
                if (scoreById.TryGetValue(item.Id, out var matchScore))
                    item.AiScore = matchScore;
                var lab = new Lab { L = item.ColorL, A = item.ColorA, B = item.ColorB };
                item.ColorName = GetColorName(lab);
                CardItems.Add(item);
            }

            IsFilterEmpty = !CardItems.Any();
            AiSearchStatus = $"Found {CardItems.Count} visually similar tiles.";
            AiChatMessages.Add(new AiChatMessageViewModel("Glazy", AiSearchStatus));
        }

        private async Task ExecuteCombinedSearchAsync(string prompt, string imagePath, CancellationToken cancellationToken)
        {
            AiSearchStatus = "Running combined visual + text search...";
            var resolvedPrompt = ResolveAiConversationPrompt(prompt);
            AiChatMessages.Add(new AiChatMessageViewModel("You", $"[Image] + {prompt}"));
            AiSearchPrompt = string.Empty;
            AiResolvedSearchPrompt = resolvedPrompt;

            var clipMatches = await _searchService.SearchByImageAsync(
                imagePath,
                cancellationToken,
                CombinedImageCandidateCount
            );
            var clipScoreById = clipMatches
                .GroupBy(match => match.TileId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Max(match => ImageDistanceToMatchScore(match.Score))
                );

            cancellationToken.ThrowIfCancellationRequested();

            AiSearchStatus = "Re-ranking by text and color...";
            var textResponse = await RunLocalAiSearchAsync(resolvedPrompt, cancellationToken);
            var textScoreById = textResponse.Results
                .GroupBy(result => result.Id)
                .ToDictionary(
                    group => group.Key,
                    group => group.Max(GetDisplayMatchScore)
                );

            cancellationToken.ThrowIfCancellationRequested();

            var combined = clipScoreById
                .Select(kvp => new
                {
                    Id = kvp.Key,
                    ClipScore = kvp.Value,
                    TextScore = textScoreById.TryGetValue(kvp.Key, out var ts) ? ts : 0.0
                })
                .Select(x => new
                {
                    x.Id,
                    CombinedScore = 0.65 * x.ClipScore + 0.35 * x.TextScore
                })
                .Where(x => x.CombinedScore >= AiMinimumMatchScore)
                .OrderByDescending(x => x.CombinedScore)
                .Take(MaxAiResults)
                .ToList();

            var items = await _db.GetCardItemsByIdsAsync(combined.Select(x => x.Id));
            var scoreMap = combined.ToDictionary(x => x.Id, x => x.CombinedScore);

            foreach (var item in items)
            {
                if (scoreMap.TryGetValue(item.Id, out var score))
                    item.AiScore = score;
                var lab = new Lab { L = item.ColorL, A = item.ColorA, B = item.ColorB };
                item.ColorName = GetColorName(lab);
                CardItems.Add(item);
            }

            var sorted = CardItems.OrderByDescending(c => c.AiScore).ToList();
            CardItems.Clear();
            foreach (var item in sorted)
                CardItems.Add(item);

            IsFilterEmpty = !CardItems.Any();
            AiSearchStatus = $"Found {CardItems.Count} tiles matching visual + text criteria.";
            AiChatMessages.Add(new AiChatMessageViewModel("Glazy", AiSearchStatus));
        }

        private async Task ExecuteTextSearchAsync(string prompt, CancellationToken cancellationToken)
        {
            var resolvedPrompt = ResolveAiConversationPrompt(prompt);
            AiChatMessages.Add(new AiChatMessageViewModel("You", prompt));
            AiSearchPrompt = string.Empty;
            AiResolvedSearchPrompt = resolvedPrompt;
            AiSearchStatus = $"Searching for: {resolvedPrompt}";

            var response = await RunLocalAiSearchAsync(resolvedPrompt, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var filteredResults = response.Results
                .Where(result => GetDisplayMatchScore(result) >= AiMinimumMatchScore)
                .GroupBy(result => result.Id)
                .Select(group => group.OrderByDescending(GetDisplayMatchScore).First())
                .OrderByDescending(GetDisplayMatchScore)
                .Take(MaxAiResults)
                .ToList();
            var idToScore = filteredResults.ToDictionary(
                result => result.Id,
                GetDisplayMatchScore
            );
            var items = await _db.GetCardItemsByIdsAsync(
                filteredResults.Select(result => result.Id)
            );
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var item in items)
            {
                if (idToScore.TryGetValue(item.Id, out var score))
                    item.AiScore = score;
                var lab = new Lab { L = item.ColorL, A = item.ColorA, B = item.ColorB };
                item.ColorName = GetColorName(lab);
                CardItems.Add(item);
            }

            IsFilterEmpty = !CardItems.Any();
            AiSearchStatus = CardItems.Count == 0
                ? "No matches found."
                : $"Found {CardItems.Count} matches for \"{resolvedPrompt}\".";
            AiChatMessages.Add(new AiChatMessageViewModel("Glazy", AiSearchStatus));
        }

        public Task SetPendingAiSearchImageAsync(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return Task.CompletedTask;

            var fileName = Path.GetFileName(imagePath);
            AiSearchImagePath = imagePath;
            AiSearchImageLabel = Localization.Format("ImageLabel", fileName);
            AiSearchStatus = Localization.Get("StatusEnterRefinement");
            AiChatMessages.Add(
                new AiChatMessageViewModel("You", Localization.Format("ImageLabel", fileName))
            );
            return Task.CompletedTask;
        }

        public void SetAiSearchImageSelectionError(string message)
        {
            AiSearchStatus = Localization.Format(
                "StatusImageSelectionFailed",
                CleanProcessMessage(message)
            );
        }

        private void ResetAiConversation()
        {
            _aiSearchCts?.Cancel();
            _aiConversationColors.Clear();
            _aiConversationModifiers.Clear();
            _aiConversationFreeTerms.Clear();
            _aiConversationConstraints.Clear();
            AiChatMessages.Clear();
            AiResolvedSearchPrompt = string.Empty;
            AiSearchImagePath = string.Empty;
            AiSearchImageLabel = string.Empty;
            AiSearchPrompt = string.Empty;
            AiSearchStatus = Localization.Get("StatusAiReset");
        }

        private async Task SaveSelectedAiFeedbackAsync(string label, string reason)
        {
            if (SelectedCard == null)
            {
                AiTrainingStatus = Localization.Get("TrainingSelectTile");
                return;
            }

            if (string.IsNullOrWhiteSpace(AiResolvedSearchPrompt))
            {
                AiTrainingStatus = Localization.Get("TrainingRunSearch");
                return;
            }

            var prototypeDir = FindAiPrototypeDirectory();
            var trainingDir = Path.Combine(prototypeDir, "training-data");
            Directory.CreateDirectory(trainingDir);

            var feedback = new AiFeedbackEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Prompt = AiResolvedSearchPrompt,
                TileId = SelectedCard.Id,
                Label = label,
                Reason = reason,
                FinalScore = SelectedCard.AiScore,
                ColorName = SelectedCard.ColorName,
                AutoTags = SelectedCard.AutoTags,
                Features = new AiTrainingFeatures
                {
                    Bias = 1,
                    ClipScore = SelectedCard.AiClipScore,
                    ColorScore = SelectedCard.AiColorScore,
                    MetadataScore = SelectedCard.AiMetadataScore,
                    VisualScore = SelectedCard.AiVisualScore,
                    VisualPenalty = SelectedCard.AiVisualPenalty,
                    ExclusionPenalty = SelectedCard.AiExclusionPenalty
                }
            };

            var feedbackPath = Path.Combine(trainingDir, "feedback.jsonl");
            var line = JsonSerializer.Serialize(feedback) + Environment.NewLine;
            await File.AppendAllTextAsync(feedbackPath, line);

            var labelText = reason switch
            {
                "good_match" => Localization.Get("FeedbackGoodSaved"),
                "wrong_color" => Localization.Get("FeedbackWrongColorSaved"),
                "has_dark_edges" => Localization.Get("FeedbackDarkEdgeSaved"),
                _ => Localization.Get("FeedbackBadSaved")
            };
            SelectedCard.AiFeedbackStatus = labelText;
            AiTrainingStatus = Localization.Format("TrainingReady", labelText);
        }

        private async Task TrainAiRankingAsync()
        {
            var prototypeDir = FindAiPrototypeDirectory();
            var nodePath = FindNodeExecutable();
            var existingPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var startInfo = new ProcessStartInfo
            {
                FileName = nodePath,
                WorkingDirectory = prototypeDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("train-ranker.mjs");
            startInfo.Environment["PATH"] = $"/usr/local/bin:/opt/homebrew/bin:{existingPath}";
            startInfo.Environment["ALLOW_REMOTE"] = Environment.GetEnvironmentVariable("ALLOW_REMOTE") ?? "1";
            startInfo.Environment["CLIP_MODEL"] = Environment.GetEnvironmentVariable("CLIP_MODEL") ?? "Xenova/clip-vit-base-patch16";
            startInfo.Environment["DB_HOST"] = HostDatabaseValue("DB_HOST", "127.0.0.1");
            startInfo.Environment["DB_PORT"] = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
            startInfo.Environment["DB_NAME"] = Environment.GetEnvironmentVariable("DB_NAME")
                ?? Environment.GetEnvironmentVariable("MYSQL_DATABASE")
                ?? "tilearchive";
            startInfo.Environment["DB_USER"] = Environment.GetEnvironmentVariable("DB_USER") ?? "ceramadmin";
            startInfo.Environment["DB_PASSWORD"] = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "glazed-dev-password";

            AiTrainingStatus = Localization.Get("TrainingInProgress");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the AI ranking trainer.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                AiTrainingStatus = Localization.Format(
                    "TrainingFailed",
                    CleanProcessMessage(message)
                );
                return;
            }

            var response = JsonSerializer.Deserialize<AiTrainingResponse>(
                ExtractJsonPayload(stdout),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            _aiSearchWorker?.Dispose();
            _aiSearchWorker = null;

            var examples = response?.Metrics?.Examples ?? 0;
            var accuracy = response?.Metrics?.Accuracy ?? 0;
            AiTrainingStatus = Localization.Format("TrainingComplete", examples, accuracy);
        }

        private void RefreshLocalization()
        {
            this.RaisePropertyChanged(nameof(AiStrictnessLabel));
            this.RaisePropertyChanged(nameof(AiMinimumMatchLabel));

            if (!IsAiSearchLoading)
                AiSearchStatus = Localization.Get("StatusAiReady");

            AiTrainingStatus = Localization.Get("TrainingSelectResult");

            if (!string.IsNullOrWhiteSpace(AiSearchImagePath))
            {
                AiSearchImageLabel = Localization.Format(
                    "ImageLabel",
                    Path.GetFileName(AiSearchImagePath)
                );
            }

            foreach (var message in AiChatMessages)
                message.RefreshLocalization();

            foreach (var card in CardItems)
                card.RefreshLocalization();
        }

        private static string NormalizeJapanesePrompt(string prompt)
        {
            if (!JapaneseCharacterRegex.IsMatch(prompt))
                return prompt;

            var terms = new List<string>();
            var searchablePrompt = prompt;

            if (JapaneseNoDarkEdgeRegex.IsMatch(searchablePrompt))
            {
                AddUnique(terms, "no dark edges");
                searchablePrompt = JapaneseNoDarkEdgeRegex.Replace(searchablePrompt, " ");
            }
            else if (JapaneseNoEdgeRegex.IsMatch(searchablePrompt))
            {
                AddUnique(terms, "no edges");
                searchablePrompt = JapaneseNoEdgeRegex.Replace(searchablePrompt, " ");
            }

            var colorPrompt = searchablePrompt;
            var cyanPhrases = new[] { "水色", "青緑", "ターコイズ" };
            if (ContainsAny(colorPrompt, cyanPhrases))
            {
                AddUnique(terms, "cyan");
                foreach (var phrase in cyanPhrases)
                    colorPrompt = colorPrompt.Replace(phrase, " ");
            }

            AddMappedJapaneseTerm(terms, colorPrompt, "black", "黒", "ブラック");
            AddMappedJapaneseTerm(terms, colorPrompt, "white", "白", "ホワイト");
            AddMappedJapaneseTerm(terms, colorPrompt, "cream", "クリーム", "ベージュ");
            AddMappedJapaneseTerm(terms, colorPrompt, "brown", "茶色", "褐色", "ブラウン");
            AddMappedJapaneseTerm(terms, colorPrompt, "red", "赤", "レッド");
            AddMappedJapaneseTerm(terms, colorPrompt, "orange", "橙", "オレンジ");
            AddMappedJapaneseTerm(terms, colorPrompt, "yellow", "黄色", "イエロー", "金色");
            AddMappedJapaneseTerm(terms, colorPrompt, "green", "緑", "グリーン");
            AddMappedJapaneseTerm(terms, colorPrompt, "blue", "青", "藍色", "紺色", "ブルー");
            AddMappedJapaneseTerm(terms, colorPrompt, "purple", "紫", "パープル");
            AddMappedJapaneseTerm(terms, colorPrompt, "pink", "ピンク", "桃色");
            AddMappedJapaneseTerm(terms, colorPrompt, "gray", "灰色", "グレー");

            if (ContainsAny(searchablePrompt, "もっと暗", "より暗", "さらに暗"))
                AddUnique(terms, "darker");
            else
                AddMappedJapaneseTerm(terms, searchablePrompt, "dark", "暗い", "濃い", "深い");

            if (ContainsAny(searchablePrompt, "もっと明る", "より明る", "さらに明る", "もっと薄"))
                AddUnique(terms, "lighter");
            else
                AddMappedJapaneseTerm(terms, searchablePrompt, "light", "明るい", "淡い", "薄い");

            AddMappedJapaneseTerm(terms, searchablePrompt, "warm", "暖か", "温か", "温もり");
            AddMappedJapaneseTerm(terms, searchablePrompt, "cool", "冷たい", "涼しい", "寒色");
            AddMappedJapaneseTerm(terms, searchablePrompt, "glossy", "光沢", "艶", "つや");
            AddMappedJapaneseTerm(terms, searchablePrompt, "matte", "マット", "つや消し", "艶消し");
            AddMappedJapaneseTerm(terms, searchablePrompt, "textured", "粗い", "ざらざら", "凹凸");
            AddMappedJapaneseTerm(terms, searchablePrompt, "smooth", "滑らか", "なめらか", "平滑");
            AddMappedJapaneseTerm(terms, searchablePrompt, "speckled", "斑点", "まだら", "点々");
            AddMappedJapaneseTerm(terms, searchablePrompt, "earthy", "素朴", "土っぽ", "自然な");

            return terms.Count > 0 ? string.Join(" ", terms) : prompt;
        }

        private static void AddMappedJapaneseTerm(
            List<string> terms,
            string prompt,
            string translatedTerm,
            params string[] phrases)
        {
            if (ContainsAny(prompt, phrases))
                AddUnique(terms, translatedTerm);
        }

        private static bool ContainsAny(string value, params string[] phrases)
            => phrases.Any(value.Contains);

        private string ResolveAiConversationPrompt(string latestPrompt)
        {
            CaptureNegativeConstraints(latestPrompt);
            var positivePrompt = RemoveNegativeConstraintPhrases(latestPrompt);
            var tokens = TokenizePrompt(positivePrompt);
            var explicitColors = tokens
                .Select(token => AiColorAliases.TryGetValue(token, out var color) ? color : null)
                .Where(color => color != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();

            if (explicitColors.Count > 0)
            {
                _aiConversationColors.Clear();
                foreach (var color in explicitColors)
                    AddUnique(_aiConversationColors, color);
            }

            ApplyPromptModifiers(tokens);
            ApplyFreeTerms(tokens);

            var terms = new List<string>();
            terms.AddRange(_aiConversationModifiers);
            terms.AddRange(_aiConversationColors);
            terms.AddRange(_aiConversationFreeTerms);
            terms.AddRange(_aiConversationConstraints);
            terms.Add("ceramic");
            terms.Add("tile");

            return string.Join(" ", terms.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private void CaptureNegativeConstraints(string prompt)
        {
            foreach (Match match in EdgeColorConstraintRegex.Matches(prompt))
            {
                var color = match.Groups["color"].Value.ToLowerInvariant();
                if (color is "black" or "brown")
                    AddUnique(_aiConversationConstraints, $"no {color} edges");
                else
                    AddUnique(_aiConversationConstraints, "no dark edges");
            }

            if (NoEdgeConstraintRegex.IsMatch(prompt))
                AddUnique(_aiConversationConstraints, "no edges");
        }

        private static string RemoveNegativeConstraintPhrases(string prompt)
        {
            var cleaned = EdgeColorConstraintRegex.Replace(prompt, " ");
            cleaned = NoEdgeConstraintRegex.Replace(cleaned, " ");
            return cleaned;
        }

        private void ApplyPromptModifiers(IReadOnlySet<string> tokens)
        {
            if (HasAny(tokens, "dark", "deep"))
                SetExclusiveModifier("dark", "light", "lighter", "bright", "brighter", "pale");

            if (HasAny(tokens, "darker", "deeper"))
                SetExclusiveModifier("darker", "light", "lighter", "bright", "brighter", "pale");

            if (HasAny(tokens, "light", "bright", "pale"))
                SetExclusiveModifier("light", "dark", "darker", "deep", "deeper");

            if (HasAny(tokens, "lighter", "brighter"))
                SetExclusiveModifier("lighter", "dark", "darker", "deep", "deeper");

            if (HasAny(tokens, "warm", "warmer"))
                SetExclusiveModifier("warm", "cool", "cold");

            if (HasAny(tokens, "cool", "colder", "cold"))
                SetExclusiveModifier("cool", "warm");

            if (HasAny(tokens, "glossy", "glossier", "shiny", "shine", "reflective"))
                SetExclusiveModifier("glossy", "matte", "dull");

            if (HasAny(tokens, "matte", "dull"))
                SetExclusiveModifier("matte", "glossy", "shiny", "reflective");

            if (HasAny(tokens, "rough", "texture", "textured", "speckled", "spotted", "spotty", "variegated"))
                SetExclusiveModifier("textured", "smooth");

            if (HasAny(tokens, "smooth"))
                SetExclusiveModifier("smooth", "rough", "textured", "speckled", "variegated");

            if (HasAny(tokens, "earth", "earthy", "rustic"))
                AddUnique(_aiConversationModifiers, "earthy");
        }

        private void ApplyFreeTerms(IReadOnlySet<string> tokens)
        {
            foreach (var token in tokens)
            {
                if (AiStopWords.Contains(token) ||
                    AiColorAliases.ContainsKey(token) ||
                    AiModifierTokens.Contains(token))
                    continue;

                AddUnique(_aiConversationFreeTerms, token);
            }

            while (_aiConversationFreeTerms.Count > 6)
                _aiConversationFreeTerms.RemoveAt(0);
        }

        private void SetExclusiveModifier(string modifier, params string[] remove)
        {
            _aiConversationModifiers.RemoveAll(term =>
                remove.Any(value => string.Equals(value, term, StringComparison.OrdinalIgnoreCase)));
            AddUnique(_aiConversationModifiers, modifier);
        }

        private static bool HasAny(IReadOnlySet<string> tokens, params string[] values)
            => values.Any(tokens.Contains);

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
                values.Add(value);
        }

        private static IReadOnlySet<string> TokenizePrompt(string prompt)
            => prompt
                .ToLowerInvariant()
                .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '\'', '"', '-', '_', '/', '\\', '(', ')', '[', ']' },
                    StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> AiColorAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "black", "black" },
            { "white", "white" },
            { "cream", "cream" },
            { "beige", "cream" },
            { "tan", "brown" },
            { "brown", "brown" },
            { "red", "red" },
            { "burgundy", "red" },
            { "maroon", "red" },
            { "orange", "orange" },
            { "yellow", "yellow" },
            { "gold", "yellow" },
            { "green", "green" },
            { "blue", "blue" },
            { "navy", "blue" },
            { "cyan", "cyan" },
            { "teal", "cyan" },
            { "turquoise", "cyan" },
            { "purple", "purple" },
            { "violet", "purple" },
            { "pink", "pink" },
            { "gray", "gray" },
            { "grey", "gray" }
        };

        private static readonly HashSet<string> AiStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "are", "can", "could", "for", "give", "i", "it", "like", "make",
            "me", "more", "now", "of", "one", "ones", "please", "search", "show", "something",
            "that", "the", "thing", "things", "tile", "tiles", "to", "want", "with",
            "edge", "edges", "border", "borders", "rim", "rims", "frame", "outline"
        };

        private static readonly HashSet<string> AiModifierTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "dark", "darker", "deep", "deeper", "light", "lighter", "bright", "brighter", "pale",
            "warm", "warmer", "cool", "colder", "cold", "glossy", "glossier", "shiny", "shine",
            "reflective", "matte", "dull", "rough", "texture", "textured", "speckled", "spotted",
            "spotty", "variegated", "smooth", "earth", "earthy", "rustic"
        };

        private static readonly Regex EdgeColorConstraintRegex = new(
            @"\b(?:no|without|avoid|not)\s+(?<color>dark|black|brown)\s+(?:edge|edges|border|borders|rim|rims|frame|outline)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex NoEdgeConstraintRegex = new(
            @"\b(?:no|without|avoid)\s+(?:edge|edges|border|borders|rim|rims|frame|outline)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex JapaneseCharacterRegex = new(
            @"[\u3040-\u30ff\u3400-\u9fff]",
            RegexOptions.Compiled
        );

        private static readonly Regex JapaneseNoDarkEdgeRegex = new(
            @"(?:(?:暗い|黒い|茶色い)\s*(?:縁|ふち|エッジ).{0,6}(?:なし|ない|避け|除外)|(?:縁|ふち|エッジ).{0,4}(?:暗く|黒く|茶色く).{0,4}(?:なし|ない|避け|除外))",
            RegexOptions.Compiled
        );

        private static readonly Regex JapaneseNoEdgeRegex = new(
            @"(?:縁|ふち|エッジ).{0,6}(?:なし|ない|避け|除外)",
            RegexOptions.Compiled
        );

        private static AiSearchWorkerClient? _aiSearchWorker;

        private static async Task<AiSearchResponse> RunLocalAiSearchAsync(string prompt, CancellationToken cancellationToken)
        {
            var prototypeDir = FindAiPrototypeDirectory();
            var nodePath = FindNodeExecutable();

            if (!string.Equals(Environment.GetEnvironmentVariable("AI_SEARCH_WORKER"), "0", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _aiSearchWorker ??= new AiSearchWorkerClient(CreateAiSearchStartInfo(prototypeDir, nodePath, useWorker: true));
                    return await _aiSearchWorker.SearchAsync(prompt, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _aiSearchWorker?.Dispose();
                    _aiSearchWorker = null;
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AI worker failed, falling back to one-shot search: {ex}");
                    _aiSearchWorker?.Dispose();
                    _aiSearchWorker = null;
                }
            }

            return await RunLocalAiSearchOnceAsync(prompt, prototypeDir, nodePath, cancellationToken);
        }

        private static async Task<AiSearchResponse> RunLocalAiSearchOnceAsync(
            string prompt,
            string prototypeDir,
            string nodePath,
            CancellationToken cancellationToken)
        {
            var startInfo = CreateAiSearchStartInfo(prototypeDir, nodePath, useWorker: false);
            startInfo.ArgumentList.Add(prompt);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the local AI search process.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException(CleanProcessMessage(message));
            }

            return DeserializeAiSearchResponse(ExtractJsonPayload(stdout));
        }

        private static ProcessStartInfo CreateAiSearchStartInfo(string prototypeDir, string nodePath, bool useWorker)
        {
            var existingPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var startInfo = new ProcessStartInfo
            {
                FileName = nodePath,
                WorkingDirectory = prototypeDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("search.mjs");
            if (useWorker)
                startInfo.ArgumentList.Add("--stdio");

            startInfo.Environment["PATH"] = $"/usr/local/bin:/opt/homebrew/bin:{existingPath}";
            startInfo.Environment["ALLOW_REMOTE"] = Environment.GetEnvironmentVariable("ALLOW_REMOTE") ?? "1";
            startInfo.Environment["CLIP_MODEL"] = Environment.GetEnvironmentVariable("CLIP_MODEL") ?? "Xenova/clip-vit-base-patch16";
            startInfo.Environment["DB_HOST"] = HostDatabaseValue("DB_HOST", "127.0.0.1");
            startInfo.Environment["DB_PORT"] = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
            startInfo.Environment["DB_NAME"] = Environment.GetEnvironmentVariable("DB_NAME")
                ?? Environment.GetEnvironmentVariable("MYSQL_DATABASE")
                ?? "tilearchive";
            startInfo.Environment["DB_USER"] = Environment.GetEnvironmentVariable("DB_USER") ?? "ceramadmin";
            startInfo.Environment["DB_PASSWORD"] = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "glazed-dev-password";
            startInfo.Environment["TOP_K"] = Environment.GetEnvironmentVariable("TOP_K") ?? "10";
            startInfo.Environment["EMBEDDING_PREFILTER"] = Environment.GetEnvironmentVariable("EMBEDDING_PREFILTER") ?? "60";

            return startInfo;
        }

        private static string FindAiPrototypeDirectory()
        {
            var directCandidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "ai-search-prototype", "search.mjs"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "ai-search-prototype", "search.mjs")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "ai-search-prototype", "search.mjs")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ai-search-prototype", "search.mjs")),
                Path.Combine(Directory.GetCurrentDirectory(), "ai-search-prototype", "search.mjs"),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "ai-search-prototype", "search.mjs")),
            };

            foreach (var candidate in directCandidates)
            {
                if (File.Exists(candidate))
                    return Path.GetDirectoryName(candidate)!;
            }

            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "ai-search-prototype", "search.mjs");
                if (File.Exists(candidate))
                    return Path.GetDirectoryName(candidate)!;

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not find ai-search-prototype/search.mjs near the app.");
        }

        private static string HostDatabaseValue(string name, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(name) ?? fallback;
            return value == "tile-db" ? "127.0.0.1" : value;
        }

        private static string ExtractJsonPayload(string stdout)
        {
            var trimmed = stdout.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                return trimmed;

            var lines = trimmed
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Reverse();

            foreach (var line in lines)
            {
                var candidate = line.Trim();
                if (candidate.StartsWith("{") && candidate.EndsWith("}"))
                    return candidate;
            }

            throw new InvalidOperationException(
                "Local AI search did not return JSON. Output: " + CleanProcessMessage(stdout)
            );
        }

        private static AiSearchResponse DeserializeAiSearchResponse(string json)
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                var details = document.RootElement.TryGetProperty("details", out var detailValue)
                    ? detailValue.GetString()
                    : null;
                var message = string.Join(
                    " ",
                    new[] { error.GetString(), details }
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                );
                throw new InvalidOperationException(CleanProcessMessage(message));
            }

            var response = JsonSerializer.Deserialize<AiSearchResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return response ?? throw new InvalidOperationException("Local AI search returned an empty response.");
        }

        private static string CleanProcessMessage(string message)
        {
            var lines = message
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(8);

            var cleaned = string.Join(" ", lines).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "Unknown local AI search error." : cleaned;
        }

        private sealed class AiSearchWorkerClient : IDisposable
        {
            private readonly ProcessStartInfo _startInfo;
            private readonly SemaphoreSlim _gate = new(1, 1);
            private readonly List<string> _stderrLines = new();
            private Process? _process;

            public AiSearchWorkerClient(ProcessStartInfo startInfo)
            {
                _startInfo = startInfo;
            }

            public async Task<AiSearchResponse> SearchAsync(string prompt, CancellationToken cancellationToken)
            {
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    var process = EnsureStarted();
                    var request = JsonSerializer.Serialize(new AiSearchWorkerRequest { Query = prompt });

                    try
                    {
                        await process.StandardInput.WriteLineAsync(request).WaitAsync(cancellationToken);
                        await process.StandardInput.FlushAsync().WaitAsync(cancellationToken);

                        while (true)
                        {
                            var line = await process.StandardOutput.ReadLineAsync().WaitAsync(cancellationToken);
                            if (line == null)
                                throw new InvalidOperationException("Local AI search worker exited. " + RecentStderr());

                            var candidate = line.Trim();
                            if (!candidate.StartsWith("{") || !candidate.EndsWith("}"))
                                continue;

                            return DeserializeAiSearchResponse(candidate);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        StopProcess();
                        throw;
                    }
                    catch (Exception)
                    {
                        StopProcess();
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            private Process EnsureStarted()
            {
                if (_process != null && !_process.HasExited)
                    return _process;

                _stderrLines.Clear();
                _process = Process.Start(_startInfo)
                    ?? throw new InvalidOperationException("Could not start the local AI search worker.");
                _ = Task.Run(() => DrainStderrAsync(_process));
                return _process;
            }

            private async Task DrainStderrAsync(Process process)
            {
                try
                {
                    while (!process.StandardError.EndOfStream)
                    {
                        var line = await process.StandardError.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        lock (_stderrLines)
                        {
                            _stderrLines.Add(line);
                            while (_stderrLines.Count > 8)
                                _stderrLines.RemoveAt(0);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }

            private string RecentStderr()
            {
                lock (_stderrLines)
                    return CleanProcessMessage(string.Join(Environment.NewLine, _stderrLines));
            }

            private void StopProcess()
            {
                try
                {
                    if (_process != null && !_process.HasExited)
                        _process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                _process?.Dispose();
                _process = null;
            }

            public void Dispose()
            {
                StopProcess();
                _gate.Dispose();
            }
        }

        private static string FindNodeExecutable()
        {
            var candidates = new[]
            {
                "/usr/local/bin/node",
                "/opt/homebrew/bin/node",
                "node"
            };

            return candidates.First(candidate => candidate == "node" || File.Exists(candidate));
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
            set
            {
                this.RaiseAndSetIfChanged(ref _filterByString, value);
                if (value)
                {
                    FilterByColor = false;
                    DontFilterByColor = false;
                }
            }
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

                var image = await _db.GetImageByIdAsync(item.Id);
                cancellationToken.ThrowIfCancellationRequested();
                if (image != null)
                    item.Image = image;
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

            try
            {
                var glazeTypes = await db.GetGlazeTypesAsync();
                foreach (var type in glazeTypes)
                    GlazeTypes.Add(type);
            }
            catch (Exception ex)
            {
                AiSearchStatus = $"Database unavailable: {CleanProcessMessage(ex.Message)}";
            }

            SurfaceConditions.Clear();
            SurfaceConditions.Add("All");

            try
            {
                var surfaceConditions = await db.GetSurfaceCondition();
                foreach (var sc in surfaceConditions)
                    SurfaceConditions.Add(sc);
            }
            catch
            {
            }

            FiringTypes.Clear();
            FiringTypes.Add("All");

            try
            {
                var firingTypes = await db.GetFiringType();
                foreach (var ft in firingTypes)
                    FiringTypes.Add(ft);
            }
            catch
            {
            }

            ColorPalettes.Clear();
            foreach (var c in new[] { "Black", "White", "Cream", "Red", "Green", "Yellow", "Blue", "Cyan", "Magenta", "Pink", "Brown" })
                ColorPalettes.Add(c);

            SelectedGlazeType = "All";
            SelectedSurfaceCondition = "All";
            SelectedFiringType = "All";
            SelectedColorPalette = "Red";
            FilterByColor = false;
            FilterByString = false;
            DontFilterByColor = true;

            await ExecuteSearchCommandAsync();
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
            set
            {
                this.RaiseAndSetIfChanged(ref _filterByColor, value);
                if (value)
                {
                    FilterByString = false;
                    DontFilterByColor = false;
                }
            }
        }

        private bool _dontFilterByColor;
        public bool DontFilterByColor
        {
            get => _dontFilterByColor;
            set
            {
                this.RaiseAndSetIfChanged(ref _dontFilterByColor, value);
                if (value)
                {
                    FilterByColor = false;
                    FilterByString = false;
                }
            }
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

        private static double GetDisplayMatchScore(AiSearchResult result)
        {
            return result.MatchScore > 0 ? result.MatchScore : result.FinalScore;
        }

        private static double ImageDistanceToMatchScore(double distance)
        {
            return Math.Clamp(1 / (1 + Math.Max(0, distance)), 0, 1);
        }

        private sealed class AiSearchResponse
        {
            [JsonPropertyName("searchedRows")]
            public int SearchedRows { get; set; }

            [JsonPropertyName("results")]
            public List<AiSearchResult> Results { get; set; } = new();
        }

        private sealed class AiFeedbackEntry
        {
            [JsonPropertyName("timestamp")]
            public DateTimeOffset Timestamp { get; set; }

            [JsonPropertyName("prompt")]
            public string Prompt { get; set; } = string.Empty;

            [JsonPropertyName("tileId")]
            public string TileId { get; set; } = string.Empty;

            [JsonPropertyName("label")]
            public string Label { get; set; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; set; } = string.Empty;

            [JsonPropertyName("finalScore")]
            public double FinalScore { get; set; }

            [JsonPropertyName("colorName")]
            public string ColorName { get; set; } = string.Empty;

            [JsonPropertyName("autoTags")]
            public string AutoTags { get; set; } = string.Empty;

            [JsonPropertyName("features")]
            public AiTrainingFeatures Features { get; set; } = new();
        }

        private sealed class AiTrainingFeatures
        {
            [JsonPropertyName("bias")]
            public double Bias { get; set; }

            [JsonPropertyName("clipScore")]
            public double ClipScore { get; set; }

            [JsonPropertyName("colorScore")]
            public double ColorScore { get; set; }

            [JsonPropertyName("metadataScore")]
            public double MetadataScore { get; set; }

            [JsonPropertyName("visualScore")]
            public double VisualScore { get; set; }

            [JsonPropertyName("visualPenalty")]
            public double VisualPenalty { get; set; }

            [JsonPropertyName("exclusionPenalty")]
            public double ExclusionPenalty { get; set; }
        }

        private sealed class AiTrainingResponse
        {
            [JsonPropertyName("metrics")]
            public AiTrainingMetrics? Metrics { get; set; }
        }

        private sealed class AiTrainingMetrics
        {
            [JsonPropertyName("examples")]
            public int Examples { get; set; }

            [JsonPropertyName("accuracy")]
            public double Accuracy { get; set; }
        }

        private sealed class AiSearchWorkerRequest
        {
            [JsonPropertyName("query")]
            public string Query { get; set; } = string.Empty;
        }

        private sealed class AiSearchResult
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("finalScore")]
            public double FinalScore { get; set; }

            [JsonPropertyName("matchScore")]
            public double MatchScore { get; set; }

            [JsonPropertyName("clipScore")]
            public double ClipScore { get; set; }

            [JsonPropertyName("colorScore")]
            public double ColorScore { get; set; }

            [JsonPropertyName("metadataScore")]
            public double MetadataScore { get; set; }

            [JsonPropertyName("visualScore")]
            public double VisualScore { get; set; }

            [JsonPropertyName("visualPenalty")]
            public double VisualPenalty { get; set; }

            [JsonPropertyName("features")]
            public AiTrainingFeatures? Features { get; set; }
        }
    }
}
