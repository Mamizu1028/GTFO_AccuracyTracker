using TheArchive.Core;
using TheArchive.Core.Attributes;
using TheArchive.Core.FeaturesAPI;
using TheArchive.Core.Localization;

[assembly: ModDefaultFeatureGroupName("Accuracy Tracker")]

namespace Hikaria.AccuracyTracker;

[ArchiveDependency(Core.PluginInfo.GUID, "0.0.13")]
[ArchiveModule(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
public class EntryPoint : IArchiveModule
{
    public void Init()
    {
        Instance = this;
        Logs.LogMessage("OK");
    }

    public void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
    }

    public void OnLateUpdate()
    {
    }

    public void OnExit()
    {
    }

    public static EntryPoint Instance { get; private set; }

    public bool ApplyHarmonyPatches => false;

    public bool UsesLegacyPatches => false;

    public ArchiveLegacyPatcher Patcher { get; set; }

    public string ModuleGroup => FeatureGroups.GetOrCreateModuleGroup("Accuracy Tracker", new()
    {
        { Language.Chinese, "命中率指示器" },
        { Language.English, "Accuracy Tracker" }
    });

    public Dictionary<Language, string> ModuleGroupLanguages => new()
    {

    };
}
