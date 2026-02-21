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
}
