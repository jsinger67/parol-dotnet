using Xunit;
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
            new DfaState(new DfaTransition?[] { new DfaTransition(1) }, new AcceptData[0]),
            new DfaState(new DfaTransition?[0], new AcceptData[] { new AcceptData(1, 0, new Lookahead.None()) })
        };
        var dfa = new Dfa(states);
        var mode = new ScannerMode("INITIAL", new Transition[0], dfa);
        var context = new ScannerContext(new[] { mode });
        
        // Match function: 'a' is class 0
        Func<char, int?> matchFn = ch => ch == 'a' ? 0 : null;
        
        var scanner = new FindMatches("a", 0, context, matchFn);
        var matches = scanner.ToList();
        
        Assert.Single(matches);
        Assert.Equal(1, matches[0].TokenType);
        Assert.Equal(0, matches[0].Span.Start);
        Assert.Equal(1, matches[0].Span.End);
    }
}
