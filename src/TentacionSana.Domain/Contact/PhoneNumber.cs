namespace TentacionSana.Domain.Contact;

public static class PhoneNumber
{
    public static string Normalize(string value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 8) digits = $"591{digits}";
        if (digits.Length is < 8 or > 15) throw new ArgumentException("El teléfono debe tener entre 8 y 15 dígitos.");
        return $"+{digits}";
    }
}
