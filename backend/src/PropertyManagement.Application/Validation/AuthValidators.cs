using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using FluentValidation;

namespace PropertyManagement.Application.Validation;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role).NotEmpty().Must(r => Roles.All.Contains(r))
            .WithMessage($"Role must be one of: {string.Join(", ", Roles.All)}");
    }
}

public class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}

public class CreateCaseRequestValidator : AbstractValidator<CreateCaseRequest>
{
    public CreateCaseRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ClientId).NotEqual(Guid.Empty);
    }
}

public class CreatePmsIntegrationRequestValidator : AbstractValidator<CreatePmsIntegrationRequest>
{
    public CreatePmsIntegrationRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ClientId).NotEqual(Guid.Empty);
        RuleFor(x => x.SyncIntervalMinutes).InclusiveBetween(15, 10080);
        RuleFor(x => x.BaseUrl).MaximumLength(500);
    }
}

public class UpdatePmsIntegrationRequestValidator : AbstractValidator<UpdatePmsIntegrationRequest>
{
    public UpdatePmsIntegrationRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SyncIntervalMinutes).InclusiveBetween(15, 10080);
        RuleFor(x => x.BaseUrl).MaximumLength(500);
    }
}

public class PmsConnectionTestRequestValidator : AbstractValidator<PmsConnectionTestRequest>
{
    public PmsConnectionTestRequestValidator()
    {
        // Either Provider+BaseUrl OR nothing (when called against a stored integration via /{id}/test)
        When(x => x.Provider.HasValue, () =>
        {
            RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(500);
        });
    }
}

public class PmsSyncRequestValidator : AbstractValidator<PmsSyncRequest>
{
    public PmsSyncRequestValidator()
    {
        RuleFor(x => x).Must(r =>
                r.FullSync || r.SyncProperties || r.SyncUnits || r.SyncTenants || r.SyncLeases || r.SyncLedgerItems)
            .WithMessage("At least one sync scope must be selected (FullSync or one of the entity flags).");
    }
}
