namespace AspireForge.Cli.Output;

public class ConsoleRenderer
{
    public void WriteLine(string text = "") => Console.WriteLine(text);

    public void WriteSectionHeader(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
    }

    public void WriteCheck(string symbol, string text) => Console.WriteLine($"  {symbol} {text}");

    public void WriteSeparator(int width = 32) => Console.WriteLine(new string('-', width));
}
