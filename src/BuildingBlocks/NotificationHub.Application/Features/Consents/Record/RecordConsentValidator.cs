using FluentValidation;

namespace NotificationHub.Application.Features.Consents.Record;

public sealed class RecordConsentValidator : AbstractValidator<RecordConsentCommand>
{
    public RecordConsentValidator()
    {
        RuleFor(x => x.Record.SubjectId).NotEmpty();
        RuleFor(x => x.Record.Purpose).NotEmpty();
    }
}
