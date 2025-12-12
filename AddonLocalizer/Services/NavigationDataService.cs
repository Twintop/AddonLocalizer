using AddonLocalizer.Core.Models;

namespace AddonLocalizer.Services;

public class NavigationDataService
{
    private ParseResult? _currentParseResult;

    public void SetParseResult(ParseResult parseResult)
    {
        _currentParseResult = parseResult;
    }

    public ParseResult? GetParseResult()
    {
        return _currentParseResult;
    }

    public void ClearParseResult()
    {
        _currentParseResult = null;
    }
}
