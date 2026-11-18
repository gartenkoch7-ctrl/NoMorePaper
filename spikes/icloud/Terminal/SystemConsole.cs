namespace ICloudSpike.Terminal;

/// <summary>The real console. Plain lines only, so a screen reader can follow the dialogue.</summary>
internal sealed class SystemConsole : IUserConsole
{
    public void WriteLine(string text) => Console.WriteLine(text);

    public string ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    public char[] ReadSecret(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            // A piped password would end up in shell history or a script file.
            throw new InvalidOperationException("The password must be typed in an interactive console.");
        }

        Console.Write(prompt);
        var typed = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Count > 0)
                {
                    typed.RemoveAt(typed.Count - 1);
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                typed.Add(key.KeyChar);
            }
        }

        // Nothing is echoed, not even placeholders: the length of the password stays private too.
        Console.WriteLine();
        var secret = typed.ToArray();
        for (var index = 0; index < typed.Count; index++)
        {
            typed[index] = '\0';
        }

        return secret;
    }
}
