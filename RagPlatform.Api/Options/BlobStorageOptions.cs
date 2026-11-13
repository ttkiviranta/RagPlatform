using System.ComponentModel.DataAnnotations;

namespace RagPlatform.Api.Options;

public sealed class BlobStorageOptions : IValidatableObject
{
    public const string SectionName = "BlobStorage";

    [Required]
    public string ServiceUri { get; set; } = string.Empty;

    [Required]
    [StringLength(63, MinimumLength = 3)]
    [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public string StagingContainerName { get; set; } = "document-staging";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(ServiceUri, UriKind.Absolute, out var serviceUri)
            || (serviceUri.Scheme != Uri.UriSchemeHttps
                && !(serviceUri.IsLoopback && serviceUri.Scheme == Uri.UriSchemeHttp))
            || !string.IsNullOrEmpty(serviceUri.UserInfo)
            || !string.IsNullOrEmpty(serviceUri.Query)
            || !string.IsNullOrEmpty(serviceUri.Fragment))
        {
            yield return new ValidationResult(
                "ServiceUri must be an HTTPS Azure Blob service URI; HTTP is allowed only for a loopback emulator.",
                [nameof(ServiceUri)]);
        }
    }
}
