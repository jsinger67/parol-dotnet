namespace Parol.Runtime;

/// <summary>
/// Structured source location used by parser diagnostics.
/// </summary>
/// <param name="SourceName">Optional source name, usually the input file path.</param>
/// <param name="Line">Optional one-based line number.</param>
/// <param name="Column">Optional one-based column number.</param>
/// <param name="Offset">Optional zero-based character offset in source text.</param>
/// <param name="Length">Optional matched length in characters.</param>
public record ParserDiagnosticLocation(
    string? SourceName,
    int? Line,
    int? Column,
    int? Offset,
    int? Length)
{
    /// <summary>
    /// Creates a location from a parser token and optional source name.
    /// </summary>
    /// <param name="sourceName">Optional source name, usually the input file path.</param>
    /// <param name="token">Token to derive location data from.</param>
    /// <returns>A location object suitable for parser diagnostics.</returns>
    public static ParserDiagnosticLocation FromToken(string? sourceName, Token? token)
    {
        var positions = token?.Match.Positions;
        if (positions != null)
        {
            return new ParserDiagnosticLocation(
                sourceName,
                positions.Start.Line,
                positions.Start.Column,
                token?.Match.Span.Start,
                token?.Match.Span.Length);
        }

        if (token != null)
        {
            return new ParserDiagnosticLocation(
                sourceName,
                null,
                null,
                token.Match.Span.Start,
                token.Match.Span.Length);
        }

        return new ParserDiagnosticLocation(sourceName, null, null, null, null);
    }

    /// <summary>
    /// Returns a clickable diagnostic prefix such as "path:line:column: ".
    /// </summary>
    /// <returns>A prefix suitable for editor and terminal hyperlink parsing.</returns>
    public string ToClickablePrefix()
    {
        if (string.IsNullOrWhiteSpace(SourceName))
        {
            return string.Empty;
        }

        if (Line.HasValue && Column.HasValue)
        {
            return $"{SourceName}:{Line.Value}:{Column.Value}: ";
        }

        if (Offset.HasValue)
        {
            return $"{SourceName}:1:1 (offset {Offset.Value}): ";
        }

        return $"{SourceName}:1:1: ";
    }

    /// <summary>
    /// Returns a human-readable location suffix for messages.
    /// </summary>
    /// <returns>A location suffix that can be appended to diagnostic text.</returns>
    public string ToDisplaySuffix()
    {
        if (Line.HasValue && Column.HasValue)
        {
            return $" at line {Line.Value}, column {Column.Value}";
        }

        if (Offset.HasValue)
        {
            return $" at offset {Offset.Value}";
        }

        return " at EOF";
    }
}

/// <summary>
/// Base exception for parser failures.
/// </summary>
public abstract class ParserException : Exception
{
    /// <summary>
    /// Stable machine-readable parser error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Structured source location associated with this parser error.
    /// </summary>
    public ParserDiagnosticLocation Location { get; }

    /// <summary>
    /// Optional source name, usually the input file path.
    /// </summary>
    public string? SourceName => Location.SourceName;

    /// <summary>
    /// Optional one-based line number.
    /// </summary>
    public int? Line => Location.Line;

    /// <summary>
    /// Optional one-based column number.
    /// </summary>
    public int? Column => Location.Column;

    /// <summary>
    /// Optional zero-based character offset.
    /// </summary>
    public int? Offset => Location.Offset;

    /// <summary>
    /// Optional token or span length.
    /// </summary>
    public int? Length => Location.Length;

    /// <summary>
    /// Initializes a new parser exception.
    /// </summary>
    /// <param name="errorCode">Stable machine-readable parser error code.</param>
    /// <param name="coreMessage">Diagnostic message without location prefix or suffix.</param>
    /// <param name="location">Location information for the diagnostic.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    protected ParserException(string errorCode, string coreMessage, ParserDiagnosticLocation location, Exception? innerException = null)
        : base($"{location.ToClickablePrefix()}{coreMessage}{location.ToDisplaySuffix()}", innerException)
    {
        ErrorCode = errorCode;
        Location = location;
    }
}

/// <summary>
/// Exception thrown when the current token does not match the expected terminal.
/// </summary>
public sealed class ParserSyntaxException : ParserException
{
    /// <summary>
    /// Expected terminal symbol name.
    /// </summary>
    public string ExpectedTerminalName { get; }

    /// <summary>
    /// Expected terminal token type.
    /// </summary>
    public int ExpectedTokenType { get; }

    /// <summary>
    /// Actual token type found, or null for EOF.
    /// </summary>
    public int? ActualTokenType { get; }

    /// <summary>
    /// Initializes a syntax exception.
    /// </summary>
    /// <param name="expectedTerminalName">Expected terminal symbol name.</param>
    /// <param name="expectedTokenType">Expected terminal token type.</param>
    /// <param name="actualToken">Actual lookahead token, or null for EOF.</param>
    /// <param name="location">Location information for the diagnostic.</param>
    public ParserSyntaxException(
        string expectedTerminalName,
        int expectedTokenType,
        Token? actualToken,
        ParserDiagnosticLocation location)
        : base(
            "PAR001",
            $"Syntax error: expected {expectedTerminalName}, found {actualToken?.ToString() ?? "EOF"}",
            location)
    {
        ExpectedTerminalName = expectedTerminalName;
        ExpectedTokenType = expectedTokenType;
        ActualTokenType = actualToken?.TokenType;
    }
}

/// <summary>
/// Exception thrown when no production can be predicted for a non-terminal.
/// </summary>
public sealed class ParserPredictionException : ParserException
{
    /// <summary>
    /// Name of the non-terminal that failed prediction.
    /// </summary>
    public string NonTerminalName { get; }

    /// <summary>
    /// Token names expected in the failing DFA state.
    /// </summary>
    public IReadOnlyList<string> ExpectedTokenNames { get; }

    /// <summary>
    /// Actual token type observed at lookahead position 1, or null at EOF.
    /// </summary>
    public int? ActualTokenType { get; }

    /// <summary>
    /// Actual error token observed at lookahead position 1, or null at EOF.
    /// </summary>
    public Token? ErrorToken { get; }

    /// <summary>
    /// Initializes a production prediction exception.
    /// </summary>
    /// <param name="nonTerminalName">Non-terminal name that failed prediction.</param>
    /// <param name="location">Location information for the diagnostic.</param>
    /// <param name="expectedTokenNames">Token names expected in the failing DFA state.</param>
    /// <param name="errorToken">Actual lookahead token, or null at EOF.</param>
    /// <param name="actualTokenType">Actual lookahead token type, or null at EOF.</param>
    public ParserPredictionException(
        string nonTerminalName,
        ParserDiagnosticLocation location,
        IReadOnlyList<string> expectedTokenNames,
        Token? errorToken,
        int? actualTokenType)
        : base(
            "PAR002",
            BuildMessage(nonTerminalName, expectedTokenNames),
            location)
    {
        NonTerminalName = nonTerminalName;
        ExpectedTokenNames = expectedTokenNames;
        ErrorToken = errorToken;
        ActualTokenType = actualTokenType;
    }

    private static string BuildMessage(string nonTerminalName, IReadOnlyList<string> expectedTokenNames)
    {
        if (expectedTokenNames.Count == 0)
        {
            return $"Syntax error while trying to parse {nonTerminalName}";
        }

        var expected = string.Join(", ", expectedTokenNames);
        return $"Syntax error while trying to parse {nonTerminalName}. Expected one of: {expected}";
    }
}

/// <summary>
/// Exception thrown for internal parser runtime consistency errors.
/// </summary>
public sealed class ParserInternalException : ParserException
{
    /// <summary>
    /// Production index involved in the internal error.
    /// </summary>
    public int ProductionIndex { get; }

    /// <summary>
    /// Initializes an internal parser exception.
    /// </summary>
    /// <param name="productionIndex">Production index involved in the error.</param>
    /// <param name="detail">Additional detail text.</param>
    /// <param name="location">Location information for the diagnostic.</param>
    public ParserInternalException(int productionIndex, string detail, ParserDiagnosticLocation location)
        : base(
            "PAR003",
            $"Internal parser error: {detail} for production {productionIndex}",
            location)
    {
        ProductionIndex = productionIndex;
    }
}

/// <summary>
/// Exception thrown when semantic action mapping cannot be resolved by retry logic.
/// </summary>
public sealed class ParserSemanticException : ParserException
{
    /// <summary>
    /// Production index for which semantic mapping failed.
    /// </summary>
    public int ProductionIndex { get; }

    /// <summary>
    /// Child type names before filtering token children.
    /// </summary>
    public string RawChildTypes { get; }

    /// <summary>
    /// Child type names after filtering token children.
    /// </summary>
    public string FilteredChildTypes { get; }

    /// <summary>
    /// Initializes a semantic mapping exception.
    /// </summary>
    /// <param name="productionIndex">Production index for which mapping failed.</param>
    /// <param name="rawChildTypes">Child type names before filtering token children.</param>
    /// <param name="filteredChildTypes">Child type names after filtering token children.</param>
    /// <param name="location">Location information for the diagnostic.</param>
    /// <param name="innerException">Underlying semantic-action exception.</param>
    public ParserSemanticException(
        int productionIndex,
        string rawChildTypes,
        string filteredChildTypes,
        ParserDiagnosticLocation location,
        Exception innerException)
        : base(
            "PAR004",
            $"Semantic mapping failed for production {productionIndex}. Raw child types: [{rawChildTypes}], filtered child types: [{filteredChildTypes}]",
            location,
            innerException)
    {
        ProductionIndex = productionIndex;
        RawChildTypes = rawChildTypes;
        FilteredChildTypes = filteredChildTypes;
    }
}
