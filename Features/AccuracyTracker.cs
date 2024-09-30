using Agents;
using CellMenu;
using Gear;
using Hikaria.AccuracyTracker.Handlers;
using Hikaria.AccuracyTracker.Managers;
using Player;
using SNetwork;
using System.Linq;
using TheArchive.Core.Attributes;
using TheArchive.Core.Attributes.Feature.Settings;
using TheArchive.Core.FeaturesAPI;
using TheArchive.Core.Models;
using TheArchive.Loader;
using UnityEngine;
using UnityEngine.UI;

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
        [FSDescription("若禁用则使用玩家名称")]
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
        if (state == (int)eGameStateName.Lobby || state == (int)eGameStateName.AfterLevel)
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
    // 用于解决延迟问题导致的开火次数错误，非完美解决方法，针对主机时其他玩家
    [ArchivePatch(typeof(PlayerInventorySynced), nameof(PlayerInventorySynced.GetSync))]
    private class PlayerInventorySynced__GetSync__Patch
    {
        private static void Prefix(PlayerInventorySynced __instance)
        {
            if (__instance.Owner == null)
                return;
            var player = __instance.Owner.Owner;
            if (!SNet.IsMaster || player.IsBot || player.IsLocal)
                return;
            var lookup = player.Lookup;
            if (AccuracyManager.IsAccuracyListener(lookup))
                return;
            var wieldSlot = __instance.Owner.Inventory.WieldedSlot;
            if (wieldSlot == InventorySlot.GearClass)
                return;
            uint count = (uint)__instance.Owner.Sync.FireCountSync;
            if (AccuracyUpdater.ShotsBuffer.TryGetValue(lookup, out var shots))
            {
                count += shots;
                AccuracyUpdater.ShotsBuffer[lookup] = 0;
            }
            if (__instance.WieldedItem != null)
            {
                if (wieldSlot == InventorySlot.GearStandard || wieldSlot == InventorySlot.GearSpecial)
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
                else
                {
                    AccuracyUpdater.ShotsBuffer[lookup] = count;
                    return;
                }
            }
            AccuracyUpdater.AddShotted(lookup, wieldSlot, count);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(lookup);
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
            if (!data.source.TryGet(out var agent))
                return;
            var playerAgent = agent.TryCast<PlayerAgent>();
            if (playerAgent == null)
                return;
            var player = playerAgent.Owner;
            var lookup = player.Lookup;
            if (AccuracyManager.IsAccuracyListener(lookup) || player.IsLocal || player.IsBot)
                return;
            var slot = playerAgent.Inventory.WieldedSlot;
            if (slot != InventorySlot.GearStandard && slot != InventorySlot.GearSpecial)
            {
                Logs.LogError("Not wielding BulletWeapon but ReceiveBulletDamage?");
                return;
            }
            if (data.limbID >= 0 && __instance.DamageLimbs[data.limbID].m_type == eLimbDamageType.Weakspot)
                AccuracyUpdater.AddWeakspotHitted(lookup, slot, 1);
            AccuracyUpdater.AddHitted(lookup, slot, 1);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(lookup);
        }
    }
    #endregion

    #region FetchBotsFireWhenHost

    private static uint BulletPiercingLimit;
    private static uint BulletsPerFire;
    private static bool IsPiercingBullet;
    private static bool IsInWeaponFire;
    private static bool CanCalc;

    [ArchivePatch(typeof(BulletWeaponSynced), nameof(BulletWeaponSynced.Fire))]
    private class BulletWeaponSynced__Fire__Patch
    {
        private static void Prefix(BulletWeaponSynced __instance)
        {
            if (!__instance.Owner.Owner.IsBot || !SNet.IsMaster)
                return;
            CanCalc = true;
            IsInWeaponFire = true;
            IsPiercingBullet = false;
            BulletPiercingLimit = 0;
            BulletsPerFire = 1;
            var data = __instance.ArchetypeData;
            if (data != null)
            {
                IsPiercingBullet = data.PiercingBullets;
                BulletPiercingLimit = data.PiercingBullets ? (uint)data.PiercingDamageCountLimit - 1 : 0;
            }
        }
        private static void Postfix(BulletWeapon __instance)
        {
            if (!__instance.Owner.Owner.IsBot || !SNet.IsMaster)
                return;
            CanCalc = false;
            IsInWeaponFire = false;
            var lookup = __instance.Owner.Owner.Lookup;
            uint hitCount = 0;
            uint weakspotHitCount = 0;
            foreach (var data in BulletHitDataLookup.Values)
            {
                if (data.IsHit)
                    hitCount++;
                if (data.IsWeakspotHit)
                    weakspotHitCount++;
            }
            AccuracyUpdater.AddShotted(lookup, __instance.ItemDataBlock.inventorySlot, BulletsPerFire);
            AccuracyUpdater.AddHitted(lookup, __instance.ItemDataBlock.inventorySlot, hitCount);
            AccuracyUpdater.AddWeakspotHitted(lookup, __instance.ItemDataBlock.inventorySlot, weakspotHitCount);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(lookup);
            CurrentBulletIndex = 0;
        }
    }

    [ArchivePatch(typeof(ShotgunSynced), nameof(ShotgunSynced.Fire))]
    private class ShotgunSynced__Fire__Patch
    {
        private static void Prefix(Shotgun __instance)
        {
            if (!__instance.Owner.Owner.IsBot || !SNet.IsMaster)
                return;
            CanCalc = true;
            IsInWeaponFire = true;
            IsPiercingBullet = false;
            BulletPiercingLimit = 0;
            BulletsPerFire = 1;
            var data = __instance.ArchetypeData;
            if (data != null)
            {
                IsPiercingBullet = data.PiercingBullets;
                BulletPiercingLimit = data.PiercingBullets ? (uint)data.PiercingDamageCountLimit - 1 : 0;
                BulletsPerFire = (uint)data.ShotgunBulletCount;
            }
        }
        private static void Postfix(BulletWeapon __instance)
        {
            if (!__instance.Owner.Owner.IsBot || !SNet.IsMaster)
                return;
            CanCalc = false;
            IsInWeaponFire = false;
            var lookup = __instance.Owner.Owner.Lookup;
            uint hitCount = 0;
            uint weakspotHitCount = 0;
            foreach (var data in BulletHitDataLookup.Values)
            {
                if (data.IsHit)
                    hitCount++;
                if (data.IsWeakspotHit)
                    weakspotHitCount++;
            }
            AccuracyUpdater.AddShotted(lookup, __instance.ItemDataBlock.inventorySlot, BulletsPerFire);
            AccuracyUpdater.AddHitted(lookup, __instance.ItemDataBlock.inventorySlot, hitCount);
            AccuracyUpdater.AddWeakspotHitted(lookup, __instance.ItemDataBlock.inventorySlot, weakspotHitCount);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(lookup);
            CurrentBulletIndex = 0;
        }
    }
    #endregion

    #region FetchLocalFire
    private static bool IsWeaponOwner(BulletWeapon weapon) => weapon?.Owner?.IsLocallyOwned ?? false;

    [ArchivePatch(typeof(BulletWeapon), nameof(BulletWeapon.Fire))]
    private class BulletWeapon__Fire__Patch
    {
        private static void Prefix(BulletWeapon __instance)
        {
            if (!IsWeaponOwner(__instance))
                return; 
            CanCalc = true;
            IsInWeaponFire = true;
            IsPiercingBullet = false;
            BulletPiercingLimit = 0;
            BulletsPerFire = 1;
            var data = __instance.ArchetypeData;
            if (data != null)
            {
                IsPiercingBullet = data.PiercingBullets;
                BulletPiercingLimit = data.PiercingBullets ? (uint)data.PiercingDamageCountLimit - 1 : 0;
            }
        }

        private static void Postfix(BulletWeapon __instance)
        {
            if (!IsWeaponOwner(__instance))
                return;
            CanCalc = false;
            IsInWeaponFire = false;
            var lookup = __instance.Owner.Owner.Lookup;
            uint hitCount = 0;
            uint weakspotHitCount = 0;
            foreach (var data in BulletHitDataLookup.Values)
            {
                if (data.IsHit)
                    hitCount++;
                if (data.IsWeakspotHit)
                    weakspotHitCount++;
            }
            AccuracyUpdater.AddShotted(lookup, __instance.ItemDataBlock.inventorySlot, BulletsPerFire);
            AccuracyUpdater.AddHitted(lookup, __instance.ItemDataBlock.inventorySlot, hitCount);
            AccuracyUpdater.AddWeakspotHitted(lookup, __instance.ItemDataBlock.inventorySlot, weakspotHitCount);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(lookup);
            CurrentBulletIndex = 0;
            BulletHitDataLookup.Clear();
        }
    }

    static bool IsShotgunFireShots;
    [ArchivePatch(typeof(Shotgun), nameof(Shotgun.Fire))]
    private class Shotgun__Fire__Patch
    {
        private static void Prefix(Shotgun __instance)
        {
            if (!IsWeaponOwner(__instance))
                return;
            CanCalc = true;
            IsInWeaponFire = true;
            IsPiercingBullet = false;
            BulletPiercingLimit = 0;
            BulletsPerFire = 1;
            var data = __instance.ArchetypeData;
            if (data != null)
            {
                IsPiercingBullet = data.PiercingBullets;
                BulletPiercingLimit = data.PiercingBullets ? (uint)data.PiercingDamageCountLimit - 1 : 0;
                BulletsPerFire = (uint)data.ShotgunBulletCount;
            }
            IsShotgunFireShots = true;
        }

        private static void Postfix(Shotgun __instance)
        {
            if (!IsWeaponOwner(__instance))
                return;
            CanCalc = false;
            IsInWeaponFire = false;
            var lookup = __instance.Owner.Owner.Lookup;
            uint hitCount = 0;
            uint weakspotHitCount = 0;
            foreach (var data in BulletHitDataLookup.Values)
            {
                if (data.IsHit)
                    hitCount++;
                if (data.IsWeakspotHit)
                    weakspotHitCount++;
            }
            AccuracyUpdater.AddShotted(lookup, __instance.ItemDataBlock.inventorySlot, BulletsPerFire);
            AccuracyUpdater.AddHitted(lookup, __instance.ItemDataBlock.inventorySlot, hitCount);
            AccuracyUpdater.AddWeakspotHitted(lookup, __instance.ItemDataBlock.inventorySlot, weakspotHitCount);
            AccuracyUpdater.MarkAccuracyDataNeedUpdate(lookup);
            IsShotgunFireShots = false;
            CurrentBulletIndex = 0;
            BulletHitDataLookup.Clear();
        }
    }

    //只用于获取是否命中弱点
    [ArchivePatch(typeof(Dam_EnemyDamageLimb), nameof(Dam_EnemyDamageLimb.BulletDamage))]
    private class Dam_EnemyDamageLimb__BulletDamage__Patch
    {
        private static void Postfix(Dam_EnemyDamageLimb __instance, Agent sourceAgent)
        {
            if (!IsInWeaponFire || IsSentryGunFire || !CanCalc || sourceAgent == null)
                return;

            if (CurrentBulletHitData == null)
                return;

            var playerAgent = sourceAgent.TryCast<PlayerAgent>();
            if (!playerAgent?.IsLocallyOwned ?? true)
                return;

            if (__instance.m_type == eLimbDamageType.Weakspot && !CurrentBulletHitData.IsWeakspotHit)
                CurrentBulletHitData.IsWeakspotHit = true;
        }
    }

    #endregion

    #region HandleFire

    private class BulletHitData
    {
        public uint Index;
        public uint BulletHitCount;
        public bool IsHit;
        public bool IsWeakspotHit;
        public bool IsDead;
    }
    static uint CurrentBulletIndex = 0;
    [ArchivePatch(typeof(global::Weapon), nameof(global::Weapon.CastWeaponRay))]
    private class Weapon__CastWeaponRay__Patch
    {
        public static Type[] ParameterTypes() => new[]
            {
                typeof(Transform),
                typeof(global::Weapon.WeaponHitData).MakeByRefType(),
                typeof(Vector3),
                typeof(int)
            };

        private static void Postfix(ref Weapon.WeaponHitData weaponRayData, bool __result)
        {
            if (!IsInWeaponFire || IsSentryGunFire || !CanCalc)
                return;
            if (!BulletHitDataLookup.TryGetValue(CurrentBulletIndex, out CurrentBulletHitData))
            {
                CurrentBulletHitData = new()
                {
                    Index = CurrentBulletIndex,
                    BulletHitCount = 0,
                    IsHit = false,
                    IsWeakspotHit = false,
                    IsDead = !IsPiercingBullet
                };
                BulletHitDataLookup.Add(CurrentBulletIndex, CurrentBulletHitData);
            }

            if (!__result)
            {
                CurrentBulletHitData.IsDead = true;
                CurrentBulletIndex++;
            }
        }
    }


    static BulletHitData CurrentBulletHitData;
    static Dictionary<uint, BulletHitData> BulletHitDataLookup = new();

    [ArchivePatch(typeof(BulletWeapon), nameof(BulletWeapon.BulletHit))]
    private class BulletWeapon__BulletHit__Patch
    {
        private static void Postfix(bool __result)
        {
            if (!IsInWeaponFire || IsSentryGunFire || !CanCalc)
                return;

            if (CurrentBulletHitData == null)
                return;

            if (__result)
            {
                if (!CurrentBulletHitData.IsHit)
                    CurrentBulletHitData.IsHit = true;

                if (IsPiercingBullet)
                    CurrentBulletHitData.BulletHitCount++;
            }

            if (!IsPiercingBullet)
            {
                CurrentBulletHitData.IsDead = true;
                CurrentBulletIndex++;
            }
            else
            {
                if (!Weapon.s_weaponRayData.rayHit.collider.gameObject.IsInLayerMask(LayerManager.MASK_BULLETWEAPON_PIERCING_PASS) 
                    || CurrentBulletHitData.BulletHitCount >= BulletPiercingLimit + 1)
                {
                    CurrentBulletHitData.IsDead = true;
                    CurrentBulletIndex++;
                }
            }
        }
    }
    #endregion
}
