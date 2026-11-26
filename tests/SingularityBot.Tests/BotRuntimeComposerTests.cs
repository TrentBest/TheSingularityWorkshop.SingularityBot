using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotRuntimeComposerTests
{
    [Fact]
    public void Compose_LoadsValidatedDefinitionAndCommandVocabulary()
    {
        var definition = BotDefinitionCatalog.CreateDefault();

        var composition = BotRuntimeComposer.Compose(definition);

        Assert.NotNull(composition.Assembly);
        Assert.Same(definition, composition.Definition);
        Assert.Equal(
            BotCommandVocabulary.All.Select(action => action.Id),
            composition.Actions.Select(action => action.Id));
    }

    [Fact]
    public void Compose_RejectsNullDefinition()
    {
        Assert.Throws<ArgumentNullException>(
            () => BotRuntimeComposer.Compose(null!));
    }
}
