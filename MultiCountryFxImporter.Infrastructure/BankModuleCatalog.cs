namespace MultiCountryFxImporter.Infrastructure;

public static class BankModuleCatalog
{
    public const string MnbCode = "MNB";
    public const string EcbCode = "ECB";
    public const string DefaultModuleCode = MnbCode;

    public static string NormalizeCode(string? bankModuleCode)
    {
        if (string.IsNullOrWhiteSpace(bankModuleCode))
        {
            return DefaultModuleCode;
        }

        return bankModuleCode.Trim().ToUpperInvariant();
    }
}
