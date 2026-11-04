namespace AspireForge.Cli.Output;

public class ConsoleRenderer(TextWriter? writer = null)
{
    private const int DefaultWidth = 100;

    private readonly TextWriter _writer = writer ?? Console.Out;
    private readonly bool _isInteractiveConsole = writer is null && !Console.IsOutputRedirected;

    // Columns available for wrapping text: the terminal's width when writing to an interactive console
    // (kept between 60 and 120 so text stays readable), otherwise a fixed 100 - redirected output, CI
    // logs, and tests get stable line breaks.
    public int Width
    {
        get
        {
            if (!_isInteractiveConsole)
            {
                return DefaultWidth;
            }

            try
            {
                return Math.Clamp(Console.WindowWidth - 1, 60, 120);
            }
            catch (IOException)
            {
                return DefaultWidth;
            }
        }
    }

    public void WriteLine(string text = "") => _writer.WriteLine(text);

    public void WriteSectionHeader(string title)
    {
        _writer.WriteLine();
        _writer.WriteLine(title);
    }

    public void WriteCheck(string symbol, string text) => _writer.WriteLine($"  {symbol} {text}");

    // A check with a right-aligned tag, e.g. the rule ID next to a failing check.
    public void WriteCheck(string symbol, string text, string tag)
    {
        var line = $"  {symbol} {text}";
        var padding = Math.Max(2, Math.Min(Width, 72) - line.Length - tag.Length);
        _writer.WriteLine(line + new string(' ', padding) + tag);
    }

    public void WriteSeparator(int width = 32) => _writer.WriteLine(new string('-', width));

    // ═══════════ Findings (3) ═══════════
    public void WriteBanner(string title)
    {
        var total = Math.Min(Width, 72);
        var side = Math.Max(3, (total - title.Length - 2) / 2);
        _writer.WriteLine($"{new string('═', side)} {title} {new string('═', side)}");
    }
}
