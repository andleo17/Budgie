using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions;
using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using ErrorOr;
using Mediator;
using System.Globalization;
using System.Text;

namespace Budgie.Modules.Finances.Application.Export;

public enum ExportFormat
{
    Csv = 0,
    Json = 1
}

public record ExportTransactionsQuery(
    ExportFormat Format,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    Guid? AccountId = null) : IRequest<ErrorOr<string>>;

internal sealed class ExportTransactionsQueryHandler(
    ITransactionRepository txRepository)
    : IRequestHandler<ExportTransactionsQuery, ErrorOr<string>>
{
    public async ValueTask<ErrorOr<string>> Handle(ExportTransactionsQuery request, CancellationToken ct)
    {
        var query = new GetTransactionsQuery(
            AccountIds: request.AccountId is null ? null : [request.AccountId.Value],
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            PageSize: 100000);

        var items = await txRepository.GetTransactionsAsync(query, ct);

        return request.Format == ExportFormat.Csv
            ? BuildCsv(items)
            : BuildJson(items);
    }

    private static string BuildCsv(IReadOnlyList<TransactionResponse> items)
    {
        var sb = new StringBuilder();
        sb.Append("Date,Account,Category,Type,Amount,Currency,ExchangeRate,Notes\r\n");
        foreach (var t in items)
        {
            sb.Append(CsvEscape(t.Date.ToString("yyyy-MM-dd"))).Append(',');
            sb.Append(CsvEscape(t.AccountName)).Append(',');
            sb.Append(CsvEscape(t.CategoryName ?? string.Empty)).Append(',');
            sb.Append(CsvEscape(t.Type.ToString())).Append(',');
            sb.Append(CsvEscape(t.Amount.ToString("0.00", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(CsvEscape(t.Currency)).Append(',');
            sb.Append(CsvEscape(t.ExchangeRate.ToString("0.0000", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(CsvEscape(t.Notes ?? string.Empty)).Append("\r\n");
        }
        return sb.ToString();
    }

    private static string BuildJson(IReadOnlyList<TransactionResponse> items)
        => System.Text.Json.JsonSerializer.Serialize(items, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
