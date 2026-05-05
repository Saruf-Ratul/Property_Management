using PropertyManagement.Application.Abstractions;

namespace PropertyManagement.Infrastructure.Multitenancy;

public class TenantContext : ITenantContext
{
    private bool _bypass;

    public Guid? LawFirmId { get; private set; }
    public bool BypassFilter => _bypass;

    public void SetTenant(Guid? lawFirmId) => LawFirmId = lawFirmId;

    public IDisposable Bypass()
    {
        _bypass = true;
        return new BypassScope(this);
    }

    private class BypassScope(TenantContext owner) : IDisposable
    {
        public void Dispose() => owner._bypass = false;
    }
}
