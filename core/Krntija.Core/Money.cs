namespace Krntija.Core.Economy;

/// <summary>
/// Единственное место, где решается, как выглядит цена. Если валюта снова
/// сменится, правится только этот файл: остальной код зовёт Money.Format.
/// </summary>
public static class Money
{
    public const string Symbol = "$";

    /// <summary>«15 $», «1 250 $», «0 $» — с разделителем тысяч и без копеек.</summary>
    public static string Format(double amount)
    {
        long rounded = (long)Math.Round(amount, MidpointRounding.AwayFromZero);
        string digits = Math.Abs(rounded).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
            .Replace(',', ' ');

        return rounded < 0 ? $"-{digits} {Symbol}" : $"{digits} {Symbol}";
    }
}
