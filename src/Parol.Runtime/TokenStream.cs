using Parol.Runtime.Scanner;

namespace Parol.Runtime;

public class TokenStream(IEnumerable<Token> tokens)
{
    private readonly IEnumerator<Token> _enumerator = tokens.GetEnumerator();
    private readonly List<Token> _buffer = new();
    private bool _eofReached = false;

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

    public bool IsEof => _eofReached && _buffer.Count == 0;
}
