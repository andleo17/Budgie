using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Rules.DeleteRule;

/// <inheritdoc cref="DeleteRuleCommand"/>
/// <remarks>
/// Loaded with <c>(userId, id)</c> first so a foreign rule is a NotFound, never a silent
/// success that deletes someone else's data. The staging rows that referenced the rule keep
/// their history: the DDL sets <c>RuleId</c> to NULL on delete.
/// </remarks>
internal sealed class DeleteRuleCommandHandler(
    IEmailImportRuleRepository ruleRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DeleteRuleCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteRuleCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var rule = await ruleRepository.GetByIdAsync(userId, request.Id, ct);
        if (rule is null)
            return Error.NotFound("EmailImport.RuleNotFound", "Import rule not found.");

        await ruleRepository.DeleteAsync(userId, request.Id, ct);
        return Result.Success;
    }
}
