namespace TentacionSana.Application.Contact;

public static class WhatsAppLinkBuilder
{
    public static string? Build(string? configuredNumber, string? productName = null, string? presentation = null)
    {
        string number;
        try
        {
            number = TentacionSana.Domain.Contact.PhoneNumber.Normalize(configuredNumber ?? string.Empty).TrimStart('+');
        }
        catch (ArgumentException)
        {
            return null;
        }

        var message = string.IsNullOrWhiteSpace(productName)
            ? "Hola Tentación Sana, quiero consultar sus productos."
            : $"Hola Tentación Sana, quiero consultar por {productName.Trim()} ({presentation?.Trim()}). Cantidad: ";

        return $"https://wa.me/{number}?text={Uri.EscapeDataString(message)}";
    }
}
