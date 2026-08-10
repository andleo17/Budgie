using Budgie.Shared.Infrastructure;
namespace Budgie.Modules.Finances.Infrastructure;

internal interface ITranslationService
{
    string Translate(string key, string language, params object[] args);
}

internal sealed class TranslationService : ITranslationService
{
    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new()
        {
            ["transfer.from_account"] = "Transfer from {0}",
            ["transfer.to_account"] = "Transfer to {0}",
            ["transfer.fee"] = "Transfer fee",
            ["loan.repayment"] = "Loan repayment",
            ["loan.repayment_from"] = "Repayment from {0}",
            ["adjustment"] = "Adjustment",
            ["interest"] = "Interest",
            ["account.initial_balance"] = "Initial balance",
            ["account.credit_line_setup"] = "Credit line setup",
        },
        ["es"] = new()
        {
            ["transfer.from_account"] = "Transferencia desde {0}",
            ["transfer.to_account"] = "Transferencia hacia {0}",
            ["transfer.fee"] = "Comisión de transferencia",
            ["loan.repayment"] = "Reembolso de préstamo",
            ["loan.repayment_from"] = "Reembolso desde {0}",
            ["adjustment"] = "Ajuste",
            ["interest"] = "Interés",
            ["account.initial_balance"] = "Saldo inicial",
            ["account.credit_line_setup"] = "Configuración de línea de crédito",
        },
    };

    public string Translate(string key, string language, params object[] args)
    {
        var dict = Translations.TryGetValue(language, out var lang) ? lang : Translations["en"];
        var template = dict.TryGetValue(key, out var value) ? value : Translations["en"].GetValueOrDefault(key, key);
        return args.Length > 0 ? string.Format(template, args) : template;
    }
}
