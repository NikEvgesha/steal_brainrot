public static class CurrencyText
{
    public const string CoinIcon = "<sprite=\"Coin\" index=0>";

    public static string Coins(string amount)
    {
        return CoinIcon + amount;
    }
}
