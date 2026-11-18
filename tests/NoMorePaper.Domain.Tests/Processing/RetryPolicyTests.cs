using NoMorePaper.Domain.Processing;
using Xunit;

namespace NoMorePaper.Domain.Tests.Processing;

public sealed class RetryPolicyTests
{
    private static readonly DateTimeOffset s_now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 30)]
    public void NextAttemptAt_WaitsLongerAfterEachFailure(int attempt, int minutes) =>
        Assert.Equal(s_now.AddMinutes(minutes), RetryPolicy.NextAttemptAt(attempt, s_now));

    [Theory]
    [InlineData(RetryPolicy.MaxAttempts)]
    [InlineData(RetryPolicy.MaxAttempts + 1)]
    [InlineData(int.MaxValue)]
    public void NextAttemptAt_GivesUp_AfterTheLastAttempt(int attempt) =>
        Assert.Null(RetryPolicy.NextAttemptAt(attempt, s_now));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NextAttemptAt_RejectsAttemptsBelowOne(int attempt) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RetryPolicy.NextAttemptAt(attempt, s_now));
}
