namespace NoMorePaper.Application.Processing;

/// <summary>
/// Where processing attempts keep their working copies and the OCR tools their temporary files
/// (docs/kb/verarbeitung.md). Each attempt gets a folder of its own and deletes it at its end.
/// </summary>
public interface IWorkingFolder
{
    /// <summary>A new, empty folder for one attempt.</summary>
    /// <returns>Its full path.</returns>
    string CreateAttemptFolder();

    /// <summary>Writes <paramref name="content"/> to a new file in an attempt folder.</summary>
    /// <param name="attemptFolder">A folder from <see cref="CreateAttemptFolder"/>.</param>
    /// <param name="fileName">A fixed name chosen by the caller, never a received one.</param>
    /// <returns>The full path of the file.</returns>
    Task<string> WriteFileAsync(string attemptFolder, string fileName, Stream content, CancellationToken cancellationToken);

    /// <summary>Opens a file of an attempt folder for reading.</summary>
    Stream OpenRead(string path);

    /// <summary>Deletes an attempt folder with everything in it.</summary>
    /// <exception cref="IOException">The folder could not be deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder could not be deleted.</exception>
    void DeleteAttemptFolder(string attemptFolder);
}
