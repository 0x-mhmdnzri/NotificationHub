using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Campaigns;

namespace NotificationHub.Application.Features.Campaigns.ImportCsv;

public sealed class ImportCsvHandler(ICampaignService campaigns)
    : IRequestHandler<ImportCsvCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ImportCsvCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var n = await campaigns.ImportCsvAsync(request.CampaignId, request.CsvStream, request.TrustedTenantId, cancellationToken);
            return Result.Success(n);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<int>(Error.Failure("campaign.import_failed", ex.Message));
        }
    }
}
