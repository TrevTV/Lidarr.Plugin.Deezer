using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;
using NzbDrone.Plugin.Deezer;

namespace NzbDrone.Core.Indexers.Deezer
{
    public class DeezerIndexerSettingsValidator : AbstractValidator<DeezerIndexerSettings>
    {
        public DeezerIndexerSettingsValidator()
        {
        }
    }

    public class DeezerIndexerSettings : IIndexerSettings
    {
        private static readonly DeezerIndexerSettingsValidator Validator = new DeezerIndexerSettingsValidator();

        public DeezerIndexerSettings()
        {
            Arl = "";
        }

        [FieldDefinition(0, Label = "Arl", Type = FieldType.Textbox)]
        public string Arl
        {
            get
            {
                return _arl;
            }
            set
            {
                _arl = value;
                DeezerAPI.Instance?.CheckAndSetARL(value);
            }
        }
        private string _arl;

        [FieldDefinition(1, Type = FieldType.Number, Label = "Early Download Limit", Unit = "days", HelpText = "Time before release date Lidarr will download from this indexer, empty is no limit", Advanced = true)]
        public int? EarlyReleaseLimit { get; set; }

        // this is hardcoded so this doesn't need to exist except that it's required by the interface
        public string BaseUrl { get => "https://api.deezer.com/"; set => _ = value; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
