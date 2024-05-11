using BepInEx.Unity.IL2CPP.Utils;
using Hikaria.AccuracyTracker.Extensions;
using Hikaria.Core;
using Hikaria.Core.Interfaces;
using Player;
using SNetwork;
using System.Collections;
using TheArchive.Utilities;
using TMPro;
using UnityEngine;
using static Hikaria.AccuracyTracker.Features.AccuracyTracker;
using static Hikaria.AccuracyTracker.Managers.AccuracyManager;

namespace Hikaria.AccuracyTracker.Handlers;

public class AccuracyUpdater : MonoBehaviour, IOnMasterChanged
{
    private void Awake()
    {
        Instance = this;
        Setup();
        GameEventAPI.RegisterSelf(this);
        this.StartCoroutine(UpdateAccuracyDataCoroutine());
    }

    private void OnDestroy()
    {
        GameEventAPI.UnregisterSelf(this);
    }

    private void Setup()
    {
        if (IsSetup)
        {
            return;
        }
        AccuracyTextMeshesVisible[0] = false;
        AccuracyTextMeshesVisible[1] = false;
        AccuracyTextMeshesVisible[2] = false;
        AccuracyTextMeshesVisible[3] = false;

        PUI_Inventory inventory = GuiManager.Current.m_playerLayer.Inventory;
        foreach (RectTransform rectTransform in inventory.m_iconDisplay.GetComponentsInChildren<RectTransform>(true))
        {
            if (rectTransform.name == "Background Fade")
            {
                TextMeshPro textMeshPro = inventory.m_inventorySlots[InventorySlot.GearMelee].m_slim_archetypeName;
                for (int i = 0; i < 4; i++)
                {
                    GameObject gameObject = Instantiate(rectTransform.gameObject, rectTransform.parent);
                    RectTransform component = gameObject.GetComponent<RectTransform>();
                    gameObject.gameObject.SetActive(true);
                    foreach (Transform transform in gameObject.GetComponentsInChildren<Transform>(true))
                    {
                        if (transform.name == "TimerShowObject")
                        {
                            transform.gameObject.active = false;
                        }
                    }
                    gameObject.transform.localPosition = new Vector3(-70f + OffsetX, -62 + OffsetY + -35 * i, 0f);
                    AccuracyTextMeshes[i] = Instantiate(textMeshPro);
                    GameObject gameObject2 = new GameObject($"AccuracyTracker{i}")
                    {
                        layer = 5,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    gameObject2.transform.SetParent(component.transform, false);
                    AccuracyTextMeshes[i].m_width *= 2;
                    AccuracyTextMeshes[i].transform.SetParent(gameObject2.transform, false);
                    AccuracyTextMeshes[i].GetComponent<RectTransform>().anchoredPosition = new(-5f, 9f);
                    AccuracyTextMeshes[i].SetText("-: -%/-%(0/0)", true);
                    AccuracyTextMeshes[i].ForceMeshUpdate();
                }
                break;
            }
        }
        MarkAllAccuracyDataNeedUpdate();
        IsSetup = true;
    }

    public void OnMasterChanged()
    {
        CheckAndSetVisible();
    }

    internal static void CheckAndSetVisible()
    {
        if (!SNet.IsMaster)
        {
            foreach (var lookup in AccuracyDataLookup.Keys.ToList())
            {
                if (!IsAccuracyListener(lookup) && AccuracyRegisteredCharacterIndex.TryGetValue(lookup, out var index))
                {
                    SetVisible(index, false, false);
                }
            }
        }
        else
        {
            foreach (var pair in AccuracyRegisteredCharacterIndex)
            {
                SetVisible(pair.Value, true, false);
            }
        }
        UpdateVisible();
    }

    internal static void RegisterPlayer(SNet_Player player)
    {
        if (player.CharacterSlot != null)
        {
            AccuracyRegisteredCharacterIndex[player.Lookup] = player.CharacterSlot.index;
            AccuracyDataLookup[player.Lookup] = new(player);
            AccuracyDataLookup[player.Lookup].NeedUpdate = true;
        }
    }

    internal static void UnregisterPlayer(SNet_Player player)
    {
        if (player.IsLocal)
        {
            UnregisterAllPlayers();
        }
        else
        {
            UnregisterPlayer(player.Lookup);
        }
    }

    private IEnumerator UpdateAccuracyDataCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(3f);
        while (true)
        {
            foreach (var data in AccuracyDataLookup.Values)
            {
                var owner = data.Owner;
                if (data.NeedUpdate && AccuracyRegisteredCharacterIndex.TryGetValue(owner.Lookup, out var index))
                {
                    UpdateAccuracyTextMesh(index, data.GetAccuracyText());
                    data.NeedUpdate = false;
                    if (SNet.IsMaster && (owner.IsBot || !IsAccuracyListener(owner.Lookup)) || owner.IsLocal)
                    {
                        SendAccuracyData(data);
                    }
                    if (NeedShowAccuracy(owner))
                    {
                        if (!AccuracyTextMeshesVisible[index])
                        {
                            SetVisible(index, true);
                        }
                    }
                    else if (AccuracyTextMeshesVisible[index])
                    {
                        SetVisible(index, false);
                    }
                }
            }
            yield return yielder;
        }
    }

    internal void UpdateAccuracyData(pAccuracyData data)
    {
        if (!data.Owner.TryGetPlayer(out var player) || !AccuracyDataLookup.TryGetValue(player.Lookup, out var accData))
        {
            return;
        }
        accData.Set(data);
        accData.NeedUpdate = true;
    }

    private void UpdateAccuracyTextMesh(int index, string text)
    {
        if (AccuracyTextMeshes.TryGetValue(index, out var textMesh))
        {
            textMesh.SetText(text);
            textMesh.ForceMeshUpdate();
        }
    }

    internal static void MarkAllAccuracyDataNeedUpdate()
    {
        foreach (var data in AccuracyDataLookup.Values)
        {
            data.NeedUpdate = true;
        }
    }

    internal static void MarkAccuracyDataNeedUpdate(ulong lookup)
    {
        AccuracyDataLookup[lookup].NeedUpdate = true;
    }

    internal static void DoClear()
    {
        foreach (var data in AccuracyDataLookup.Values)
        {
            data.DoClear();
            data.NeedUpdate = true;
        }
    }

    internal static void SetVisible(int index, bool visible, bool update = true)
    {
        AccuracyTextMeshesVisible[index] = Enabled ? visible : false;
        if (update)
        {
            UpdateVisible();
        }
    }

    private static void UpdateVisible()
    {
        for (int i = 0; i < 4; i++)
        {
            if (!AccuracyTextMeshesVisible.ContainsKey(i))
            {
                continue;
            }
            int preInvisible = 0;
            for (int j = 0; j < 4; j++)
            {
                if (j <= i && !AccuracyTextMeshesVisible[j])
                {
                    preInvisible++;
                }
            }
            if (AccuracyTextMeshesVisible[i])
            {
                AccuracyTextMeshes[i].transform.parent.parent.gameObject.SetActive(true);
                AccuracyTextMeshes[i].transform.parent.parent.transform.localPosition = new(-70f + OffsetX, -62f + OffsetY + -35f * (i - preInvisible), 0f);
            }
            else
            {
                AccuracyTextMeshes[i].transform.parent.parent.gameObject.SetActive(false);
            }
        }
    }

    internal static void UnregisterAllPlayers()
    {
        foreach (var lookup in AccuracyRegisteredCharacterIndex.Keys)
        {
            UnregisterPlayer(lookup);
        }
        UpdateVisible();
    }

    internal static void UnregisterPlayer(ulong lookup)
    {
        if (AccuracyRegisteredCharacterIndex.TryGetValue(lookup, out var index))
        {
            SetVisible(index, false);
        }
        AccuracyDataLookup.Remove(lookup);
        AccuracyRegisteredCharacterIndex.Remove(lookup);
        ShotsBuffer.Remove(lookup);
    }

    internal static void AddHitted(ulong lookup, InventorySlot slot, uint count)
    {
        if (AccuracyDataLookup.TryGetValue(lookup, out var data))
        {
            data.AddHitted(slot, count);
        }
    }

    internal static void AddShotted(ulong lookup, InventorySlot slot, uint count)
    {
        if (AccuracyDataLookup.TryGetValue(lookup, out var data))
        {
            data.AddShotted(slot, count);
        }
    }

    internal static void AddWeakspotHitted(ulong lookup, InventorySlot slot, uint count)
    {
        if (AccuracyDataLookup.TryGetValue(lookup, out var data))
        {
            data.AddWeakspotHitted(slot, count);
        }
    }

    public static AccuracyUpdater Instance { get; private set; }

    public static int OffsetX
    {
        get
        {
            return _offsetX;
        }
        set
        {
            _offsetX = value;
            if (IsSetup)
            {
                UpdateVisible();
            }
        }
    }

    private static int _offsetX = 0;

    public static int OffsetY
    {
        get
        {
            return _offsetY;
        }
        set
        {
            _offsetY = value;
            if (IsSetup)
            {
                UpdateVisible();
            }
        }
    }

    private static int _offsetY = 0;

    public static bool Enabled
    {
        get
        {
            return _enable;
        }
        internal set
        {
            if (IsSetup)
            {
                MarkAllAccuracyDataNeedUpdate();
            }
            _enable = value;
        }
    }

    private static bool _enable = true;

    public static bool ShowOtherPlayersAcc
    {
        get
        {
            return _showOthersAcc;
        }
        internal set
        {
            if (IsSetup)
            {
                MarkAllAccuracyDataNeedUpdate();
            }
            _showOthersAcc = value;
        }
    }

    private static bool _showOthersAcc = true;

    public static bool ShowBotsAcc
    {
        get
        {
            return _showBotsAcc;
        }
        internal set
        {
            if (IsSetup)
            {
                MarkAllAccuracyDataNeedUpdate();
            }
            _showBotsAcc = value;
        }
    }

    private static bool _showBotsAcc = false;

    public static string ShowFormat
    {
        get
        {
            return _showFormat;
        }
        set
        {
            if (IsSetup)
            {
                MarkAllAccuracyDataNeedUpdate();
            }
            _showFormat = value;
        }
    }

    private static string _showFormat = "{0}: {1}/{2}({4}/{5})";

    public static string PageExpeditionSuccessShowFormat
    {
        get
        {
            return _pageExpeditionSuccessShowFormat;
        }
        set
        {
            if (IsSetup)
            {
                MarkAllAccuracyDataNeedUpdate();
            }
            _pageExpeditionSuccessShowFormat = value;
        }
    }

    private static string _pageExpeditionSuccessShowFormat = "{0}/{1}({2}/{3}/{4})";

    public static bool UseGenericName
    {
        get
        {
            return _useGenericName;
        }
        set
        {
            if (IsSetup)
            {
                MarkAllAccuracyDataNeedUpdate();
            }
            _useGenericName = value;
        }
    }

    private static bool _useGenericName = true;

    public static bool TryGetPlayerAccuracyData(SNet_Player player, out AccuracyData data)
    {
        return AccuracyDataLookup.TryGetValue(player.Lookup, out data);
    }

    private static bool NeedShowAccuracy(SNet_Player player)
    {
        if (!Settings.Enabled || (!player.IsLocal && !ShowOtherPlayersAcc) || (player.IsBot && !ShowBotsAcc))
        {
            return false;
        }
        return IsAccuracyListener(player.Lookup) || player.IsLocal || (player.IsBot && IsMasterHasAcc) || IsMasterHasAcc;
    }

    public static Dictionary<int, PlayerNameEntry> CharacterNamesLookup { get; set; } = new()
    {
        { 0, new("Wood", "RED") }, { 1, new("Dauda", "GRE") }, { 2, new("Hackett", "BLU") }, { 3, new("Bishop", "PUR") }
    };

    public static bool IsSetup
    {
        get
        {
            return _isSetup;
        }
        private set
        {
            _isSetup = value;
        }
    }

    private static bool _isSetup;

    private static Dictionary<int, TextMeshPro> AccuracyTextMeshes = new();
    private static Dictionary<int, bool> AccuracyTextMeshesVisible = new();
    private static Dictionary<ulong, int> AccuracyRegisteredCharacterIndex = new();
    internal static Dictionary<ulong, uint> ShotsBuffer = new();


    public class AccuracyData
    {
        internal AccuracyData(SNet_Player player)
        {
            Owner = player;
            AccuracySlotDataLookup[InventorySlot.GearStandard] = new(InventorySlot.GearStandard);
            AccuracySlotDataLookup[InventorySlot.GearSpecial] = new(InventorySlot.GearSpecial);
        }

        internal void Set(pAccuracyData data)
        {
            if (data.Owner.TryGetPlayer(out var player))
                Owner = player;
            AccuracySlotDataLookup[InventorySlot.GearStandard].Set(data.StandardSlotData);
            AccuracySlotDataLookup[InventorySlot.GearSpecial].Set(data.SpecialSlotData);
        }

        internal void AddShotted(InventorySlot slot, uint count)
        {
            if (AccuracySlotDataLookup.TryGetValue(slot, out var data))
            {
                data.m_Shotted += count;
            }
        }

        internal void AddHitted(InventorySlot slot, uint count)
        {
            if (AccuracySlotDataLookup.TryGetValue(slot, out var data))
            {
                data.m_Hitted += count;
            }
        }

        internal void AddWeakspotHitted(InventorySlot slot, uint count)
        {
            if (AccuracySlotDataLookup.TryGetValue(slot, out var data))
            {
                data.m_WeakspotHitted += count;
            }
        }

        internal void DoClear()
        {
            foreach (var data in AccuracySlotDataLookup.Values)
            {
                data.DoClear();
            }
        }

        public SNet_Player Owner { get; private set; }
        public uint TotalHitted
        {
            get
            {
                var count = 0U;
                foreach (var slot in AccuracySlotDataLookup.Keys)
                {
                    count += AccuracySlotDataLookup[slot].m_Hitted;
                }
                return count;
            }
        }
        public uint TotalWeakspotHitted
        {
            get
            {
                var count = 0U;
                foreach (var slot in AccuracySlotDataLookup.Keys)
                {
                    count += AccuracySlotDataLookup[slot].m_WeakspotHitted;
                }
                return count;
            }
        }
        public uint TotalShotted
        {
            get
            {
                var count = 0U;
                foreach (var slot in AccuracySlotDataLookup.Keys)
                {
                    count += AccuracySlotDataLookup[slot].m_Shotted;
                }
                return count;
            }
        }

        internal bool NeedUpdate = true;
        private Dictionary<InventorySlot, AccuracySlotData> AccuracySlotDataLookup = new();

        public string GetAccuracyText()
        {
            if (!Owner.HasCharacterSlot)
            {
                var result = string.Format(Settings.DisplayFormatInGame, "-", $"<{Settings.FontColors.HittedRatioColor.ToHexString()}>-%</color>", $"<{Settings.FontColors.WeakspotHittedRatioColor.ToHexString()}>-%</color>", $"<{Settings.FontColors.WeakspotHittedColor.ToHexString()}>0</color>", $"<{Settings.FontColors.HittedColor.ToHexString()}>0</color>", $"<{Settings.FontColors.ShottedColor.ToHexString()}>0</color>");
                return Settings.FontColors.EnableColorInGame ? result : string.Format(Settings.DisplayFormatInGame, "*-", "-%", "-%", 0, 0, 0);
            }
            string prefix = IsAccuracyListener(Owner.Lookup) || (IsMasterHasAcc && Owner.IsBot) || Owner.IsLocal ? "": "*";
            string playerName = UseGenericName ? CharacterNamesLookup[Owner.CharacterIndex].Name : Owner.NickName.RemoveHtmlTags();
            if (TotalShotted == 0)
            {
                var result = $"{prefix}{string.Format(Settings.DisplayFormatInGame, playerName, $"<{Settings.FontColors.HittedRatioColor.ToHexString()}>-%</color>", $"<{Settings.FontColors.WeakspotHittedRatioColor.ToHexString()}>-%</color>", $"<{Settings.FontColors.WeakspotHittedColor.ToHexString()}>0</color>", $"<{Settings.FontColors.HittedColor.ToHexString()}>0</color>", $"<{Settings.FontColors.ShottedColor.ToHexString()}>0</color>")}";
                return Settings.FontColors.EnableColorInGame ? result : $"{prefix}{string.Format(Settings.DisplayFormatInGame, playerName, "-%", "-%", 0, 0, 0)}";
            }
            else
            {
                var result = $"{prefix}{string.Format(Settings.DisplayFormatInGame, playerName, $"<{Settings.FontColors.HittedRatioColor.ToHexString()}>{(int)(100 * TotalHitted / TotalShotted)}%</color>", TotalHitted == 0 ? "-" : $"<{Settings.FontColors.WeakspotHittedRatioColor.ToHexString()}>{(int)(100 * TotalWeakspotHitted / TotalHitted)}%</color>", $"<{Settings.FontColors.WeakspotHittedColor.ToHexString()}>{TotalWeakspotHitted}</color>", $"<{Settings.FontColors.HittedColor.ToHexString()}>{TotalHitted}</color>", $"<{Settings.FontColors.ShottedColor.ToHexString()}>{TotalShotted}</color>")}";
                return Settings.FontColors.EnableColorInGame ? result : $"{prefix}{string.Format(Settings.DisplayFormatInGame, playerName, $"{(int)(100 * TotalHitted / TotalShotted)}%", TotalHitted == 0 ? "-" : $"{(int)(100 * TotalWeakspotHitted / TotalHitted)}%", $"{TotalWeakspotHitted}", $"{TotalHitted}", $"{TotalShotted}")}";
            }
        }

        public string GetAccuracyText(InventorySlot slot)
        {
            if (!Owner.HasCharacterSlot || !AccuracySlotDataLookup.TryGetValue(slot, out var data))
            {
                return string.Format(Settings.DisplayFormatOnEndScreen, $"<{Settings.FontColors.HittedRatioColor.ToHexString()}>-%</color>", $"<{Settings.FontColors.WeakspotHittedRatioColor.ToHexString()}>-%</color>", $"<{Settings.FontColors.WeakspotHittedColor.ToHexString()}>0</color>", $"<{Settings.FontColors.HittedColor.ToHexString()}>0</color>", $"<{Settings.FontColors.ShottedColor.ToHexString()}>0</color>");
            }
            string prefix = IsAccuracyListener(Owner.Lookup) || (IsMasterHasAcc && Owner.IsBot) || Owner.IsLocal ? "" : "*";
            if (data.m_Shotted == 0)
            {
                var result = $"{prefix}{string.Format(Settings.DisplayFormatOnEndScreen, $"<{Settings.FontColors.HittedRatioColor.ToHexString()}>-%</color>", $"<{Settings.FontColors.WeakspotHittedRatioColor.ToHexString()}>-%</color>", $"<{Settings.FontColors.WeakspotHittedColor.ToHexString()}>0</color>", $"<{Settings.FontColors.HittedColor.ToHexString()}>0</color>", $"<{Settings.FontColors.ShottedColor.ToHexString()}>0</color>")}";
                return Settings.FontColors.EnableColorOnEndScreen ? result : $"{prefix}{string.Format(Settings.DisplayFormatOnEndScreen, "-%", "-%", 0, 0, 0)}";
            }
            else
            {
                var result = $"{prefix}{string.Format(Settings.DisplayFormatOnEndScreen, $"<{Settings.FontColors.HittedRatioColor.ToHexString()}>{(int)(100 * data.m_Hitted / data.m_Shotted)}%</color>", data.m_Hitted == 0 ? "-" : $"<{Settings.FontColors.WeakspotHittedRatioColor.ToHexString()}>{(int)(100 * data.m_WeakspotHitted / data.m_Hitted)}%</color>", $"<{Settings.FontColors.WeakspotHittedColor.ToHexString()}>{data.m_WeakspotHitted}</color>", $"<{Settings.FontColors.HittedColor.ToHexString()}>{data.m_Hitted}</color>", $"<{Settings.FontColors.ShottedColor.ToHexString()}>{data.m_Shotted}</color>")}";
                return Settings.FontColors.EnableColorOnEndScreen ? result : $"{prefix}{string.Format(Settings.DisplayFormatOnEndScreen, $"{(int)(100 * data.m_Hitted / data.m_Shotted)}%", data.m_Hitted == 0 ? "-" : $"{(int)(100 * data.m_WeakspotHitted / data.m_Hitted)}%", data.m_WeakspotHitted, data.m_Hitted, data.m_Shotted)}"; ;
            }
        }

        public pAccuracyData GetAccuracyData()
        {
            return new(Owner, AccuracySlotDataLookup);
        }

        internal class AccuracySlotData
        {
            public AccuracySlotData(InventorySlot slot)
            {
                m_Slot = slot;
            }

            internal void Set(pAccuracySlotData data)
            {
                m_Hitted = data.Hitted;
                m_Shotted = data.Shotted;
                m_WeakspotHitted = data.WeakspotHitted;
                m_Slot = data.Slot;
            }

            public void DoClear()
            {
                m_Hitted = 0;
                m_Shotted = 0;
                m_WeakspotHitted = 0;
            }

            public uint m_Hitted = 0;
            public uint m_Shotted = 0;
            public uint m_WeakspotHitted = 0;
            public InventorySlot m_Slot = InventorySlot.None;
        }
    }
}