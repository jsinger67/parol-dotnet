using Parol.Runtime.Scanner;

namespace Parol.Runtime;

public enum ParseType
{
    T, // Terminal
    N, // Non-terminal
    E  // End of production
}

public record ParseItem(ParseType Type, int Index);

public record Production(int Lhs, ParseItem[] Rhs);

public record Trans(int From, int Term, int To, int ProdNum);

public record LookaheadDfa(int Prod0, Trans[] Transitions, int K);


public interface IUserActions
{
    void CallSemanticActionForProductionNumber(int productionNumber, object[] children);
    void OnComment(Token token);
}

public record Token(string Text, int TokenType, Match Match)
{
    public override string ToString() => $"Token({TokenType}, \"{Text}\")";
}

public class LLKParser(
    int startSymbolIndex,
    LookaheadDfa[] lookaheadAutomata,
    Production[] productions,
    string[] terminalNames,
    string[] nonTerminalNames)
{
    private readonly int _startSymbolIndex = startSymbolIndex;
    private readonly LookaheadDfa[] _lookaheadAutomata = lookaheadAutomata;

    private readonly Production[] _productions = productions;
    private readonly string[] _terminalNames = terminalNames;
    private readonly string[] _nonTerminalNames = nonTerminalNames;

    public void Parse(IEnumerable<Token> tokens, IUserActions userActions)
    {
        var stack = new Stack<ParseItem>();
        stack.Push(new ParseItem(ParseType.N, _startSymbolIndex));

        var tokenStream = new TokenStream(tokens);

        while (stack.Count > 0)
        {
            var expected = stack.Pop();

            if (expected.Type == ParseType.T)
            {
                var currentToken = tokenStream.Peek(0);
                if (currentToken == null || currentToken.TokenType != expected.Index)
                {
                    throw new Exception($"Syntax error: expected {_terminalNames[expected.Index]}, found {currentToken?.ToString() ?? "EOF"}");
                }
                tokenStream.Consume();
            }
            else if (expected.Type == ParseType.N)
            {
                int productionIndex = PredictProduction(expected.Index, tokenStream);
                var production = _productions[productionIndex];
                
                // Push RHS in reverse order
                for (int i = production.Rhs.Length - 1; i >= 0; i--)
                {
                    stack.Push(production.Rhs[i]);
                }
            }
        }
    }

    private int PredictProduction(int nonTerminalIndex, TokenStream tokens)
    {
        var dfa = _lookaheadAutomata[nonTerminalIndex];
        if (dfa.Transitions.Length == 0)
        {
            return dfa.Prod0;
        }

        int state = 0;
        int prodNum = dfa.Prod0;
        int lastProdNum = -1; // INVALID_PROD in Rust

        for (int i = 0; i < dfa.K; i++)
        {
            var token = tokens.Peek(i);
            int tokenType = token?.TokenType ?? 0; // 0 for EOF

            bool transitionFound = false;
            foreach (var transition in dfa.Transitions)
            {
                if (transition.From == state && transition.Term == tokenType)
                {
                    state = transition.To;
                    prodNum = transition.ProdNum;
                    if (transition.ProdNum >= 0)
                    {
                        lastProdNum = transition.ProdNum;
                    }
                    transitionFound = true;
                    break;
                }
            }

            if (!transitionFound) break;
        }

        if (prodNum >= 0) return prodNum;
        if (lastProdNum >= 0) return lastProdNum;
        
        throw new Exception($"Production prediction failed for non-terminal {_nonTerminalNames[nonTerminalIndex]}");
    }
}


