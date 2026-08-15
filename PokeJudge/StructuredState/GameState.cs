namespace PokeJudge.StructuredState;

// Owned by the application, not the model -- the LLM has no memory between
// calls, so every "known fact" has to be re-supplied from here each turn.
// Confirmed facts and hypotheses are deliberately separate lists with no
// promotion path between them: a hypothesis can never become a confirmed
// fact just by sitting in this object.
public sealed class GameState
{
    private readonly List<string> _confirmedFacts = new();
    private readonly List<string> _hypotheses = new();

    public IReadOnlyList<string> ConfirmedFacts => _confirmedFacts;
    public IReadOnlyList<string> Hypotheses => _hypotheses;

    public void AddConfirmedFacts(IEnumerable<string> facts) => _confirmedFacts.AddRange(facts);

    public void AddHypotheses(IEnumerable<string> hypotheses) => _hypotheses.AddRange(hypotheses);
}
