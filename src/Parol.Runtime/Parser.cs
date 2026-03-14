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
    /// This is set by parser implementations (for example <see cref="LLKParser.Parse(IEnumerable{Token}, IUserActions, string?)"/>) for parser scope.
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
/// LALR parser production metadata.
/// </summary>
/// <param name="Lhs">Left-hand-side non-terminal index.</param>
/// <param name="SemanticMask">
/// Per-RHS-symbol semantic forwarding mask. <c>true</c> means the reduced symbol is forwarded
/// to the semantic action children; <c>false</c> means it is omitted.
/// </param>
public record LRProduction(int Lhs, bool[] SemanticMask)
{
    /// <summary>
    /// Number of symbols on the right-hand side.
    /// </summary>
    public int Len => SemanticMask.Length;
}

/// <summary>
/// Base type for LR parse table actions.
/// </summary>
public abstract record LRAction
{
    /// <summary>
    /// Shift the current token and move to <paramref name="NextState"/>.
    /// </summary>
    public sealed record Shift(int NextState) : LRAction;

    /// <summary>
    /// Reduce by <paramref name="ProductionIndex"/> and continue with goto for <paramref name="NonTerminalIndex"/>.
    /// </summary>
    public sealed record Reduce(int NonTerminalIndex, int ProductionIndex) : LRAction;

    /// <summary>
    /// Accept the input.
    /// </summary>
    public sealed record Accept : LRAction;
}

/// <summary>
/// Terminal-to-action-index entry in an LR state.
/// </summary>
public record LRActionRef(int Terminal, int ActionIndex);

/// <summary>
/// Non-terminal-to-state transition in an LR state.
/// </summary>
public record LRGoto(int NonTerminal, int State);

/// <summary>
/// One state of the LALR parse table.
/// </summary>
public record LR1State(LRActionRef[] Actions, LRGoto[] Gotos)
{
    /// <summary>
    /// Returns the action index for the given terminal, or null if absent.
    /// </summary>
    public int? ActionIndex(int terminalIndex)
    {
        foreach (var entry in Actions)
        {
            if (entry.Terminal == terminalIndex)
            {
                return entry.ActionIndex;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns the goto state for the given non-terminal, or null if absent.
    /// </summary>
    public int? GotoState(int nonTerminalIndex)
    {
        foreach (var entry in Gotos)
        {
            if (entry.NonTerminal == nonTerminalIndex)
            {
                return entry.State;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns all terminal indices with actions in this state.
    /// </summary>
    public int[] ViableTerminalIndices()
    {
        var result = new int[Actions.Length];
        for (var i = 0; i < Actions.Length; i++)
        {
            result[i] = Actions[i].Terminal;
        }
        return result;
    }
}

/// <summary>
/// LALR parse table used by generated parsers.
/// </summary>
public record LRParseTable(LRAction[] Actions, LR1State[] States)
{
    /// <summary>
    /// Returns the parser action for <paramref name="state"/> and <paramref name="terminalIndex"/>, or null if absent.
    /// </summary>
    public LRAction? Action(int state, int terminalIndex)
    {
        var actionIndex = States[state].ActionIndex(terminalIndex);
        return actionIndex.HasValue ? Actions[actionIndex.Value] : null;
    }

    /// <summary>
    /// Returns the goto state for <paramref name="state"/> and <paramref name="nonTerminalIndex"/>, or null if absent.
    /// </summary>
    public int? Goto(int state, int nonTerminalIndex)
    {
        return States[state].GotoState(nonTerminalIndex);
    }

    /// <summary>
    /// Returns viable terminal indices for diagnostics in <paramref name="state"/>.
    /// </summary>
    public int[] ViableTerminalIndices(int state)
    {
        return States[state].ViableTerminalIndices();
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
        Parse(tokens, userActions, sourceName: null);
    }

    /// <summary>
    /// Parses <paramref name="tokens"/> with <paramref name="userActions"/>.
    /// If actions implement <see cref="IProvidesValueConverter"/>, the provided converter is activated
    /// for this parse call and restored afterwards.
    /// </summary>
    /// <param name="tokens">Input token stream.</param>
    /// <param name="userActions">Semantic action receiver.</param>
    /// <param name="sourceName">Optional source name (usually file path) used in diagnostic messages.</param>
    public void Parse(IEnumerable<Token> tokens, IUserActions userActions, string? sourceName = null)
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
                        var location = ParserDiagnosticLocation.FromToken(sourceName, currentToken);
                        throw new ParserSyntaxException(
                            _terminalNames[expected.Index],
                            expected.Index,
                            currentToken,
                            location);
                    }
                    tokenStream.Consume();
                    if (expected.Type == ParseType.T)
                    {
                        valueStack.Push(currentToken);
                    }
                }
                else if (expected.Type == ParseType.N)
                {
                    int productionIndex = PredictProduction(expected.Index, tokenStream, sourceName);
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
                            var location = ParserDiagnosticLocation.FromToken(sourceName, tokenStream.Peek(0));
                            throw new ParserInternalException(productionIndex, "missing child value", location);
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
                            var locationToken = children.OfType<Token>().FirstOrDefault();
                            var location = ParserDiagnosticLocation.FromToken(sourceName, locationToken);
                            throw new ParserSemanticException(
                                productionIndex,
                                rawTypes,
                                filteredTypes,
                                location,
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

    private int PredictProduction(int nonTerminalIndex, TokenStream tokens, string? sourceName)
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

        var lookaheadToken = tokens.Peek(0);
        var location = ParserDiagnosticLocation.FromToken(sourceName, lookaheadToken);
        var expectedTokenNames = _lookaheadAutomata[nonTerminalIndex].Transitions
            .Where(t => t.From == state)
            .Select(t => t.Term)
            .Distinct()
            .Select(term => term >= 0 && term < _terminalNames.Length ? _terminalNames[term] : $"token({term})")
            .ToArray();

        throw new ParserPredictionException(
            _nonTerminalNames[nonTerminalIndex],
            location,
            expectedTokenNames,
            lookaheadToken,
            lookaheadToken?.TokenType);
    }
}

/// <summary>
/// LALR(1) parser runtime for generated parser tables.
/// </summary>
public class LRParser(
    int startSymbolIndex,
    LRParseTable parseTable,
    LRProduction[] productions,
    string[] terminalNames,
    string[] nonTerminalNames)
{
    private readonly int _startSymbolIndex = startSymbolIndex;
    private readonly LRParseTable _parseTable = parseTable;
    private readonly LRProduction[] _productions = productions;
    private readonly string[] _terminalNames = terminalNames;
    private readonly string[] _nonTerminalNames = nonTerminalNames;

    /// <summary>
    /// Parses <paramref name="tokens"/> with <paramref name="userActions"/>.
    /// </summary>
    public void Parse(IEnumerable<Token> tokens, IUserActions userActions)
    {
        Parse(tokens, userActions, sourceName: null);
    }

    /// <summary>
    /// Parses <paramref name="tokens"/> with <paramref name="userActions"/>.
    /// If actions implement <see cref="IProvidesValueConverter"/>, the provided converter is activated
    /// for this parse call and restored afterwards.
    /// </summary>
    public void Parse(IEnumerable<Token> tokens, IUserActions userActions, string? sourceName = null)
    {
        var previousConverter = RuntimeValueConverter.Converter;
        if (userActions is IProvidesValueConverter converterProvider)
        {
            RuntimeValueConverter.Converter = converterProvider.ValueConverter;
        }

        var tokenStream = new TokenStream(tokens);
        var stateStack = new Stack<int>();
        var symbolStack = new Stack<object?>();
        stateStack.Push(0);

        try
        {
            while (true)
            {
                var token = tokenStream.Peek(0);
                var terminalIndex = token?.TokenType ?? 0;
                var currentState = stateStack.Peek();
                var action = _parseTable.Action(currentState, terminalIndex);

                if (action is null)
                {
                    var location = ParserDiagnosticLocation.FromToken(sourceName, token);
                    var expectedTokenNames = _parseTable
                        .ViableTerminalIndices(currentState)
                        .Select(term => term >= 0 && term < _terminalNames.Length ? _terminalNames[term] : $"token({term})")
                        .ToArray();
                    throw new ParserPredictionException(
                        $"state {currentState}",
                        location,
                        expectedTokenNames,
                        token,
                        token?.TokenType);
                }

                switch (action)
                {
                    case LRAction.Shift(var nextState):
                    {
                        var consumed = tokenStream.Consume();
                        if (consumed is null)
                        {
                            var location = ParserDiagnosticLocation.FromToken(sourceName, token);
                            throw new ParserInternalException(-1, "unexpected EOF during shift", location);
                        }
                        stateStack.Push(nextState);
                        symbolStack.Push(consumed);
                        break;
                    }
                    case LRAction.Reduce(var nonTerminalIndex, var productionIndex):
                    {
                        var value = ReduceProduction(
                            nonTerminalIndex,
                            productionIndex,
                            stateStack,
                            symbolStack,
                            userActions,
                            sourceName,
                            token);
                        symbolStack.Push(value);

                        var gotoState = _parseTable.Goto(stateStack.Peek(), nonTerminalIndex);
                        if (!gotoState.HasValue)
                        {
                            var location = ParserDiagnosticLocation.FromToken(sourceName, token);
                            throw new ParserInternalException(
                                productionIndex,
                                $"missing goto for non-terminal {nonTerminalIndex} in state {stateStack.Peek()}",
                                location);
                        }
                        stateStack.Push(gotoState.Value);
                        break;
                    }
                    case LRAction.Accept:
                    {
                        var startProduction = Array.FindIndex(_productions, p => p.Lhs == _startSymbolIndex);
                        if (startProduction >= 0)
                        {
                            _ = InvokeSemanticAction(
                                startProduction,
                                _productions[startProduction].SemanticMask,
                                symbolStack,
                                userActions,
                                sourceName,
                                token,
                                popSymbols: true);
                        }
                        return;
                    }
                }
            }
        }
        finally
        {
            RuntimeValueConverter.Converter = previousConverter;
        }
    }

    private object ReduceProduction(
        int nonTerminalIndex,
        int productionIndex,
        Stack<int> stateStack,
        Stack<object?> symbolStack,
        IUserActions userActions,
        string? sourceName,
        Token? currentToken)
    {
        var production = _productions[productionIndex];

        for (var i = 0; i < production.Len; i++)
        {
            if (stateStack.Count == 0)
            {
                var location = ParserDiagnosticLocation.FromToken(sourceName, currentToken);
                throw new ParserInternalException(productionIndex, "missing parser state during reduce", location);
            }
            stateStack.Pop();
        }

        return InvokeSemanticAction(
            productionIndex,
            production.SemanticMask,
            symbolStack,
            userActions,
            sourceName,
            currentToken,
            popSymbols: true);
    }

    private static object InvokeSemanticAction(
        int productionIndex,
        bool[] semanticMask,
        Stack<object?> symbolStack,
        IUserActions userActions,
        string? sourceName,
        Token? currentToken,
        bool popSymbols)
    {
        var popped = new object?[semanticMask.Length];
        if (popSymbols)
        {
            for (var i = semanticMask.Length - 1; i >= 0; i--)
            {
                if (symbolStack.Count == 0)
                {
                    var location = ParserDiagnosticLocation.FromToken(sourceName, currentToken);
                    throw new ParserInternalException(productionIndex, "missing symbol value", location);
                }
                popped[i] = symbolStack.Pop();
            }
        }
        else
        {
            var array = symbolStack.ToArray();
            if (array.Length < semanticMask.Length)
            {
                var location = ParserDiagnosticLocation.FromToken(sourceName, currentToken);
                throw new ParserInternalException(productionIndex, "insufficient symbols for semantic action", location);
            }
            for (var i = 0; i < semanticMask.Length; i++)
            {
                popped[i] = array[semanticMask.Length - 1 - i];
            }
        }

        var children = new List<object>(semanticMask.Length);
        for (var i = 0; i < semanticMask.Length; i++)
        {
            if (semanticMask[i] && popped[i] is not null)
            {
                children.Add(popped[i]!);
            }
        }

        try
        {
            return userActions.CallSemanticActionForProductionNumber(productionIndex, children.ToArray());
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
                return userActions.CallSemanticActionForProductionNumber(productionIndex, filteredChildren);
            }
            catch (InvalidOperationException retryException)
            {
                var rawTypes = string.Join(", ", children.Select(child => child?.GetType().Name ?? "null"));
                var filteredTypes = string.Join(", ", filteredChildren.Select(child => child?.GetType().Name ?? "null"));
                var locationToken = children.OfType<Token>().FirstOrDefault() ?? currentToken;
                var location = ParserDiagnosticLocation.FromToken(sourceName, locationToken);
                throw new ParserSemanticException(
                    productionIndex,
                    rawTypes,
                    filteredTypes,
                    location,
                    retryException);
            }
        }
    }
}


