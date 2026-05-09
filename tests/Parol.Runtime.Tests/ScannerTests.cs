using Parol.Runtime.Scanner;

namespace Parol.Runtime.Tests;

public class ScannerTests
{
    [Fact]
    public void TestSimpleScan()
    {
        // Define a simple DFA that accepts "a"
        var states = new DfaState[]
        {
            new([new DfaTransition(1)], []),
            new([], [new(1, 0, new Lookahead.None())])
        };
        var dfa = new Dfa(states);
        var mode = new ScannerMode("INITIAL", [], dfa);
        var context = new ScannerContext([mode]);

        // Match function: 'a' is class 0
        static int? matchFn(char ch) => ch == 'a' ? 0 : null;

        var scanner = new FindMatches("a", 0, context, matchFn);
        var matches = scanner.ToList();
        
        Assert.Single(matches);
        Assert.Equal(1, matches[0].TokenType);
        Assert.Equal(0, matches[0].Span.Start);
        Assert.Equal(1, matches[0].Span.End);
    }

    [Fact]
    public void ScannerScan_UsesSourceModeSpecificSkipTokens()
    {
        var states = new DfaState[]
        {
            new([new DfaTransition(1), new DfaTransition(2)], []),
            new([null, null], [new(10, 0, new Lookahead.None())]),
            new([null, null], [new(20, 0, new Lookahead.None())])
        };
        var dfa = new Dfa(states);
        var modes = new[]
        {
            new ScannerMode("INITIAL", [new Transition(TransitionType.SetMode, 10, 1)], dfa),
            new ScannerMode("COMMENT", [], dfa)
        };

        static int? matchFn(char ch) => ch switch
        {
            'a' => 0,
            'b' => 1,
            _ => null
        };

        var tokens = Parol.Runtime.Scanner.Scanner.Scan(
                "ab",
                "test",
                matchFn,
                modes,
                [
                    [10],
                    []
                ])
            .ToList();

        Assert.Single(tokens);
        Assert.Equal(20, tokens[0].TokenType);
        Assert.Equal("b", tokens[0].Text);
    }

    [Fact]
    public void ScannerScan_DefaultSkipTokensAreBackwardCompatible()
    {
        var states = new DfaState[]
        {
            new([new DfaTransition(1), new DfaTransition(2)], []),
            new([null, null], [new(1, 0, new Lookahead.None())]),
            new([null, null], [new(5, 0, new Lookahead.None())])
        };
        var dfa = new Dfa(states);
        var modes = new[]
        {
            new ScannerMode("INITIAL", [], dfa)
        };

        static int? matchFn(char ch) => ch switch
        {
            'a' => 0,
            'b' => 1,
            _ => null
        };

        var tokens = Parol.Runtime.Scanner.Scanner.Scan("ab", "test", matchFn, modes).ToList();

        Assert.Single(tokens);
        Assert.Equal(5, tokens[0].TokenType);
        Assert.Equal("b", tokens[0].Text);
    }
}
