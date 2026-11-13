using System.ComponentModel.DataAnnotations;

namespace RagPlatform.Api.Options;

public sealed class ServiceBusOptions : IValidatableObject
{
    public const string SectionName = "ServiceBus";

    [Required]
    public string FullyQualifiedNamespace { get; set; } = string.Empty;

    [Required]
    [StringLength(260, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9](?:[A-Za-z0-9._-]{0,258}[A-Za-z0-9])?$")]
    public string ImportQueueName { get; set; } = "document-import";

    public bool DocumentImportWorkerEnabled { get; set; } = false;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FullyQualifiedNamespace)
            || FullyQualifiedNamespace.Contains("://", StringComparison.Ordinal)
            || !Uri.TryCreate($"https://{FullyQualifiedNamespace}", UriKind.Absolute, out var namespaceUri)
            || Uri.CheckHostName(namespaceUri.Host) != UriHostNameType.Dns
            || !string.Equals(namespaceUri.Host, FullyQualifiedNamespace, StringComparison.OrdinalIgnoreCase)
            || !namespaceUri.IsDefaultPort
            || namespaceUri.AbsolutePath != "/")
        {
            yield return new ValidationResult(
                "FullyQualifiedNamespace must be a DNS host name without a scheme, port, or path.",
                [nameof(FullyQualifiedNamespace)]);
        }
    }
}
