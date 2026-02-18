namespace Parol.Runtime.Scanner;

/// <summary>
/// Character item with absolute index and line/column position.
/// </summary>
public record CharItem(char Ch, int ByteIndex, Position Position);

/// <summary>
/// Stateful character iterator over an input string.
/// </summary>
public class CharIter(string input, int offset = 0)
{
    private readonly string _input = input;
    private int _offset = offset;
    private int _line = 1;
    private int _column = 1;
    
    private int _savedOffset;
    private int _savedLine;
    private int _savedColumn;

    /// <summary>
    /// Peeks the current character without advancing the iterator.
    /// </summary>
    public CharItem? Peek()
    {
        if (_offset >= _input.Length) return null;
        return new CharItem(_input[_offset], _offset, new Position(_line, _column));
    }

    /// <summary>
    /// Returns the current character and advances to the next position.
    /// </summary>
    public CharItem? Next()
    {
        if (_offset >= _input.Length) return null;

        char ch = _input[_offset];
        var item = new CharItem(ch, _offset, new Position(_line, _column));

        _offset++;
        if (ch == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        return item;
    }

    /// <summary>
    /// Saves the current iterator state for later restoration.
    /// </summary>
    public void SaveState()
    {
        _savedOffset = _offset;
        _savedLine = _line;
        _savedColumn = _column;
    }

    /// <summary>
    /// Restores the iterator state saved by <see cref="SaveState"/>.
    /// </summary>
    public void RestoreState()
    {
        _offset = _savedOffset;
        _line = _savedLine;
        _column = _savedColumn;
    }
}
