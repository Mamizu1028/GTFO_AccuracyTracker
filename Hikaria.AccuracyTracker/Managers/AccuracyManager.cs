using Hikaria.Core;
using Hikaria.Core.SNetworkExt;
using Player;
using SNetwork;
using static Hikaria.AccuracyTracker.Handlers.AccuracyUpdater;
using Version = Hikaria.Core.Version;

namespace Hikaria.AccuracyTracker.Managers;

public static class AccuracyManager
{
    private static Version MinimumVer = new("1.2.0");

    internal static void Setup()
    {
        CoreAPI.OnPlayerModsSynced += OnPlayerModsSynced;
        GameEventAPI.OnPlayerEvent += OnPlayerEvent;
        GameEventAPI.OnMasterChanged += OnMasterChanged;
        s_AccuracyDataBroadcastAction = SNetExt_BroadcastAction<pAccuracyData>.Create($"{typeof(pAccuracyData).FullName}_v3", ReceiveAccuracyData, AccuracyDataListenerFilter, SNet_ChannelType.GameNonCritical);
        s_AccuracyDataBroadcastAction.OnPlayerAddedToListeners += SyncToPlayer;
        s_AccuracyDataBroadcastAction.OnPlayerRemovedFromListeners += UnregisterPlayer;
    }

    internal static void DoClear()
    {
        foreach (var data in AccuracyDataLookup.Values)
        {
            data.DoClear();
            data.NeedUpdate = true;
        }
    }

    private static void SyncToPlayer(SNet_Player player)
    {
        foreach (var data in AccuracyDataLookup.Values)
        {
            if (data.Owner.IsLocal || SNet.IsMaster)
            {
                s_AccuracyDataBroadcastAction.SyncToPlayer(player, data.GetAccuracyData());
            }
        }
    }

    private static void OnPlayerEvent(SNet_Player player, SNet_PlayerEvent playerEvent, SNet_PlayerEventReason reason)
    {
        if (playerEvent == SNet_PlayerEvent.PlayerIsSynced)
        {
            RegisterPlayer(player);
        }
    }

    private static bool AccuracyDataListenerFilter(SNet_Player player)
    {
        return CoreAPI.IsPlayerInstalledMod(player, PluginInfo.GUID, MinimumVer);
    }

    private static void OnPlayerModsSynced(SNet_Player player, IEnumerable<pModInfo> mods)
    {
        if (player.IsMaster)
        {
            IsMasterHasAcc = CoreAPI.IsPlayerInstalledMod(player, PluginInfo.GUID, MinimumVer);
        }
    }

    private static void ReceiveAccuracyData(ulong senderID, pAccuracyData data)
    {
        if (Instance != null && data.Owner.TryGetPlayer(out var player) && !player.IsLocal)
        {
            Instance.UpdateAccuracyData(data);
        }
    }

    internal static void SendAccuracyData(AccuracyData data)
    {
        s_AccuracyDataBroadcastAction.Do(data.GetAccuracyData());
    }

    private static void OnMasterChanged()
    {
        IsMasterHasAcc = CoreAPI.IsPlayerInstalledMod(SNet.Master, PluginInfo.GUID, MinimumVer);
    }

    public static bool IsAccuracyListener(ulong lookup)
    {
        return s_AccuracyDataBroadcastAction.IsListener(lookup);
    }

    public static bool IsMasterHasAcc { get; private set; }

    private static SNetExt_BroadcastAction<pAccuracyData> s_AccuracyDataBroadcastAction;

    public static Dictionary<ulong, AccuracyData> AccuracyDataLookup { get; private set; } = new();

    public struct pAccuracyData
    {
        internal pAccuracyData(SNet_Player player, Dictionary<InventorySlot, AccuracyData.AccuracySlotData> slotDatas)
        {
            Owner.SetPlayer(player);
            if (slotDatas.TryGetValue(InventorySlot.GearStandard, out var standardSlotData))
            {
                StandardSlotData = new(standardSlotData);
            }
            if (slotDatas.TryGetValue(InventorySlot.GearSpecial, out var specialSlotData))
            {
                SpecialSlotData = new(specialSlotData);
            }
        }

        public SNetStructs.pPlayer Owner = new();
        public pAccuracySlotData StandardSlotData = new();
        public pAccuracySlotData SpecialSlotData = new();
    }

    public struct pAccuracySlotData
    {
        internal pAccuracySlotData(AccuracyData.AccuracySlotData data)
        {
            Hitted = data.m_Hitted;
            Shotted = data.m_Shotted;
            WeakspotHitted = data.m_WeakspotHitted;
            Slot = data.m_Slot;
        }

        public uint Hitted { get; private set; } = 0;
        public uint Shotted { get; private set; } = 0;
        public uint WeakspotHitted { get; private set; } = 0;
        public InventorySlot Slot { get; private set; } = InventorySlot.None;
    }
}
