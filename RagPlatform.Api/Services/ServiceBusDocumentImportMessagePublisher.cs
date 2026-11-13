using System.Text.Json;
using Azure.Messaging.ServiceBus;
using RagPlatform.Api.Contracts.Documents;

namespace RagPlatform.Api.Services;

public sealed class ServiceBusDocumentImportMessagePublisher : IDocumentImportMessagePublisher
{
    private static readonly JsonSerializerOptions MessageJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ServiceBusSender _sender;

    public ServiceBusDocumentImportMessagePublisher(ServiceBusSender sender)
    {
        _sender = sender;
    }

    public Task PublishAsync(DocumentImportMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var serviceBusMessage = new ServiceBusMessage(
            BinaryData.FromObjectAsJson(message, MessageJsonOptions))
        {
            MessageId = message.DocumentId.ToString("N"),
            ContentType = "application/json",
            Subject = "DocumentImportRequested"
        };

        return _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }
}
