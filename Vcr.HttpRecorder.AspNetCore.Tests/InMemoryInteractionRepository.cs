using Vcr.HttpRecorder.Repositories;

namespace Vcr.HttpRecorder.AspNetCore.Tests;

/// <summary>
/// In-memory interaction repository used to avoid file I/O in tests.
/// </summary>
public sealed class InMemoryInteractionRepository : IInteractionRepository
{
    private Interaction? _stored;

    public Task<bool> ExistsAsync(string interactionName, CancellationToken cancellationToken = default)
        => Task.FromResult(_stored != null);

    public Task<Interaction> LoadAsync(string interactionName, CancellationToken cancellationToken = default)
        => Task.FromResult(_stored ?? throw new InvalidOperationException("No interaction stored"));

    public Task<Interaction> StoreAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        _stored = interaction;
        return Task.FromResult(interaction);
    }
}
