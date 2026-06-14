using FluentValidation;
using SqlMcpServer.Application.Models.Requests;

namespace SqlMcpServer.Application.Validators;

public sealed class ExecuteProcedureRequestValidator : AbstractValidator<ExecuteProcedureRequest>
{
    public ExecuteProcedureRequestValidator()
    {
        RuleFor(x => x.Schema).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Parameters)
            .Must(p => p is null || p.Count <= 100)
            .WithMessage("Procedure cannot have more than 100 parameters.");
    }
}
