namespace AspireForge.Cli.Output;

public class ConsoleRenderer(TextWriter? writer = null)
{
    private readonly TextWriter _writer = writer ?? Console.Out;

    public void WriteLine(string text = "") => _writer.WriteLine(text);

    public void WriteSectionHeader(string title)
    {
        _writer.WriteLine();
        _writer.WriteLine(title);
    }

    public void WriteCheck(string symbol, string text) => _writer.WriteLine($"  {symbol} {text}");

    public void WriteSeparator(int width = 32) => _writer.WriteLine(new string('-', width));
}
