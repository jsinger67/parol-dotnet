namespace Parol.Runtime.Scanner;

public record CharItem(char Ch, int ByteIndex, Position Position);

public class CharIter(string input, int offset = 0)
{
    private readonly string _input = input;
    private int _offset = offset;
    private int _line = 1;
    private int _column = 1;
    
    private int _savedOffset;
    private int _savedLine;
    private int _savedColumn;

    public CharItem? Peek()
    {
        if (_offset >= _input.Length) return null;
        return new CharItem(_input[_offset], _offset, new Position(_line, _column));
    }

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

    public void SaveState()
    {
        _savedOffset = _offset;
        _savedLine = _line;
        _savedColumn = _column;
    }

    public void RestoreState()
    {
        _offset = _savedOffset;
        _line = _savedLine;
        _column = _savedColumn;
    }
}
