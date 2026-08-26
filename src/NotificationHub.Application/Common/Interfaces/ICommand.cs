using MediatR;

namespace NotificationHub.Application.Common.Interfaces;

/// <summary>Write-side request (mutates state). Routed through command pipeline behaviors.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>Write-side with no payload response.</summary>
public interface ICommand : IRequest<Unit>;
