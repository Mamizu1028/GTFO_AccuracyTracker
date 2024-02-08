using GTFO.API;
using Player;
using SNetwork;
using static Hikaria.AccuracyTracker.Features.AccuracyTracker;
using static Hikaria.AccuracyTracker.Handlers.AccuracyUpdater;

namespace Hikaria.AccuracyTracker.Managers;

public static class AccuracyManager
{
    internal static void Setup()
    {
        NetworkAPI.RegisterEvent<pAccuracyData>(typeof(pAccuracyData).FullName, ReceiveAccuracyData);
        NetworkAPI.RegisterEvent<pBroadcastListenAccuracyData>(typeof(pBroadcastListenAccuracyData).FullName, ReceiveBroadcastListenAccuracyData);
    }

    private static void ReceiveBroadcastListenAccuracyData(ulong senderID, pBroadcastListenAccuracyData data)
    {
        if (SNet.Core.TryGetPlayer(senderID, out var player, true))
        {
            AccuracyDataListeners.TryAdd(player.Lookup, player);
            MarkAllAccuracyDataNeedUpdate();
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
        NetworkAPI.InvokeEvent(typeof(pAccuracyData).FullName, data.GetAccuracyData(), AccuracyDataListeners.Values.ToList(), SNet_ChannelType.GameNonCritical);
    }

    internal static void BroadcastAccuracyDataListener()
    {
        NetworkAPI.InvokeEvent(typeof(pBroadcastListenAccuracyData).FullName, broadcastData, SNet_ChannelType.GameNonCritical);
    }

    private static pBroadcastListenAccuracyData broadcastData = new();

    internal static void OnSessionMemberChanged(SNet_Player player, SessionMemberEvent playerEvent)
    {
        if (playerEvent == SessionMemberEvent.JoinSessionHub)
        {
            LobbyPlayers.TryAdd(player.Lookup, player);
            if (player.IsLocal)
            {
                AccuracyDataListeners.TryAdd(player.Lookup, player);
            }
            RegisterPlayer(player);
        }
        else if (playerEvent == SessionMemberEvent.LeftSessionHub)
        {
            LobbyPlayers.Remove(player.Lookup);
            if (player.IsLocal)
            {
                AccuracyDataListeners.Clear();
                UnregisterAllPlayers();
            }
            else
            {
                AccuracyDataListeners.Remove(player.Lookup);
                UnregisterPlayer(player.Lookup);
            }

        }
    }

    public static bool IsAccuracyListener(ulong lookup)
    {
        return AccuracyDataListeners.ContainsKey(lookup);
    }

    public static bool IsLobbyPlayer(ulong lookup)
    {
        return LobbyPlayers.ContainsKey(lookup);
    }

    public static bool IsMasterHasAcc => AccuracyDataListeners.Any(p => p.Key == SNet.Master.Lookup);

    private static Dictionary<ulong, SNet_Player> AccuracyDataListeners { get; set; } = new();

    private static Dictionary<ulong, SNet_Player> LobbyPlayers { get; set; } = new();

    private struct pBroadcastListenAccuracyData
    {
    }

    public struct pAccuracyData
    {
        internal pAccuracyData(SNet_Player player, Dictionary<InventorySlot, AccuracyData.AccuracySlotData> slotDatas)
        {
            Owner.SetPlayer(player);
            StandardSlotData = new();
            SpecialSlotData = new();
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
        public pAccuracySlotData StandardSlotData;
        public pAccuracySlotData SpecialSlotData;
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

        public uint Hitted = 0;
        public uint Shotted = 0;
        public uint WeakspotHitted = 0;
        public InventorySlot Slot = InventorySlot.None;
    }
}
