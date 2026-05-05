using PropertyManagement.Application.Abstractions;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Pms;

/// <summary>
/// Base for connectors whose live implementation is scheduled for a future phase.
/// All getters fail with a friendly NotSupportedException; TestConnectionAsync returns a clear failure
/// so the UI can render a sensible error.
/// </summary>
public abstract class NotImplementedConnectorBase : IPmsConnector
{
    public abstract PmsProvider Provider { get; }
    public string ExpectedPhase { get; }

    protected NotImplementedConnectorBase(string expectedPhase = "Phase 5")
    {
        ExpectedPhase = expectedPhase;
    }

    public Task<PmsConnectionTestOutcome> TestConnectionAsync(PmsConnectionContext ctx, CancellationToken ct) =>
        Task.FromResult(PmsConnectionTestOutcome.Fail(
            $"{Provider} connector is not implemented yet (planned: {ExpectedPhase}). " +
            "The marker interface and DI binding are in place — drop in the live implementation and you're done."));

#pragma warning disable CS1998 // Async method lacks 'await' operators — required by IAsyncEnumerable contract
    public async IAsyncEnumerable<PmsPropertyDto> GetPropertiesAsync(PmsConnectionContext ctx,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ThrowNotImplemented();
        yield break;
    }

    public async IAsyncEnumerable<PmsUnitDto> GetUnitsAsync(PmsConnectionContext ctx, string propertyExternalId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ThrowNotImplemented();
        yield break;
    }

    public async IAsyncEnumerable<PmsTenantDto> GetTenantsAsync(PmsConnectionContext ctx,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ThrowNotImplemented();
        yield break;
    }

    public async IAsyncEnumerable<PmsLeaseDto> GetLeasesAsync(PmsConnectionContext ctx,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ThrowNotImplemented();
        yield break;
    }

    public async IAsyncEnumerable<PmsLedgerItemDto> GetLedgerAsync(PmsConnectionContext ctx, string leaseExternalId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ThrowNotImplemented();
        yield break;
    }
#pragma warning restore CS1998

    private void ThrowNotImplemented() => throw new NotSupportedException(
        $"{Provider} connector is scheduled for {ExpectedPhase}. The IPmsConnector contract is wired up so " +
        "swapping in the real client is a one-class change.");
}

public class YardiConnector : NotImplementedConnectorBase, IYardiConnector
{
    public override PmsProvider Provider => PmsProvider.Yardi;
}

public class AppFolioConnector : NotImplementedConnectorBase, IAppFolioConnector
{
    public override PmsProvider Provider => PmsProvider.AppFolio;
}

public class BuildiumConnector : NotImplementedConnectorBase, IBuildiumConnector
{
    public override PmsProvider Provider => PmsProvider.Buildium;
}

public class PropertyFlowConnector : NotImplementedConnectorBase, IPropertyFlowConnector
{
    public override PmsProvider Provider => PmsProvider.PropertyFlow;
}

/// <summary>Resolves a connector by <see cref="PmsProvider"/> using the marker-interface DI bindings.</summary>
public class PmsConnectorFactory : IPmsConnectorFactory
{
    private readonly IServiceProvider _sp;
    public PmsConnectorFactory(IServiceProvider sp) => _sp = sp;

    public IPmsConnector Get(PmsProvider provider) => provider switch
    {
        PmsProvider.RentManager   => Resolve<IRentManagerConnector>(),
        PmsProvider.Yardi         => Resolve<IYardiConnector>(),
        PmsProvider.AppFolio      => Resolve<IAppFolioConnector>(),
        PmsProvider.Buildium      => Resolve<IBuildiumConnector>(),
        PmsProvider.PropertyFlow  => Resolve<IPropertyFlowConnector>(),
        _ => throw new NotSupportedException($"Unknown PMS provider: {provider}")
    };

    private T Resolve<T>() where T : class
        => (T?)_sp.GetService(typeof(T))
           ?? throw new InvalidOperationException(
               $"No connector is registered for {typeof(T).Name}. Check Infrastructure DI.");
}
