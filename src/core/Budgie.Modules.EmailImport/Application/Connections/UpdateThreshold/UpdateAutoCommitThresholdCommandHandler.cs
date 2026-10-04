using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Connections.UpdateThreshold;

/// <inheritdoc cref="UpdateAutoCommitThresholdCommand"/>
internal sealed class UpdateAutoCommitThresholdCommandHandler(
    IEmailConnectionRepository emailConnectionRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateAutoCommitThresholdCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        UpdateAutoCommitThresholdCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        // GetByProviderAsync (LIMIT 1, documented for the multi-mailbox case) is correct here,
        // for the same reason it is in DisconnectEmailCommandHandler: the command acts on one
        // concrete row.
        var connection = await emailConnectionRepository.GetByProviderAsync(userId, request.Provider, ct);
        if (connection is null)
            return EmailImportErrors.NotConnected();

        // The domain rejects a value outside [0, 1] by throwing, which would surface as a 500.
        // A slider that lets a client post anything must answer with a validation error, not a
        // crash: catching here IS half of this endpoint's contract.
        try
        {
            connection.ChangeAutoCommitThreshold(request.Threshold);
        }
        catch (ArgumentException)
        {
            return EmailImportErrors.InvalidThreshold();
        }

        await emailConnectionRepository.UpdateAsync(connection, ct);

        return Result.Success;
    }
}