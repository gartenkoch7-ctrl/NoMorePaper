using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Inputs;

/// <summary><c>GET /api/inputs</c>: the administrator sees every row, a member never another account's.</summary>
public sealed class InputStatusApiTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheAdministrator_SeesEveryAccountsRow_AMemberOnlyTheSharedOnesAndItsOwn()
    {
        var (anna, annaId) = await api.CreateMemberAsync("inputs-anna", Ct);
        var (_, benId) = await api.CreateMemberAsync("inputs-ben", Ct);
        await RecordAsync(annaId, benId);

        using var admin = api.CreateAdminClient();
        var all = await admin.GetFromJsonAsync<InputStatusDto[]>("/api/inputs", Ct);
        var own = await anna.GetFromJsonAsync<InputStatusDto[]>("/api/inputs", Ct);

        Assert.Contains(all!, row => row.Kind == "icloud" && row.AccountName == "inputs-ben Name" && row.ReceivedTotal == 2);
        Assert.Contains(all!, row => row.Kind == "icloud" && row.AccountName == "inputs-anna Name");
        Assert.Contains(own!, row => row.Kind == "mail" && row.AccountName is null && row.Problem == "unreachable");
        Assert.Contains(own!, row => row.Kind == "icloud" && row.AccountName == "inputs-anna Name" && row.NeedsAttention && row.Problem == "code-needed");
        Assert.DoesNotContain(own!, row => row.AccountName == "inputs-ben Name");
        Assert.All(own!, row => Assert.True(row.AccountName is null or "inputs-anna Name"));
    }

    [Fact]
    public async Task WithoutSignIn_Is401()
    {
        using var anonymous = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/inputs", Ct)).StatusCode);
    }

    private async Task RecordAsync(Guid anna, Guid ben)
    {
        var scope = api.Services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var store = scope.ServiceProvider.GetRequiredService<IInputStatusStore>();
            var now = DateTimeOffset.UtcNow;
            await store.RecordAsync(InputKind.Mail, null, 0, new InputProblem(InputProblemKind.Unreachable), now, Ct);
            await store.RecordAsync(InputKind.ICloud, anna, 0, new InputProblem(InputProblemKind.CodeNeeded, NeedsAttention: true), now, Ct);
            await store.RecordAsync(InputKind.ICloud, ben, 2, null, now, Ct);
        }
    }
}
