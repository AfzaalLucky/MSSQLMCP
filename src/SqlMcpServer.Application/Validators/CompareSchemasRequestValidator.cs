using FluentValidation;
using SqlMcpServer.Application.Models.Requests;

namespace SqlMcpServer.Application.Validators;

public sealed class CompareSchemasRequestValidator : AbstractValidator<CompareSchemasRequest>
{
    public CompareSchemasRequestValidator()
    {
        RuleFor(x => x.SourceDatabase).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SourceSchema).NotEmpty().MaximumLength(128);
        RuleFor(x => x.TargetDatabase).NotEmpty().MaximumLength(128);
        RuleFor(x => x.TargetSchema).NotEmpty().MaximumLength(128);

        RuleFor(x => x)
            .Must(x => !(x.SourceDatabase == x.TargetDatabase && x.SourceSchema == x.TargetSchema))
            .WithMessage("Source and target schema must be different.");
    }
}
