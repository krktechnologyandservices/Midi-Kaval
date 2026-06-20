namespace MidiKaval.Api.Infrastructure.Email;

public sealed class EmailDeliveryException(string message) : Exception(message);
