using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleDomain;
namespace SingularityBot.Behavior;
public sealed class BotCommandVocabularyMicroBundle : IMicroBundle
{
 public const ulong BundleId=51002; public const string Version="1.0.0"; public const string ProviderId="singularitybot.command-vocabulary";
 public MicroBundleDescriptor Descriptor {get;}=new(BundleId,Version,providers:[new MicroBundleProvider(ProviderId)]);
 public IReadOnlyList<MicroBundleDependencyRequest> Dependencies=>Array.Empty<MicroBundleDependencyRequest>();
 public IReadOnlyList<BotCommandVocabulary.ActionDefinition> Actions=>BotCommandVocabulary.All;
 public void Load(IMicroBundleLoadContext context){ArgumentNullException.ThrowIfNull(context);if(context.RuntimeId!=BotRuntimeComposer.RuntimeId)throw new InvalidOperationException("Unexpected bot runtime identity.");}
 public bool Arbitrate(IMicroBundleArbitrationContext context,int roundIndex){ArgumentNullException.ThrowIfNull(context);return false;}
}
public sealed class BotDefinitionMicroBundle : IMicroBundle
{
 public const ulong BundleId=51001; public const string Version="1.0.0"; public const string ProviderId="singularitybot.bot-definition";
 private bool _isLoaded;
 public BotDefinitionMicroBundle(BotDefinition definition){Definition=definition??throw new ArgumentNullException(nameof(definition));var errors=Definition.Validate();if(errors.Count!=0)throw new ArgumentException("Bot definition is invalid: "+string.Join(" ",errors),nameof(definition));}
 public BotDefinition Definition{get;} public bool IsLoaded=>_isLoaded;
 public MicroBundleDescriptor Descriptor{get;}=new(BundleId,Version,dependencies:[new MicroBundleDependency(BotCommandVocabularyMicroBundle.BundleId)],providers:[new MicroBundleProvider(ProviderId)]);
 public IReadOnlyList<MicroBundleDependencyRequest> Dependencies=>[MicroBundleDependencyRequest.Unconfigured(BotCommandVocabularyMicroBundle.BundleId)];
 public void Load(IMicroBundleLoadContext context){ArgumentNullException.ThrowIfNull(context);if(context.RuntimeId!=BotRuntimeComposer.RuntimeId)throw new InvalidOperationException("Unexpected bot runtime identity.");_isLoaded=true;}
 public bool Arbitrate(IMicroBundleArbitrationContext context,int roundIndex){ArgumentNullException.ThrowIfNull(context);return false;}
}
internal sealed class BotMicroBundleCatalog : IMicroBundleCatalog
{
 private readonly IReadOnlyDictionary<ulong,IMicroBundle> _bundles;
 public BotMicroBundleCatalog(BotDefinition definition){ArgumentNullException.ThrowIfNull(definition);_bundles=new Dictionary<ulong,IMicroBundle>{[BotDefinitionMicroBundle.BundleId]=new BotDefinitionMicroBundle(definition),[BotCommandVocabularyMicroBundle.BundleId]=new BotCommandVocabularyMicroBundle()};}
 public bool TryResolve(ulong bundleId,string version,out IMicroBundle? bundle){if(_bundles.TryGetValue(bundleId,out bundle)&&string.Equals(bundle.Descriptor.Version,version,StringComparison.Ordinal))return true;bundle=null;return false;}
 public bool TryResolve(ulong bundleId,out IMicroBundle? bundle)=>_bundles.TryGetValue(bundleId,out bundle);
}
public sealed record BotRuntimeComposition(TheSingularityWorkshop.FSM_COS.RuntimeAssembly Assembly,BotDefinition Definition,IReadOnlyList<BotCommandVocabulary.ActionDefinition> Actions);
public static class BotRuntimeComposer
{
 public const ulong RuntimeId=51000;
 public static BotRuntimeComposition Compose(BotDefinition definition)
 {
  ArgumentNullException.ThrowIfNull(definition);var errors=definition.Validate();if(errors.Count!=0)throw new ArgumentException("Bot definition is invalid: "+string.Join(" ",errors),nameof(definition));
  var manifest=new TheSingularityWorkshop.FSM_COS.RuntimeManifest(RuntimeId,[new TheSingularityWorkshop.FSM_COS.MicroBundleManifestEntry(BotDefinitionMicroBundle.BundleId,BotDefinitionMicroBundle.Version)]);
  var assembly=new TheSingularityWorkshop.FSM_COS.FsmCos(new BotMicroBundleCatalog(definition)).Execute(manifest);
  if(!assembly.TryGetBundle<BotDefinitionMicroBundle>(BotDefinitionMicroBundle.BundleId,out var root)||root is null||!root.IsLoaded)throw new InvalidOperationException("FSM_COS did not load the compiled bot definition.");
  if(!assembly.TryGetBundle<BotCommandVocabularyMicroBundle>(BotCommandVocabularyMicroBundle.BundleId,out var vocabulary)||vocabulary is null)throw new InvalidOperationException("FSM_COS did not compose the CommandAi vocabulary.");
  return new BotRuntimeComposition(assembly,root.Definition,vocabulary.Actions);
 }
}