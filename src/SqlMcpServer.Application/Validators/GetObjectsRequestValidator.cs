using FluentValidation;
using SqlMcpServer.Application.Models.Requests;

namespace SqlMcpServer.Application.Validators;

public sealed class GetObjectsRequestValidator : AbstractValidator<GetObjectsRequest>
{
    public GetObjectsRequestValidator()
    {
        RuleFor(x => x.Database).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Schema).MaximumLength(128).When(x => x.Schema is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
    }
}
