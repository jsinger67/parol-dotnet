namespace Parol.Runtime.Scanner;

/// <summary>
/// Character span in the scanned input using start-inclusive and end-exclusive offsets.
/// </summary>
public record Span(int Start, int End)
{
    /// <summary>
    /// Gets the span length.
    /// </summary>
    public int Length => End - Start;
}

/// <summary>
/// Defines scanner mode transition behavior for matched tokens.
/// </summary>
public enum TransitionType
{
    /// <summary>
    /// Sets the current mode to the target mode.
    /// </summary>
    SetMode,
    /// <summary>
    /// Pushes the current mode to the mode stack and switches to the target mode.
    /// </summary>
    PushMode,
    /// <summary>
    /// Pops a mode from the mode stack and switches to it.
    /// </summary>
    PopMode
}

/// <summary>
/// Describes a scanner mode transition triggered by a token type.
/// </summary>
public record Transition(TransitionType Type, int TokenType, int? TargetMode = null);

/// <summary>
/// Represents a DFA transition to another state.
/// </summary>
public record DfaTransition(int To);

/// <summary>
/// Describes accepting information for a DFA state.
/// </summary>
public record AcceptData(int TokenType, int Priority, Lookahead Lookahead);

/// <summary>
/// Lookahead predicate attached to an accepting state.
/// </summary>
public abstract record Lookahead
{
    /// <summary>
    /// No lookahead constraint.
    /// </summary>
    public record None : Lookahead;
    /// <summary>
    /// Positive lookahead that must match.
    /// </summary>
    public record Positive(Dfa Dfa) : Lookahead;
    /// <summary>
    /// Negative lookahead that must not match.
    /// </summary>
    public record Negative(Dfa Dfa) : Lookahead;
}

/// <summary>
/// A DFA state with outgoing transitions and accepting configurations.
/// </summary>
public record DfaState(DfaTransition?[] Transitions, AcceptData[] AcceptData);

/// <summary>
/// Deterministic finite automaton used for scanning and lookahead.
/// </summary>
public record Dfa(DfaState[] States);

/// <summary>
/// Scanner mode definition with transitions and a DFA.
/// </summary>
public record ScannerMode(string Name, Transition[] Transitions, Dfa Dfa);

/// <summary>
/// A scanner match containing byte span, token type, and optional line/column positions.
/// </summary>
public record Match(Span Span, int TokenType, Positions? Positions = null);

/// <summary>
/// A one-based line and column position.
/// </summary>
public record Position(int Line, int Column);

/// <summary>
/// Start and end positions for a matched token.
/// </summary>
public record Positions(Position Start, Position End);
