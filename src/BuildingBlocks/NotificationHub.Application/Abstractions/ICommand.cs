using MediatR;

namespace NotificationHub.Application.Abstractions;

/// <summary>Write intent — may mutate state / outbox / domain.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface ICommand : IRequest<Result>;
