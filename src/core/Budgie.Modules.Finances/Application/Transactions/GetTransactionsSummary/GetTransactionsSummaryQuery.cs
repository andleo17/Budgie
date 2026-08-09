using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.GetTransactionsSummary;

public record CurrencySummary(
    decimal Income,
    decimal Expenses,
    decimal Balance,
    string Currency);

public record GetTransactionsSummaryResponse(
    List<CurrencySummary> Currencies,
    string MainCurrency,
    decimal ApproximateIncome,
    decimal ApproximateExpenses);

public record GetTransactionsSummaryQuery(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyList<Guid>? AccountIds = null) : IRequest<ErrorOr<GetTransactionsSummaryResponse>>;
