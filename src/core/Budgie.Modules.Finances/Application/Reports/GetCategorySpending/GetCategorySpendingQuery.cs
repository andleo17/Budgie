using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Reports.GetCategorySpending;

public record GetCategorySpendingQuery(DateTime StartDate, DateTime EndDate) : IRequest<ErrorOr<List<CategorySpending>>>;
