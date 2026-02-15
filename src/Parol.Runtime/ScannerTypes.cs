namespace Parol.Runtime.Scanner;

public record Span(int Start, int End)
{
    public int Length => End - Start;
}

public enum TransitionType
{
    SetMode,
    PushMode,
    PopMode
}

public record Transition(TransitionType Type, int TokenType, int? TargetMode = null);

public record DfaTransition(int To);

public record AcceptData(int TokenType, int Priority, Lookahead Lookahead);

public abstract record Lookahead
{
    public record None : Lookahead;
    public record Positive(Dfa Dfa) : Lookahead;
    public record Negative(Dfa Dfa) : Lookahead;
}

public record DfaState(DfaTransition?[] Transitions, AcceptData[] AcceptData);

public record Dfa(DfaState[] States);

public record ScannerMode(string Name, Transition[] Transitions, Dfa Dfa);

public record Match(Span Span, int TokenType, Positions? Positions = null);

public record Position(int Line, int Column);

public record Positions(Position Start, Position End);
