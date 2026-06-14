using FluentValidation;
using SqlMcpServer.Application.Models.Requests;

namespace SqlMcpServer.Application.Validators;

public sealed class ExecuteQueryRequestValidator : AbstractValidator<ExecuteQueryRequest>
{
    public ExecuteQueryRequestValidator()
    {
        RuleFor(x => x.Sql)
            .NotEmpty()
            .MaximumLength(65_536)
            .WithMessage("SQL must not be empty and must be under 64 KB.");

        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(1, 300)
            .WithMessage("Timeout must be between 1 and 300 seconds.");

        RuleFor(x => x.MaxRows)
            .InclusiveBetween(1, 10_000)
            .WithMessage("MaxRows must be between 1 and 10,000.");
    }
}
