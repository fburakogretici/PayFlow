using MediatR;
using PayFlow.SharedKernel.Results;

namespace PayFlow.SharedKernel.CQRS;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
