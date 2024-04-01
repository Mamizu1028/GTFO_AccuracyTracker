﻿using Agents;
using CellMenu;
using Gear;
using Hikaria.AccuracyTracker.Handlers;
using Hikaria.AccuracyTracker.Managers;
using Player;
using SNetwork;
using TheArchive.Core.Attributes;
using TheArchive.Core.Attributes.Feature.Settings;
using TheArchive.Core.FeaturesAPI;
using TheArchive.Core.Models;
using TheArchive.Loader;
using UnityEngine;

namespace Hikaria.AccuracyTracker.Features;

[EnableFeatureByDefault]
[DisallowInGameToggle]
public class AccuracyTracker : Feature
{
    public override string Name => "命中率显示";

    public override bool InlineSettingsIntoParentMenu => true;

    #region FeatureSettings
    [FeatureConfig]
    public static AccuracyTrackerSettings Settings { get; set; }

    public class AccuracyTrackerSettings
    {
        [FSDisplayName("启用")]
        public bool Enabled { get => AccuracyUpdater.Enabled; set => AccuracyUpdater.Enabled = value; }

        [FSDisplayName("在结算界面显示")]
        public bool DisplayOnEndScreen { get; set; } = true;

        [FSDisplayName("显示其他玩家的命中率")]
        public bool ShowOtherPlayersAcc { get => AccuracyUpdater.ShowOtherPlayersAcc; set => AccuracyUpdater.ShowOtherPlayersAcc = value; }

        [FSDisplayName("显示机器人玩家的命中率")]
        public bool ShowBotsAcc { get => AccuracyUpdater.ShowBotsAcc; set => AccuracyUpdater.ShowBotsAcc = value; }

        [FSDisplayName("显示格式")]
        [FSDescription("{0}: 玩家名称, {1}: 命中率, {2}: 弱点命中率, {3}: 弱点命中次数, {4}: 命中次数, {5}: 弹丸击发次数")]
        public string DisplayFormatInGame { get => AccuracyUpdater.ShowFormat; set => AccuracyUpdater.ShowFormat = value; }

        [FSDisplayName("结算界面显示格式")]
        [FSDescription("{0}: 命中率, {1}: 弱点命中率, {2}: 弱点命中次数, {3}: 命中次数, {4}: 弹丸击发次数")]
        public string DisplayFormatOnEndScreen { get => AccuracyUpdater.PageExpeditionSuccessShowFormat; set => AccuracyUpdater.PageExpeditionSuccessShowFormat = value; }

        [FSHeader("玩家显示名称设置")]
        [FSDisplayName("使用通用玩家名称")]
        [FSDescription("若此项为 False, 则使用玩家名称")]
        public bool UseGenericName { get => AccuracyUpdater.UseGenericName; set => AccuracyUpdater.UseGenericName = value; }

        [FSInline]
        [FSDisplayName("显示位置设置")]
        public PositionSettings Position { get; set; } = new();

        [FSInline]
        [FSDisplayName("显示颜色设置")]
        public ColorSettings FontColors { get; set; } = new();
    }

    public class PlayerNameEntry
    {
        public PlayerNameEntry(string character, string name)
        {
            Character = character;
            Name = name;
        }

        [FSSeparator]
        [FSDisplayName("人物")]
        [FSReadOnly]
        public string Character { get; set; }
        [FSDisplayName("名称")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (AccuracyUpdater.IsSetup)
                {
                    AccuracyUpdater.MarkAllAccuracyDataNeedUpdate();
                }
                _name = value;
            }
        }

        private string _name;
    }

    public class PositionSettings
    {
        [FSHeader("显示位置设置")]
        [FSDisplayName("横向偏移量")]
        [FSDescription("单位: 像素")]
        public int OffsetX
        {
            get
            {
                return AccuracyUpdater.OffsetX;
            }
            set
            {
                AccuracyUpdater.OffsetX = value;
            }
        }

        [FSDisplayName("纵向偏移量")]
        [FSDescription("单位: 像素")]
        public int OffsetY
        {
            get
            {
                return AccuracyUpdater.OffsetY;
            }
            set
            {
                AccuracyUpdater.OffsetY = value;
            }
        }
    }

    public class ColorSettings
    {
        [FSHeader("显示颜色设置")]
        [FSDisplayName("在游戏内使用颜色")]
        public bool EnableColorInGame { get; set; } = true;
        [FSDisplayName("在结算界面使用颜色")]
        public bool EnableColorOnEndScreen { get; set; } = false;
        [FSDisplayName("命中率颜色")]
        public SColor HittedRatioColor { get; set; } = new(0.7206f, 0.7206f, 0.7206f, 0.3137f);
        [FSDisplayName("命中次数颜色")]
        public SColor HittedColor { get; set; } = new(0.7206f, 0.7206f, 0.7206f, 0.3137f);
        [FSDisplayName("弱点命中率颜色")]
        public SColor WeakspotHittedRatioColor { get; set; } = new(0.7206f, 0.7206f, 0.7206f, 0.3137f);
        [FSDisplayName("弱点命中次数颜色")]
        public SColor WeakspotHittedColor { get; set; } = new(0.7206f, 0.7206f, 0.7206f, 0.3137f);
        [FSDisplayName("弹丸击发次数颜色")]
        public SColor ShottedColor { get; set; } = new(0.7206f, 0.7206f, 0.7206f, 0.3137f);
    }

    #endregion

    #region FeatureMethods
    public override void OnGameStateChanged(int state)
    {
        if (state == (int)eGameStateName.Lobby)
        {
            AccuracyUpdater.DoClear();
        }
    }

    public override void Init()
    {
        LoaderWrapper.ClassInjector.RegisterTypeInIl2Cpp<AccuracyUpdater>();
        AccuracyManager.Setup();
    }
    #endregion

    #region SetupAccurayTracker
    public static bool IsSetup { get; private set; }

    [ArchivePatch(typeof(CM_PageRundown_New), nameof(CM_PageRundown_New.Setup))]
    public class CM_PageRundown_New__Setup__Postfix
    {
        private static void Postfix()
        {
            if (!IsSetup)
            {
                GameObject gameObject = new("AccurayTracker");
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                if (gameObject.GetComponent<AccuracyUpdater>() == null)
                {
                    gameObject.AddComponent<AccuracyUpdater>();
                }
                IsSetup = true;
            }
        }
    }

    [ArchivePatch(typeof(CM_PageExpeditionSuccess), nameof(CM_PageExpeditionSuccess.TryGetArchetypeName))]
    private class CM_PageExpeditionSuccess__TryGetArchetypeName__Patch
    {
        private static void Postfix(PlayerBackpack backpack, InventorySlot slot, ref string name)
        {
            if (slot != InventorySlot.GearStandard && slot != InventorySlot.GearSpecial || !Settings.DisplayOnEndScreen)
                return;
            if (AccuracyUpdater.TryGetPlayerAccuracyData(backpack.Owner, out var data))
            {
                name += $" | {data.GetAccuracyText(slot)}";
            }
        }
    }
    #endregion

    #region HookOnSessionMemberChanged
    [ArchivePatch(typeof(SNet_GlobalManager), nameof(SNet_GlobalManager.Setup))]
    private class SNet_GlobalManager__Setup__Patch
    {
        private static void Postfix()
        {
            SNet_Events.OnMasterChanged += new Action(OnMasterChanged);
            SNet_Events.OnPlayerEvent += new Action<SNet_Player, SNet_PlayerEvent, SNet_PlayerEventReason>(OnPlayerEvent);
        }
    }

    private static void OnPlayerEvent(SNet_Player player, SNet_PlayerEvent playerEvent, SNet_PlayerEventReason reason)
    {
        AccuracyManager.BroadcastAccuracyDataListener();
        switch (playerEvent)
        {
            case SNet_PlayerEvent.PlayerLeftSessionHub:
            case SNet_PlayerEvent.PlayerAgentDeSpawned:
                AccuracyManager.OnSessionMemberChanged(player, SessionMemberEvent.LeftSessionHub);
                break;
            case SNet_PlayerEvent.PlayerAgentSpawned:
                AccuracyManager.OnSessionMemberChanged(player, SessionMemberEvent.JoinSessionHub);
                break;
        }
    }

    private static void OnMasterChanged()
    {
        AccuracyUpdater.CheckAndSetVisible();
    }

    public enum SessionMemberEvent
    {
        JoinSessionHub,
        LeftSessionHub
    }
    #endregion

    #region FetchSentryFire

    // 只有主机才需要获取炮台是否开火
    public static bool IsSentryGunFire { get; private set; }

    [ArchivePatch(typeof(SentryGunInstance_Firing_Bullets), nameof(SentryGunInstance_Firing_Bullets.FireBullet))]
    private class SentryGunInstance_Firing_Bullets__FireBullet__Patch
    {
        private static void Prefix()
        {
            IsSentryGunFire = true;
        }

        private static void Postfix()
        {
            IsSentryGunFire = false;
        }
    }

    [ArchivePatch(typeof(SentryGunInstance_Firing_Bullets), nameof(SentryGunInstance_Firing_Bullets.UpdateFireShotgunSemi))]
    private class SentryGunInstance_Firing_Bullets__UpdateFireShotgunSemi__Patch
    {
        private static void Prefix()
        {
            IsSentryGunFire = true;
        }

        private static void Postfix()
        {
            IsSentryGunFire = false;
        }
    }

    #endregion

    #region FetchOtherPlayersFire
    private static Dictionary<ulong, InventorySlot> LastWieldValidSlot { get; set; } = new();

    [ArchivePatch(typeof(PlayerSync), nameof(PlayerSync.SyncInventoryStatus))]
    private class PlayerSync__SyncInventoryStatus__Patch
    {
        private static void Prefix(PlayerSync __instance)
        {
            var player = __instance.m_agent.Owner;
            if (player.IsLocal || !SNet.IsMaster || player.IsBot)
            {
                return;
            }
            if (AccuracyManager.IsAccuracyListener(player.Lookup))
            {
                return;
            }
            var slot = __instance.GetWieldedSlot();
            if (slot == InventorySlot.GearStandard || slot == InventorySlot.GearSpecial)
            {
                LastWieldValidSlot[player.Lookup] = slot;
            }
        }
    }

    // 用于解决延迟问题导致的开火次数错误，非完美解决方法，针对主机时其他玩家
    [ArchivePatch(typeof(PlayerInventorySynced), nameof(PlayerInventorySynced.GetSync))]
    private class PlayerInventorySynced__GetSync__Patch
    {
        private static void Prefix(PlayerInventorySynced __instance)
        {
            if (__instance.Owner == null)
            {
                return;
            }
            var player = __instance.Owner.Owner;
            if (!SNet.IsMaster || player.IsBot || player.IsLocal)
            {
                return;
            }
            if (AccuracyManager.IsAccuracyListener(player.Lookup))
            {
                return;
            }
            var wieldSlot = __instance.Owner.Inventory.WieldedSlot;
            if (wieldSlot == InventorySlot.GearClass)
            {
                return;
            }
            uint count = (uint)__instance.Owner.Sync.FireCountSync;
            if (__instance.WieldedItem != null && (wieldSlot == InventorySlot.GearStandard || wieldSlot == InventorySlot.GearSpecial))
            {
                var bulletWeapon = __instance.WieldedItem.TryCast<BulletWeaponSynced>();
                if (bulletWeapon != null)
                {
                    var shotGun = bulletWeapon.TryCast<ShotgunSynced>();
                    if (shotGun != null && shotGun.ArchetypeData != null)
                    {
                        count *= (uint)shotGun.ArchetypeData.ShotgunBulletCount;
                    }
                }
            }
            else if (LastWieldValidSlot.TryGetValue(player.Lookup, out wieldSlot))
            {
                if (!PlayerBackpackManager.TryGetBackpack(player, out var backpack) || !backpack.TryGetBackpackItem(wieldSlot, out var backpackItem))
                {
                    return;
                }
                var bulletWeapon = backpackItem.Instance.TryCast<BulletWeaponSynced>();
                if (bulletWeapon != null)
                {
                    var shotGun = bulletWeapon.TryCast<ShotgunSynced>();
                    if (shotGun != null && shotGun.ArchetypeData != null)
                    {
                        count *= (uint)shotGun.ArchetypeData.ShotgunBulletCount;
                    }
                }
                else
                {
                    Logs.LogError("Not BulletWeapon but fire bullets?");
                }
            }
            AccuracyUpdater.AddShotted(player.Lookup, wieldSlot, count);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(player.Lookup);
        }
    }

    // 做主机时使用该方法
    [ArchivePatch(typeof(Dam_EnemyDamageBase), nameof(Dam_EnemyDamageBase.ReceiveBulletDamage))]
    private class Dam_EnemyDamageBase__ReceiveBulletDamage__Patch
    {
        private static void Postfix(Dam_EnemyDamageBase __instance, pBulletDamageData data)
        {
            if (IsSentryGunFire)
                return;
            if (data.source.TryGet(out var agent))
            {
                var playerAgent = agent.TryCast<PlayerAgent>();
                if (playerAgent == null)
                {
                    return;
                }
                var player = playerAgent.Owner;
                if (AccuracyManager.IsAccuracyListener(player.Lookup) || player.IsLocal || player.IsBot)
                {
                    return;
                }
                var slot = playerAgent.Inventory.WieldedSlot;
                if (slot != InventorySlot.GearStandard && slot != InventorySlot.GearSpecial)
                {
                    Logs.LogError("Not wielding BulletWeapon but ReceiveBulletDamage?");
                    return;
                }
                if (__instance.DamageLimbs[data.limbID].m_type == eLimbDamageType.Weakspot)
                {
                    AccuracyUpdater.AddWeakspotHitted(player.Lookup, slot, 1);
                }
                AccuracyUpdater.AddHitted(player.Lookup, slot, 1);
                AccuracyUpdater.MarkAccuracyDataNeedUpdate(player.Lookup);
            }
        }
    }
    #endregion

    #region FetchBotsFireWhenHost
    private static bool IsWeaponOwner(BulletWeapon weapon)
    {
        if (weapon == null || weapon.Owner == null || weapon.Owner.Owner == null)
        {
            return false;
        }
        return weapon.Owner.Owner.IsLocal;
    }

    private static uint HitCount;

    private static uint WeakspotHitCount;

    private static int BulletPiercingLimit;

    private static int BulletsCountPerFire;

    private static int BulletHitCalledCount;

    private static bool IsPiercingBullet;

    private static bool IsInWeaponFire;

    private static bool CanCalc;

    [ArchivePatch(typeof(BulletWeaponSynced), nameof(BulletWeaponSynced.Fire))]
    private class BulletWeaponSynced__Fire__Patch
    {
        private static void Prefix(BulletWeaponSynced __instance)
        {
            if (!__instance.Owner.Owner.IsBot || !SNet.IsMaster)
            {
                return;
            }
            CanCalc = true;
            IsInWeaponFire = true;
            IsPiercingBullet = false;
            BulletPiercingLimit = 1;
            HitCount = 0;
            BulletHitCalledCount = 0;
            BulletsCountPerFire = 1;
            var data = __instance.ArchetypeData;
            if (data != null)
            {
                IsPiercingBullet = data.PiercingBullets;
                BulletPiercingLimit = data.PiercingBullets ? data.PiercingDamageCountLimit : 0;
            }
        }
        private static void Postfix(BulletWeapon __instance)
        {
            if (!__instance.Owner.Owner.IsBot || !SNet.IsMaster)
            {
                return;
            }
            CanCalc = false;
            IsInWeaponFire = false;
            var player = __instance.Owner.Owner;
            AccuracyUpdater.AddShotted(player.Lookup, __instance.ItemDataBlock.inventorySlot, (uint)BulletsCountPerFire);
            AccuracyUpdater.AddHitted(player.Lookup, __instance.ItemDataBlock.inventorySlot, HitCount);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(player.Lookup);
        }
    }

    [ArchivePatch(typeof(ShotgunSynced), nameof(ShotgunSynced.Fire))]
    private class ShotgunSynced__Fire__Patch
    {
        private static void Prefix(Shotgun __instance)
        {
            if (!__instance.Owner.Owner.IsBot || !SNet.IsMaster)
            {
                return;
            }
            CanCalc = true;
            IsInWeaponFire = true;
            IsPiercingBullet = false;
            BulletPiercingLimit = 1;
            HitCount = 0;
            BulletHitCalledCount = 0;
            BulletsCountPerFire = 1;
            var data = __instance.ArchetypeData;
            if (data != null)
            {
                IsPiercingBullet = data.PiercingBullets;
                BulletPiercingLimit = data.PiercingBullets ? data.PiercingDamageCountLimit : 0;
                BulletsCountPerFire = data.ShotgunBulletCount;
            }
        }
        private static void Postfix(BulletWeapon __instance)
        {
            if (!__instance.Owner.Owner.IsBot || !SNet.IsMaster)
            {
                return;
            }
            CanCalc = false;
            IsInWeaponFire = false;
            var player = __instance.Owner.Owner;
            AccuracyUpdater.AddShotted(player.Lookup, __instance.ItemDataBlock.inventorySlot, (uint)BulletsCountPerFire);
            AccuracyUpdater.AddHitted(player.Lookup, __instance.ItemDataBlock.inventorySlot, HitCount);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(player.Lookup);
        }
    }
    #endregion

    #region FetchLocalFire

    [ArchivePatch(typeof(BulletWeapon), nameof(BulletWeapon.Fire))]
    private class BulletWeapon__Fire__Patch
    {
        private static void Prefix(BulletWeapon __instance)
        {
            if (!IsWeaponOwner(__instance))
            {
                return;
            }
            CanCalc = true;
            IsInWeaponFire = true;
            IsPiercingBullet = false;
            BulletPiercingLimit = 1;
            HitCount = 0;
            WeakspotHitCount = 0;
            BulletHitCalledCount = 0;
            BulletsCountPerFire = 1;
            var data = __instance.ArchetypeData;
            if (data != null)
            {
                IsPiercingBullet = data.PiercingBullets;
                BulletPiercingLimit = data.PiercingBullets ? data.PiercingDamageCountLimit : 0;
            }
        }

        private static void Postfix(BulletWeapon __instance)
        {
            if (!IsWeaponOwner(__instance))
            {
                return;
            }
            CanCalc = false;
            IsInWeaponFire = false;
            var player = __instance.Owner.Owner;
            AccuracyUpdater.AddShotted(player.Lookup, __instance.ItemDataBlock.inventorySlot, (uint)BulletsCountPerFire);
            AccuracyUpdater.AddHitted(player.Lookup, __instance.ItemDataBlock.inventorySlot, HitCount);
            AccuracyUpdater.AddWeakspotHitted(player.Lookup, __instance.ItemDataBlock.inventorySlot, WeakspotHitCount);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(player.Lookup);
        }
    }

    [ArchivePatch(typeof(Shotgun), nameof(Shotgun.Fire))]
    private class Shotgun__Fire__Patch
    {
        private static void Prefix(Shotgun __instance)
        {
            if (!IsWeaponOwner(__instance))
            {
                return;
            }
            CanCalc = true;
            IsInWeaponFire = true;
            IsPiercingBullet = false;
            BulletPiercingLimit = 1;
            HitCount = 0;
            WeakspotHitCount = 0;
            BulletHitCalledCount = 0;
            BulletsCountPerFire = 1;
            var data = __instance.ArchetypeData;
            if (data != null)
            {
                IsPiercingBullet = data.PiercingBullets;
                BulletPiercingLimit = data.PiercingBullets ? data.PiercingDamageCountLimit : 0;
                BulletsCountPerFire = data.ShotgunBulletCount;
            }
        }

        private static void Postfix(Shotgun __instance)
        {
            if (!IsWeaponOwner(__instance))
            {
                return;
            }
            CanCalc = false;
            IsInWeaponFire = false;
            var player = __instance.Owner.Owner;
            AccuracyUpdater.AddShotted(player.Lookup, __instance.ItemDataBlock.inventorySlot, (uint)BulletsCountPerFire);
            AccuracyUpdater.AddHitted(player.Lookup, __instance.ItemDataBlock.inventorySlot, HitCount);
            AccuracyUpdater.AddWeakspotHitted(player.Lookup, __instance.ItemDataBlock.inventorySlot, WeakspotHitCount);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(player.Lookup);
        }
    }

    [ArchivePatch(typeof(Dam_EnemyDamageLimb), nameof(Dam_EnemyDamageLimb.BulletDamage))]
    private class Dam_EnemyDamageLimb__BulletDamage__Patch
    {
        private static void Postfix(Dam_EnemyDamageLimb __instance, Agent sourceAgent)
        {
            if (!IsInWeaponFire || IsSentryGunFire || !CanCalc || sourceAgent == null)
            {
                return;
            }
            var playerAgent = sourceAgent.TryCast<PlayerAgent>();
            if (playerAgent != null && playerAgent.IsLocallyOwned)
            {
                BulletWeapon__BulletHit__Patch.WeakspotHitted = __instance.m_type == eLimbDamageType.Weakspot;
            }
        }
    }

    #endregion

    #region HandleFire
    [ArchivePatch(typeof(BulletWeapon), nameof(BulletWeapon.BulletHit))]
    private class BulletWeapon__BulletHit__Patch
    {
        public static bool WeakspotHitted;
        private static bool CanCalcHitted;
        private static bool CanCalcWeakspotHitted;
        private static bool PiercingRoundCalced;
        private static void Postfix(bool __result)
        {
            if (!IsInWeaponFire || IsSentryGunFire || !CanCalc)
            {
                return;
            }
            if (BulletHitCalledCount >= BulletsCountPerFire)
            {
                return;
            }
            if (!IsPiercingBullet)
            {
                CanCalcHitted = true;
                CanCalcWeakspotHitted = true;
            }
            else if (BulletHitCalledCount % BulletPiercingLimit == 0)
            {
                CanCalcHitted = true;
                CanCalcWeakspotHitted = true;
            }
            BulletHitCalledCount++;
            if (!__result)
            {
                return;
            }
            if (CanCalcHitted)
            {
                HitCount++;
                CanCalcHitted = false;
            }
            if (CanCalcWeakspotHitted)
            {
                if (WeakspotHitted)
                {
                    WeakspotHitCount++;
                    CanCalcWeakspotHitted = false;
                }
            }
        }
    }
    #endregion
}
