using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Rules.GetRules;

/// <summary>Returns every import rule of the current user, ordered by <c>Priority</c> ascending.</summary>
public record GetRulesQuery : IRequest<ErrorOr<List<EmailImportRuleResponse>>>;
