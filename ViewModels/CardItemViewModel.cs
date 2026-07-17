using ReactiveUI;

namespace ASTEM_DB.ViewModels
{
    public class CardItemViewModel : ViewModelBase
    {
        private string _id = string.Empty;
        private Avalonia.Media.Imaging.Bitmap _image = null!;
        private string _glazeType = string.Empty;
        private string _glazeTypeString = string.Empty;
        private double _colorL;
        private double _colorA;
        private double _colorB;
        private string _lab = string.Empty;
        private string _firingType = string.Empty;
        private string _firingTypeString = string.Empty;
        private string _soilType = string.Empty;
        private string _soilTypeString = string.Empty;
        private string _chemicalComposition = string.Empty;
        private string _chemicalCompositionString = string.Empty;
        private string _surfaceCondition = string.Empty;
        private string _surfaceConditionString = string.Empty;
        private string _colorName = string.Empty;
        private string _imagePath = string.Empty;
        private string _autoTags = string.Empty;
        private string _autoKeywords = string.Empty;
        private double _aiScore;
        private string _aiScoreString = string.Empty;
        private double _aiClipScore;
        private double _aiColorScore;
        private double _aiMetadataScore;
        private double _aiVisualScore;
        private double _aiVisualPenalty;
        private double _aiExclusionPenalty;
        private string _aiFeedbackStatus = string.Empty;
        public string Id
        {
            get => _id;
            set => this.RaiseAndSetIfChanged(ref _id, value);
        }
        public string ImagePath
        {
            get => _imagePath;
            set => this.RaiseAndSetIfChanged(ref _imagePath, value);
        }

        public Avalonia.Media.Imaging.Bitmap Image
        {
            get => _image;
            set => this.RaiseAndSetIfChanged(ref _image, value);
        }

        public string GlazeType
        {
            get => _glazeType;
            set
            {
                this.RaiseAndSetIfChanged(ref _glazeType, value);
                GlazeTypeString = $"Glaze Type: {value}";
            }
        }

        public string GlazeTypeString
        {
            get => _glazeTypeString;
            private set => this.RaiseAndSetIfChanged(ref _glazeTypeString, value);
        }

        public double ColorL
        {
            get => _colorL;
            set => this.RaiseAndSetIfChanged(ref _colorL, value);
        }

        public double ColorA
        {
            get => _colorA;
            set => this.RaiseAndSetIfChanged(ref _colorA, value);
        }

        public double ColorB
        {
            get => _colorB;
            set => this.RaiseAndSetIfChanged(ref _colorB, value);
        }

        public string Lab
        {
            get => _lab;
            set => this.RaiseAndSetIfChanged(ref _lab, value);
        }

        public string FiringType
        {
            get => _firingType;
            set
            {
                this.RaiseAndSetIfChanged(ref _firingType, value);
                FiringTypeString = $"Firing Method: {value}";
            }
        }
        public string FiringTypeString
        {
            get => _firingTypeString;
            private set => this.RaiseAndSetIfChanged(ref _firingTypeString, value);
        }
        public string SoilType
        {
            get => _soilType;
            set
            {
                this.RaiseAndSetIfChanged(ref _soilType, value);
                SoilTypeString = $"Soil Type: {value}";
            }
        }
        public string SoilTypeString
        {
            get => _soilTypeString;
            private set => this.RaiseAndSetIfChanged(ref _soilTypeString, value);
        }

        public string ChemicalComposition
        {
            get => _chemicalComposition;
            set
            {
                this.RaiseAndSetIfChanged(ref _chemicalComposition, value);
                ChemicalCompositionString = $"Chemical Formula: {value}";
            }
        }
        public string ChemicalCompositionString
        {
            get => _chemicalCompositionString;
            private set => this.RaiseAndSetIfChanged(ref _chemicalCompositionString, value);
        }
        public string SurfaceCondition
        {
            get => _surfaceCondition;
            set
            {
                this.RaiseAndSetIfChanged(ref _surfaceCondition, value);
                SurfaceConditionString = $"Surface Condition: {value}";
            }
        }
        public string SurfaceConditionString
        {
            get => _surfaceConditionString;
            private set => this.RaiseAndSetIfChanged(ref _surfaceConditionString, value);
        }
        public string ColorName
        {
            get => _colorName;
            set => this.RaiseAndSetIfChanged(ref _colorName, value);
        }

        public string AutoTags
        {
            get => _autoTags;
            set => this.RaiseAndSetIfChanged(ref _autoTags, value);
        }

        public string AutoKeywords
        {
            get => _autoKeywords;
            set => this.RaiseAndSetIfChanged(ref _autoKeywords, value);
        }

        public double AiScore
        {
            get => _aiScore;
            set
            {
                this.RaiseAndSetIfChanged(ref _aiScore, value);
                AiScoreString = value > 0 ? $"AI Match: {System.Math.Clamp(value, 0, 1):P1}" : string.Empty;
            }
        }

        public string AiScoreString
        {
            get => _aiScoreString;
            private set => this.RaiseAndSetIfChanged(ref _aiScoreString, value);
        }

        public double AiClipScore
        {
            get => _aiClipScore;
            set => this.RaiseAndSetIfChanged(ref _aiClipScore, value);
        }

        public double AiColorScore
        {
            get => _aiColorScore;
            set => this.RaiseAndSetIfChanged(ref _aiColorScore, value);
        }

        public double AiMetadataScore
        {
            get => _aiMetadataScore;
            set => this.RaiseAndSetIfChanged(ref _aiMetadataScore, value);
        }

        public double AiVisualScore
        {
            get => _aiVisualScore;
            set => this.RaiseAndSetIfChanged(ref _aiVisualScore, value);
        }

        public double AiVisualPenalty
        {
            get => _aiVisualPenalty;
            set => this.RaiseAndSetIfChanged(ref _aiVisualPenalty, value);
        }

        public double AiExclusionPenalty
        {
            get => _aiExclusionPenalty;
            set => this.RaiseAndSetIfChanged(ref _aiExclusionPenalty, value);
        }

        public string AiFeedbackStatus
        {
            get => _aiFeedbackStatus;
            set => this.RaiseAndSetIfChanged(ref _aiFeedbackStatus, value);
        }
    }
}
