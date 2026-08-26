using MediatR;

namespace NotificationHub.Application.Common.Interfaces;

/// <summary>Read-side request (no side effects). Routed through query pipeline behaviors only.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
