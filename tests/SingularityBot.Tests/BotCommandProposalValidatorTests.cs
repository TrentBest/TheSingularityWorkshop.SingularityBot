using SingularityBot.Behavior;
using TheSingularityWorkshop.CommandAi;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotCommandProposalValidatorTests
{
    [Fact]
    public void AcceptsWellFormedMarineProductionTargetProposal()
    {
        var proposal = new CommandProposalBuilder()
            .AddCommand(BotCommandVocabulary.Actions.SetProductionTarget, command => command
                .WithIdentifier(BotCommandVocabulary.Parameters.ProductionTarget, 1)
                .WithText(BotCommandVocabulary.Parameters.ProductionDesiredCount, "5")
                .WithText(BotCommandVocabulary.Parameters.ProductionPriority, "50"))
            .Build();

        Assert.True(BotCommandProposalValidator.TryValidate(proposal, out var commands, out var error), error);
        var command = Assert.Single(commands);
        Assert.Equal("SetProductionTarget", command.Definition.Name);
        Assert.Equal("5", command.Arguments[BotCommandVocabulary.Parameters.ProductionDesiredCount].Text);
    }

    [Fact]
    public void RejectsUnknownAction()
    {
        var proposal = new CommandProposal([new CommandInstruction(ulong.MaxValue)]);

        Assert.False(BotCommandProposalValidator.TryValidate(proposal, out var commands, out var error));
        Assert.Empty(commands);
        Assert.Contains("Unknown action", error);
    }

    [Fact]
    public void RejectsMissingRequiredParameter()
    {
        var proposal = new CommandProposalBuilder()
            .AddCommand(BotCommandVocabulary.Actions.SetProductionTarget)
            .Build();

        Assert.False(BotCommandProposalValidator.TryValidate(proposal, out _, out var error));
        Assert.Contains("missing required parameter", error);
    }

    [Fact]
    public void RejectsUnknownParameter()
    {
        var proposal = new CommandProposalBuilder()
            .AddCommand(BotCommandVocabulary.Actions.SetProductionTarget, command => command
                .WithIdentifier(ulong.MaxValue, 1))
            .Build();

        Assert.False(BotCommandProposalValidator.TryValidate(proposal, out _, out var error));
        Assert.Contains("does not declare parameter", error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("five")]
    [InlineData("1.5")]
    public void RejectsInvalidProductionCounts(string count)
    {
        var proposal = new CommandProposalBuilder()
            .AddCommand(BotCommandVocabulary.Actions.SetProductionTarget, command => command
                .WithIdentifier(BotCommandVocabulary.Parameters.ProductionTarget, 1)
                .WithText(BotCommandVocabulary.Parameters.ProductionDesiredCount, count))
            .Build();

        Assert.False(BotCommandProposalValidator.TryValidate(proposal, out _, out var error));
        Assert.Contains("positive base-10 integer", error);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("high")]
    public void RejectsInvalidProductionPriority(string priority)
    {
        var proposal = new CommandProposalBuilder()
            .AddCommand(BotCommandVocabulary.Actions.SetProductionTarget, command => command
                .WithIdentifier(BotCommandVocabulary.Parameters.ProductionTarget, 1)
                .WithText(BotCommandVocabulary.Parameters.ProductionDesiredCount, "5")
                .WithText(BotCommandVocabulary.Parameters.ProductionPriority, priority))
            .Build();

        Assert.False(BotCommandProposalValidator.TryValidate(proposal, out _, out var error));
        Assert.Contains("0 through 100", error);
    }
}
