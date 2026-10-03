using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Rules.DeleteRule;

/// <summary>Deletes an import rule of the current user.</summary>
public record DeleteRuleCommand(Guid Id) : IRequest<ErrorOr<Success>>;
