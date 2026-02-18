using Parol.Runtime.Scanner;

namespace Parol.Runtime;

/// <summary>
/// Buffered token stream with lookahead support.
/// </summary>
public class TokenStream(IEnumerable<Token> tokens)
{
    private readonly IEnumerator<Token> _enumerator = tokens.GetEnumerator();
    private readonly List<Token> _buffer = new();
    private bool _eofReached = false;

    /// <summary>
    /// Peeks the token at lookahead distance <paramref name="k"/> without consuming it.
    /// </summary>
    /// <param name="k">Zero-based lookahead distance.</param>
    /// <returns>The looked-ahead token, or <c>null</c> at end of stream.</returns>
    public Token? Peek(int k)
    {
        while (_buffer.Count <= k && !_eofReached)
        {
            if (_enumerator.MoveNext())
            {
                _buffer.Add(_enumerator.Current);
            }
            else
            {
                _eofReached = true;
            }
        }

        return k < _buffer.Count ? _buffer[k] : null;
    }

    /// <summary>
    /// Consumes and returns the next token.
    /// </summary>
    /// <returns>The next token, or <c>null</c> if end of stream is reached.</returns>
    public Token? Consume()
    {
        if (_buffer.Count > 0)
        {
            var token = _buffer[0];
            _buffer.RemoveAt(0);
            return token;
        }

        if (!_eofReached)
        {
            if (_enumerator.MoveNext())
            {
                return _enumerator.Current;
            }
            _eofReached = true;
        }

        return null;
    }

    /// <summary>
    /// Gets whether end of stream has been reached and no buffered tokens remain.
    /// </summary>
    public bool IsEof => _eofReached && _buffer.Count == 0;
}
