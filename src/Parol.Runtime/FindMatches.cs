using System.Collections;

namespace Parol.Runtime.Scanner;

/// <summary>
/// Holds mutable scanner mode state during tokenization.
/// </summary>
public interface IScannerContext
{
    /// <summary>
    /// Available scanner modes.
    /// </summary>
    ScannerMode[] Modes { get; }
    /// <summary>
    /// Index of the currently active mode.
    /// </summary>
    int CurrentMode { get; set; }
    /// <summary>
    /// Mode stack used by push/pop transitions.
    /// </summary>
    Stack<int> ModeStack { get; }
    /// <summary>
    /// Applies mode transitions associated with the provided token type.
    /// </summary>
    void HandleModeTransition(int tokenType);
}

/// <summary>
/// Default scanner context implementation.
/// </summary>
public class ScannerContext(ScannerMode[] modes) : IScannerContext
{
    /// <summary>
    /// Available scanner modes.
    /// </summary>
    public ScannerMode[] Modes { get; } = modes;

    /// <summary>
    /// Index of the currently active mode.
    /// </summary>
    public int CurrentMode { get; set; } = 0;

    /// <summary>
    /// Mode stack used by push/pop transitions.
    /// </summary>
    public Stack<int> ModeStack { get; } = new();

    /// <summary>
    /// Applies scanner mode transitions for a produced token type.
    /// </summary>
    public void HandleModeTransition(int tokenType)
    {
        var mode = Modes[CurrentMode];
        var transition = mode.Transitions.FirstOrDefault(t => t.TokenType == tokenType);
        
        if (transition == null) return;

        switch (transition.Type)
        {
            case TransitionType.SetMode:
                CurrentMode = transition.TargetMode!.Value;
                break;
            case TransitionType.PushMode:
                ModeStack.Push(CurrentMode);
                CurrentMode = transition.TargetMode!.Value;
                break;
            case TransitionType.PopMode:
                if (ModeStack.TryPop(out var prev))
                {
                    CurrentMode = prev;
                }
                break;
        }
    }
}

/// <summary>
/// Enumerates scanner matches for an input string.
/// </summary>
public class FindMatches(string input, int offset, IScannerContext context, Func<char, int?> matchFunction) : IEnumerable<Match>
{
    private readonly CharIter _charIter = new(input, offset);
    private readonly IScannerContext _context = context;
    private readonly Func<char, int?> _matchFunction = matchFunction;

    /// <summary>
    /// Returns an enumerator over all matches produced from the current offset.
    /// </summary>
    public IEnumerator<Match> GetEnumerator()
    {
        while (true)
        {
            var match = FindNext();
            if (match != null)
            {
                _context.HandleModeTransition(match.TokenType);
                yield return match;
            }
            else
            {
                if (_charIter.Next() == null) break;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private Match? FindNext()
    {
        var dfa = _context.Modes[_context.CurrentMode].Dfa;
        int state = 0;
        CharItem? startItem = null;
        CharItem? bestEndItem = null;
        int? bestTokenType = null;
        int bestPriority = int.MaxValue;

        _charIter.SaveState();

        while (true)
        {
            var charItem = _charIter.Peek();
            if (charItem == null) break;

            int? classIdx = _matchFunction(charItem.Ch);
            if (classIdx == null) break;

            var currentState = dfa.States[state];
            if (classIdx >= currentState.Transitions.Length) break;
            
            var transition = currentState.Transitions[classIdx.Value];
            if (transition == null) break;

            state = transition.To;
            var nextState = dfa.States[state];

            _charIter.Next();
            startItem ??= charItem;

            foreach (var accept in nextState.AcceptData)
            {
                bool lookaheadSatisfied = true;
                if (accept.Lookahead is Lookahead.Positive pos)
                {
                    lookaheadSatisfied = CheckLookahead(pos.Dfa);
                }
                else if (accept.Lookahead is Lookahead.Negative neg)
                {
                    lookaheadSatisfied = !CheckLookahead(neg.Dfa);
                }

                if (lookaheadSatisfied)
                {
                    int currentLen = charItem.ByteIndex + 1 - startItem.ByteIndex;
                    int oldLen = bestEndItem != null ? bestEndItem.ByteIndex + 1 - startItem.ByteIndex : 0;

                    if (bestEndItem == null || currentLen > oldLen || (currentLen == oldLen && accept.Priority < bestPriority))
                    {
                        bestEndItem = charItem;
                        bestTokenType = accept.TokenType;
                        bestPriority = accept.Priority;
                        _charIter.SaveState();
                    }
                    break; // Priority order
                }
            }
        }

        if (bestEndItem != null)
        {
            _charIter.RestoreState();
            var endPos = new Position(bestEndItem.Position.Line, bestEndItem.Position.Column + 1);
            if (bestEndItem.Ch == '\n') endPos = new Position(bestEndItem.Position.Line + 1, 1);
            
            return new Match(
                new Span(startItem!.ByteIndex, bestEndItem.ByteIndex + 1),
                bestTokenType!.Value,
                new Positions(startItem.Position, endPos)
            );
        }

        _charIter.RestoreState();
        return null;
    }

    private bool CheckLookahead(Dfa dfa)
    {
        _charIter.SaveState();
        int state = 0;
        bool accepted = false;

        while (true)
        {
            var charItem = _charIter.Peek();
            if (charItem == null) break;

            int? classIdx = _matchFunction(charItem.Ch);
            if (classIdx == null) break;

            var currentState = dfa.States[state];
            if (classIdx >= currentState.Transitions.Length) break;

            var transition = currentState.Transitions[classIdx.Value];
            if (transition == null) break;

            state = transition.To;
            var nextState = dfa.States[state];

            _charIter.Next();

            if (nextState.AcceptData.Length > 0)
            {
                accepted = true;
            }
        }

        _charIter.RestoreState();
        return accepted;
    }
}

/// <summary>
/// Convenience API for scanning input into parser tokens.
/// </summary>
public static class Scanner
{
    /// <summary>
    /// Scans input text and yields tokens for non-skipped token types.
    /// </summary>
    /// <param name="input">Input text to scan.</param>
    /// <param name="fileName">Logical source file name.</param>
    /// <param name="matchFunction">Maps a character to its character class index.</param>
    /// <param name="modes">Scanner modes and DFAs.</param>
    /// <returns>Sequence of scanned tokens.</returns>
    public static IEnumerable<Token> Scan(string input, string fileName, Func<char, int?> matchFunction, ScannerMode[] modes)
    {
        var context = new ScannerContext(modes);
        var findMatches = new FindMatches(input, 0, context, matchFunction);
        foreach (var match in findMatches)
        {
            if (match.TokenType is 1 or 2 or 3 or 4)
            {
                continue;
            }
            yield return new Token(input.Substring(match.Span.Start, match.Span.Length), match.TokenType, match);
        }
    }
}

