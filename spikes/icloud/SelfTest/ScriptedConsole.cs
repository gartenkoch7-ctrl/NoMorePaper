using ICloudSpike.Terminal;

namespace ICloudSpike.SelfTest;

/// <summary>Console double: answers prompts from a script and records everything that is shown.</summary>
internal sealed class ScriptedConsole : IUserConsole
{
    private readonly Queue<string> _answers;
    private readonly Queue<string> _secrets;

    /// <param name="answers">Replies to normal prompts, in order.</param>
    /// <param name="secrets">Replies to password prompts, in order.</param>
    public ScriptedConsole(IEnumerable<string> answers, IEnumerable<string> secrets)
    {
        _answers = new Queue<string>(answers);
        _secrets = new Queue<string>(secrets);
    }

    /// <summary>Every line and prompt the user would have seen.</summary>
    public List<string> Transcript { get; } = [];

    public int UnusedAnswers => _answers.Count + _secrets.Count;

    public void WriteLine(string text) => Transcript.Add(text);

    public string ReadLine(string prompt)
    {
        Transcript.Add(prompt);

        // An unexpected prompt is a test failure, e.g. a restart that asks for a code again.
        return _answers.TryDequeue(out var answer)
            ? answer.Trim()
            : throw new InvalidOperationException($"Unexpected prompt: {prompt}");
    }

    public char[] ReadSecret(string prompt)
    {
        Transcript.Add(prompt);
        return _secrets.TryDequeue(out var secret)
            ? secret.ToCharArray()
            : throw new InvalidOperationException($"Unexpected password prompt: {prompt}");
    }
}
