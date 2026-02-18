using Parol.Runtime.Scanner;
using System.Linq;

namespace Parol.Runtime;

/// <summary>
/// Parse stack symbol kinds used by the LL(k) parser runtime.
/// </summary>
public enum ParseType
{
    /// <summary>
    /// Terminal symbol.
    /// </summary>
    T, // Terminal
    /// <summary>
    /// Clipped terminal symbol (consumed but not forwarded as semantic child).
    /// </summary>
    C, // Clipped terminal
    /// <summary>
    /// Non-terminal symbol.
    /// </summary>
    N, // Non-terminal
    /// <summary>
    /// End-of-production marker.
    /// </summary>
    E  // End of production
}

/// <summary>
/// Item on the parse stack.
/// </summary>
public record ParseItem(ParseType Type, int Index);

/// <summary>
/// Grammar production definition.
/// </summary>
public record Production(int Lhs, ParseItem[] Rhs);

/// <summary>
/// Lookahead DFA transition for production prediction.
/// </summary>
public record Trans(int From, int Term, int To, int ProdNum);

/// <summary>
/// Lookahead DFA used to predict the production for a non-terminal.
/// </summary>
public record LookaheadDfa(int Prod0, Trans[] Transitions, int K);


/// <summary>
/// Callback contract implemented by generated user action classes.
/// </summary>
public interface IUserActions
{
    /// <summary>
    /// Invokes the semantic action for the specified production.
    /// </summary>
    /// <param name="productionNumber">Production number to execute.</param>
    /// <param name="children">Child values produced by the production's right-hand side.</param>
    /// <returns>The semantic value to push to the parser value stack.</returns>
    object CallSemanticActionForProductionNumber(int productionNumber, object[] children);

    /// <summary>
    /// Called for comment tokens emitted by generated parsers.
    /// </summary>
    /// <param name="token">The comment token.</param>
    void OnComment(Token token);
}

/// <summary>
/// Conversion hook used by generated actions to map parser values to target user-defined types.
/// </summary>
public interface IValueConverter
{
    /// <summary>
    /// Tries to convert <paramref name="value"/> into <paramref name="targetType"/>.
    /// </summary>
    /// <returns>
    /// True when conversion succeeded and <paramref name="convertedValue"/> contains a compatible value.
    /// </returns>
    bool TryConvert(object value, Type targetType, out object? convertedValue);
}

/// <summary>
/// Optional contract for action implementations that provide a parser-scoped <see cref="IValueConverter"/>.
/// </summary>
public interface IProvidesValueConverter
{
    /// <summary>
    /// The converter instance to use while parsing with this action object.
    /// </summary>
    IValueConverter ValueConverter { get; }
}

/// <summary>
/// Runtime conversion facade used by generated parser actions.
/// </summary>
public static class RuntimeValueConverter
{
    /// <summary>
    /// Active converter used by <see cref="Convert{TTarget}(object)"/>.
    /// This is set by <see cref="LLKParser.Parse(IEnumerable{Token}, IUserActions)"/> for parser scope.
    /// </summary>
    public static IValueConverter? Converter { get; set; }

    /// <summary>
    /// Converts <paramref name="value"/> into <typeparamref name="TTarget"/> using direct cast first,
    /// then the currently active <see cref="Converter"/>.
    /// </summary>
    public static TTarget Convert<TTarget>(object value)
    {
        if (value == null)
        {
            throw new InvalidCastException($"Cannot convert null to {typeof(TTarget).FullName}.");
        }

        if (value is TTarget direct)
        {
            return direct;
        }

        if (Converter != null &&
            Converter.TryConvert(value, typeof(TTarget), out var converted) &&
            converted is TTarget typed)
        {
            return typed;
        }

        throw new InvalidCastException(
            $"Cannot convert {value.GetType().FullName} to {typeof(TTarget).FullName}. " +
            "Configure Parol.Runtime.RuntimeValueConverter.Converter to provide a grammar-agnostic conversion.");
    }
}

/// <summary>
/// Token produced by scanner runtime and consumed by parser runtime.
/// </summary>
public record Token(string Text, int TokenType, Match Match)
{
    /// <summary>
    /// Returns a human-readable token representation.
    /// </summary>
    public override string ToString() => $"Token({TokenType}, \"{Text}\")";
}

/// <summary>
/// LL(k) parser runtime for generated parser tables.
/// </summary>
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

    /// <summary>
    /// Parses <paramref name="tokens"/> with <paramref name="userActions"/>.
    /// If actions implement <see cref="IProvidesValueConverter"/>, the provided converter is activated
    /// for this parse call and restored afterwards.
    /// </summary>
    /// <param name="tokens">Input token stream.</param>
    /// <param name="userActions">Semantic action receiver.</param>
    public void Parse(IEnumerable<Token> tokens, IUserActions userActions)
    {
        var previousConverter = RuntimeValueConverter.Converter;
        if (userActions is IProvidesValueConverter converterProvider)
        {
            RuntimeValueConverter.Converter = converterProvider.ValueConverter;
        }

        var stack = new Stack<ParseItem>();
        var valueStack = new Stack<object>();
        stack.Push(new ParseItem(ParseType.N, _startSymbolIndex));

        var tokenStream = new TokenStream(tokens);
        try
        {
            while (stack.Count > 0)
            {
                var expected = stack.Pop();

                if (expected.Type == ParseType.T || expected.Type == ParseType.C)
                {
                    var currentToken = tokenStream.Peek(0);
                    if (currentToken == null || currentToken.TokenType != expected.Index)
                    {
                        throw new Exception($"Syntax error: expected {_terminalNames[expected.Index]}, found {currentToken?.ToString() ?? "EOF"}");
                    }
                    tokenStream.Consume();
                    if (expected.Type == ParseType.T)
                    {
                        valueStack.Push(currentToken);
                    }
                }
                else if (expected.Type == ParseType.N)
                {
                    int productionIndex = PredictProduction(expected.Index, tokenStream);
                    var production = _productions[productionIndex];

                    stack.Push(new ParseItem(ParseType.E, productionIndex));

                    // Push RHS in reverse order
                    for (int i = production.Rhs.Length - 1; i >= 0; i--)
                    {
                        stack.Push(production.Rhs[i]);
                    }
                }
                else
                {
                    var productionIndex = expected.Index;
                    var production = _productions[productionIndex];
                    var childCount = production.Rhs.Count(item => item.Type != ParseType.C);
                    var children = new object[childCount];
                    for (int i = childCount - 1; i >= 0; i--)
                    {
                        if (valueStack.Count == 0)
                        {
                            throw new Exception($"Internal parser error: missing child value for production {productionIndex}");
                        }
                        children[i] = valueStack.Pop();
                    }

                    object value;
                    try
                    {
                        value = userActions.CallSemanticActionForProductionNumber(productionIndex, children);
                    }
                    catch (InvalidOperationException)
                    {
                        var hasTokenChild = children.Any(child => child is Token);
                        var hasNonTokenChild = children.Any(child => child is not Token);
                        if (!hasTokenChild || !hasNonTokenChild)
                        {
                            throw;
                        }

                        var filteredChildren = children.Where(child => child is not Token).ToArray();
                        try
                        {
                            value = userActions.CallSemanticActionForProductionNumber(productionIndex, filteredChildren);
                        }
                        catch (InvalidOperationException retryException)
                        {
                            var rawTypes = string.Join(", ", children.Select(child => child?.GetType().Name ?? "null"));
                            var filteredTypes = string.Join(", ", filteredChildren.Select(child => child?.GetType().Name ?? "null"));
                            throw new InvalidOperationException(
                                $"Semantic mapping failed for production {productionIndex}. Raw child types: [{rawTypes}], filtered child types: [{filteredTypes}]",
                                retryException);
                        }
                    }
                    valueStack.Push(value);
                }
            }
        }
        finally
        {
            RuntimeValueConverter.Converter = previousConverter;
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


