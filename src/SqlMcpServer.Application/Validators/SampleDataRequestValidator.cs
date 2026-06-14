using FluentValidation;
using SqlMcpServer.Application.Models.Requests;

namespace SqlMcpServer.Application.Validators;

public sealed class SampleDataRequestValidator : AbstractValidator<SampleDataRequest>
{
    public SampleDataRequestValidator()
    {
        RuleFor(x => x.Schema).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Table).NotEmpty().MaximumLength(128);
        RuleFor(x => x.RowCount).InclusiveBetween(1, 1000);
    }
}
