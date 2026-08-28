using FluentValidation;

namespace NotificationHub.Application.Features.Segments.Save;

public sealed class SaveSegmentValidator : AbstractValidator<SaveSegmentCommand>
{
    public SaveSegmentValidator()
    {
        RuleFor(x => x.Segment.Key).NotEmpty().MaximumLength(128);
    }
}
