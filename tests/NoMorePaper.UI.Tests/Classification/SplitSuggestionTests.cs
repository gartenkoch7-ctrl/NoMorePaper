using Microsoft.Playwright;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>The split suggestion in "Zu prüfen": text, the three answers, the question before splitting, axe.</summary>
public sealed class SplitSuggestionTests(ServerFixture server)
{
    private static readonly SplitSuggestionDto _suggestion = new([new(1, 2), new(3, 3), new(4, 6)], [5]);

    public static TheoryData<string> Cultures() => new(SupportedCultures.All);

    // A stack that is otherwise confirmed: the suggestion is the only reason it is listed.
    private static ReviewItemDto Stack(string title = "Scan vom 05.03.2026") =>
        ClassificationSamples.Review(title, ClassificationSamples.Automatic() with { Recognition = DocumentRecognitionState.Confirmed })
            with { Split = _suggestion };

    private static string Section(ReviewItemDto item) => $"#review-split-{item.Id:N}";

    [Fact]
    public async Task Suggestion_ShowsTheSentence_TheParts_AndButtonsThatNameTheDocument()
    {
        var stack = Stack();
        await using var session = await new FakeReview(stack, ClassificationSamples.Review("Brief Finanzamt")).OpenAsync(server);
        var page = session.Page;

        var section = page.GetByRole(AriaRole.Region, new() { Name = "Trennvorschlag" });
        await Assertions.Expect(section).ToHaveCountAsync(1);
        await Assertions.Expect(section.GetByRole(AriaRole.Heading, new() { Level = 3 })).ToHaveTextAsync("Trennvorschlag");
        await Assertions.Expect(section).ToContainTextAsync("Trennvorschlag: 3 Dokumente (Seiten 1–2, 3, 4–6), Seite 5 vermutlich leer.");
        await Assertions.Expect(section.GetByRole(AriaRole.Listitem)).ToHaveTextAsync(["Dokument 1: Seiten 1–2", "Dokument 2: Seite 3", "Dokument 3: Seiten 4–6"]);
        await Assertions.Expect(section.GetByRole(AriaRole.Button, new() { Name = "Teilen: Scan vom 05.03.2026" })).ToBeVisibleAsync();
        await Assertions.Expect(section.GetByRole(AriaRole.Button, new() { Name = "Nicht teilen: Scan vom 05.03.2026" })).ToBeVisibleAsync();
        var adjust = section.GetByRole(AriaRole.Link, new() { Name = "Anpassen: Scan vom 05.03.2026" });
        await Assertions.Expect(adjust).ToHaveAttributeAsync("href", $"dokumente/{stack.Id}/seiten?trennen=1");
        // Only the stack has a suggestion; the other card shows none.
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Trennvorschlag" })).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Suggestion_WithOnlyBlankPages_NamesThemInTheSentence()
    {
        var stack = Stack() with { Split = new([new(1, 3)], [2, 4]) };
        await using var session = await new FakeReview(stack).OpenAsync(server);

        await Assertions.Expect(session.Page.GetByRole(AriaRole.Region, new() { Name = "Trennvorschlag" }))
            .ToContainTextAsync("Trennvorschlag: 1 Dokumente (Seiten 1–3), Seiten 2, 4 vermutlich leer.");
    }

    [Fact]
    public async Task Teilen_AsksFirst_AndSplitsOnlyAfterTheAnswer()
    {
        var (stack, other) = (Stack(), ClassificationSamples.Review("Brief Finanzamt"));
        var fake = new FakeReview(stack, other);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Teilen: Scan vom 05.03.2026" }).ClickAsync();

        var question = page.GetByRole(AriaRole.Group, new() { Name = "In 3 Dokumente teilen?" });
        await Assertions.Expect(question).ToBeVisibleAsync();
        await Assertions.Expect(question).ToContainTextAsync("Es entstehen 3 neue Dokumente. Das ursprüngliche Dokument kommt 30 Tage in den Papierkorb.");
        await Assertions.Expect(page.Locator($"{Section(stack)}-confirm-heading")).ToBeFocusedAsync();
        Assert.False(fake.Mocks.WasCalled("POST", $"/api/documents/{stack.Id}/split-suggestion/accept"));

        await question.GetByRole(AriaRole.Button, new() { Name = "Ja, teilen" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .live-region").First)
            .ToHaveTextAsync("„Scan vom 05.03.2026“ wird geteilt. Die neuen Dokumente erscheinen unter „Zu prüfen“. 1 Dokument zu prüfen.");
        Assert.True(fake.Mocks.WasCalled("POST", $"/api/documents/{stack.Id}/split-suggestion/accept"));
        await Assertions.Expect(page.Locator($"#review-h-{other.Id:N}")).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Trennvorschlag" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Question_CanBeCancelledWithEscape_AndFocusGoesBackToTeilen()
    {
        var stack = Stack();
        var fake = new FakeReview(stack);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Teilen: Scan vom 05.03.2026" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "In 3 Dokumente teilen?" })).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "In 3 Dokumente teilen?" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator($"{Section(stack)}-do")).ToBeFocusedAsync();
        Assert.False(fake.Mocks.WasCalled("POST", $"/api/documents/{stack.Id}/split-suggestion/accept"));
    }

    [Fact]
    public async Task NichtTeilen_DismissesAtOnce_AnnouncesIt_AndTheSuggestionIsGone()
    {
        var (stack, other) = (Stack(), ClassificationSamples.Review("Brief Finanzamt"));
        var fake = new FakeReview(stack, other);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Nicht teilen: Scan vom 05.03.2026" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .live-region").First).ToHaveTextAsync("„Scan vom 05.03.2026“ wird nicht geteilt. 1 Dokument zu prüfen.");
        Assert.True(fake.Mocks.WasCalled("POST", $"/api/documents/{stack.Id}/split-suggestion/dismiss"));
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Trennvorschlag" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator($"#review-h-{other.Id:N}")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Anpassen_OpensThePageEditorWithTheSplitPoints()
    {
        var stack = Stack();
        await using var session = await new FakeReview(stack).OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Link, new() { Name = "Anpassen: Scan vom 05.03.2026" }).ClickAsync();

        await page.WaitForURLAsync($"**/dokumente/{stack.Id}/seiten?trennen=1");
    }

    [Fact]
    public async Task Dismiss_AlreadyAnsweredElsewhere_LoadsTheListAgain_AndSaysSo()
    {
        var (stack, other) = (Stack(), ClassificationSamples.Review("Brief Finanzamt"));
        var fake = new FakeReview(stack, other) { SplitStatus = 409 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Nicht teilen: Scan vom 05.03.2026" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .form-error")).ToContainTextAsync("Die Angaben wurden inzwischen geändert und neu geladen.");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Scan vom 05.03.2026", "Brief Finanzamt"]);
    }

    [Fact]
    public async Task Accept_WhileAnEditRuns_ShowsTheErrorNextToTheButton_AndKeepsTheSuggestion()
    {
        var stack = Stack();
        var fake = new FakeReview(stack) { SplitStatus = 409 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Teilen: Scan vom 05.03.2026" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, teilen" }).ClickAsync();

        await Assertions.Expect(page.Locator($"{Section(stack)}-heading").Locator("xpath=..").Locator(".form-error"))
            .ToContainTextAsync("Das war nicht möglich: Das Dokument wird gerade bearbeitet oder hat sich geändert.");
        await Assertions.Expect(page.Locator($"{Section(stack)}-do")).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Trennvorschlag" })).ToHaveCountAsync(1);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task ReviewPage_WithASplitSuggestion_HasNoAxeViolations(string culture)
    {
        await using var session = await new FakeReview(Stack(), ClassificationSamples.Review("Brief Finanzamt")).OpenAsync(server, culture);
        await Assertions.Expect(session.Page.Locator(".split-suggestion")).ToHaveCountAsync(1);

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task ReviewPage_WithTheSplitQuestionOpen_HasNoAxeViolations(string culture)
    {
        var stack = Stack();
        await using var session = await new FakeReview(stack).OpenAsync(server, culture);
        var page = session.Page;

        await page.Locator($"{Section(stack)}-do").ClickAsync();
        await Assertions.Expect(page.Locator($"{Section(stack)}-confirm-heading")).ToBeFocusedAsync();

        await AxeCheck.NoViolationsAsync(page);
    }
}
