namespace Algos.Insights.AI;

public record AlgosAiRequest(string Question, IReadOnlyDictionary<string, object?> Context);
public record AlgosAiResponse(string Answer, bool IsConfigured);

public interface IAlgosAiProvider
{
    Task<AlgosAiResponse> AskAsync(AlgosAiRequest request, CancellationToken cancellationToken = default);
}

public sealed class DisabledAlgosAiProvider : IAlgosAiProvider
{
    public Task<AlgosAiResponse> AskAsync(AlgosAiRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AlgosAiResponse("AI assistant is disabled. Configure options.AI to enable it.", false));
}
