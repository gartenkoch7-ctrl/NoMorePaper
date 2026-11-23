using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Web.Client.Inputs;

namespace NoMorePaper.Web.Client.Start;

/// <summary>One input on the start page: its kind, its overall state and the rows that have a problem.</summary>
public sealed record StartInputLine(string Kind, InputRowState State, IReadOnlyList<InputStatusDto> Problems);
