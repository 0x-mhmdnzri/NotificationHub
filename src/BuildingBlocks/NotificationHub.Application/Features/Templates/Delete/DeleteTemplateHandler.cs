using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Templates;

namespace NotificationHub.Application.Features.Templates.Delete;

public sealed class DeleteTemplateHandler(ITemplateStore store)
    : IRequestHandler<DeleteTemplateCommand, Result>
{
    public async Task<Result> Handle(DeleteTemplateCommand request, CancellationToken cancellationToken)
    {
        var ok = await store.DeleteAsync(request.Key, request.Channel, request.Locale, request.TrustedTenantId, cancellationToken);
        return ok ? Result.Success() : Result.Failure(Errors.TemplateNotFound);
    }
}
