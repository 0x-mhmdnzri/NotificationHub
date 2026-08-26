using MediatR;

namespace NotificationHub.Application.Abstractions;

/// <summary>Read intent — must not mutate business state.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
