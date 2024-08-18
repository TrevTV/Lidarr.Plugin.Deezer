using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.Deezer
{
    public class DeezerSettingsValidator : AbstractValidator<DeezerSettings>
    {
        public DeezerSettingsValidator()
        {
        }
    }

    public class DeezerSettings : IProviderConfig
    {
        private static readonly DeezerSettingsValidator Validator = new DeezerSettingsValidator();

        public DeezerSettings()
        {
            Arl = "";
        }

        [FieldDefinition(0, Label = "Arl", Type = FieldType.Textbox)]
        public string Arl { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
