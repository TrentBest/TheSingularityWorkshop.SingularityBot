using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotCommandVocabularyTests
{
    [Fact]
    public void ActionAndParameterIdentitiesAreUniqueAndStable()
    {
        var actions = BotCommandVocabulary.All;

        Assert.Equal(5, actions.Count);
        Assert.Equal(actions.Count, actions.Select(action => action.Id).Distinct().Count());

        var parameters = actions.SelectMany(action => action.Parameters).ToArray();
        Assert.Equal(parameters.Length, parameters.Select(parameter => parameter.Id).Distinct().Count());
        Assert.All(actions, action => Assert.True(action.Id > 0));
        Assert.All(parameters, parameter => Assert.True(parameter.Id > 0));
    }

    [Fact]
    public void EveryActionDeclaresRequiredTypedParameters()
    {
        Assert.True(BotCommandVocabulary.TryGetAction(
            BotCommandVocabulary.Actions.SetProductionTarget, out var production));

        Assert.Collection(production.Parameters,
            target =>
            {
                Assert.Equal(BotCommandVocabulary.Parameters.ProductionTarget, target.Id);
                Assert.Equal(BotCommandVocabulary.ValueKind.Identifier, target.Kind);
                Assert.True(target.Required);
            },
            count =>
            {
                Assert.Equal(BotCommandVocabulary.Parameters.ProductionDesiredCount, count.Id);
                Assert.Equal(BotCommandVocabulary.ValueKind.LiteralText, count.Kind);
                Assert.True(count.Required);
            },
            priority =>
            {
                Assert.Equal(BotCommandVocabulary.Parameters.ProductionPriority, priority.Id);
                Assert.Equal(BotCommandVocabulary.ValueKind.LiteralText, priority.Kind);
                Assert.False(priority.Required);
            });
    }

    [Fact]
    public void UnknownActionIdentityFailsClosed()
    {
        Assert.False(BotCommandVocabulary.TryGetAction(ulong.MaxValue, out _));
    }

    [Fact]
    public void VocabularyDoesNotExposeRawSc2AbilityIds()
    {
        Assert.Equal("SingularityBot.Commands/1", BotCommandVocabulary.Version);
        Assert.DoesNotContain(BotCommandVocabulary.All, action =>
            action.Name.Contains("Ability", StringComparison.OrdinalIgnoreCase));
    }
}
