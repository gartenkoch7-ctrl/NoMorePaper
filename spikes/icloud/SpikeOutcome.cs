using ICloudSpike.Auth;
using ICloudSpike.Drive;
using ICloudSpike.Http;

namespace ICloudSpike;

/// <summary>What one run achieved, measured against the spike's criteria.</summary>
/// <param name="Path">How the session was obtained; null if the sign-in failed.</param>
/// <param name="RootItemCount">Entries in the root folder; null if the listing failed.</param>
/// <param name="Download">The downloaded file; null if none was chosen or it failed.</param>
/// <param name="Failure">Null if the run ended without an error.</param>
internal sealed record SpikeOutcome(LoginPath? Path, int? RootItemCount, DownloadedFile? Download, ICloudFailure? Failure)
{
    public int ExitCode => Failure is null ? 0 : 1;
}
