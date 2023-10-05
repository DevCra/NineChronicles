using System;
using System.Collections.Generic;
using System.Linq;
using Nekoyume.Action;
using Nekoyume.Game.VFX;
using Nekoyume.L10n;
using Nekoyume.Model.Item;
using Nekoyume.Model.Mail;
using Nekoyume.Model.Quest;
using Nekoyume.Model.State;
using Nekoyume.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nekoyume.UI.Module
{
    using Nekoyume.Game;
    using Nekoyume.Helper;
    using Nekoyume.UI.Scroller;
    using UniRx;

    public class HeaderMenuStatic : StaticWidget
    {
        public enum ToggleType
        {
            Quest,
            AvatarInfo,
            CombinationSlots,
            Mail,
            Rank,
            Chat,
            Settings,
            Quit,
            PortalReward,
        }

        public enum AssetVisibleState
        {
            Main = 0,
            Combination,
            Shop,
            Battle,
            Arena,
            EventDungeon,
            WorldBoss,
            CurrencyOnly,
            RuneStone,
            Mileage,
            Summon,
        }

        [Serializable]
        private class ToggleInfo
        {
            public ToggleType Type;
            public Toggle Toggle;
            public Image Notification;
            public GameObject Lock;
            public TextMeshProUGUI LockText;
        }

        [SerializeField]
        private List<ToggleInfo> toggles = new List<ToggleInfo>();

        [SerializeField]
        private Gold ncg;

        [SerializeField]
        private ActionPoint actionPoint;

        [SerializeField]
        private Crystal crystal;

        [SerializeField]
        private GameObject dailyBonus;

        [SerializeField]
        private Hourglass hourglass;

        [SerializeField]
        private RuneStone runeStone;

        [SerializeField]
        private ArenaTickets arenaTickets;

        [SerializeField]
        private EventDungeonTickets eventDungeonTickets;

        [SerializeField]
        private WorldBossTickets worldBossTickets;

        [SerializeField]
        private MaterialAsset[] materialAssets;

        [SerializeField]
        private GameObject mileage;

        [SerializeField]
        private VFX inventoryVFX;

        [SerializeField]
        private VFX workshopVFX;

        [SerializeField]
        private Toggle menuToggleDropdown;

        private readonly List<IDisposable> _disposablesAtOnEnable = new List<IDisposable>();

        private readonly Dictionary<ToggleType, Widget> _toggleWidgets =
            new Dictionary<ToggleType, Widget>();

        private readonly Dictionary<ToggleType, ReactiveProperty<bool>> _toggleNotifications =
            new Dictionary<ToggleType, ReactiveProperty<bool>>()
            {
                { ToggleType.Quest, new ReactiveProperty<bool>(false) },
                { ToggleType.AvatarInfo, new ReactiveProperty<bool>(false) },
                { ToggleType.CombinationSlots, new ReactiveProperty<bool>(false) },
                { ToggleType.Mail, new ReactiveProperty<bool>(false) },
                { ToggleType.Rank, new ReactiveProperty<bool>(false) },
                { ToggleType.PortalReward, new ReactiveProperty<bool>(false) },
            };

        private readonly Dictionary<ToggleType, int> _toggleUnlockStages =
            new Dictionary<ToggleType, int>()
            {
                { ToggleType.Quest, GameConfig.RequireClearedStageLevel.UIBottomMenuQuest },
                { ToggleType.AvatarInfo, GameConfig.RequireClearedStageLevel.UIBottomMenuCharacter },
                { ToggleType.CombinationSlots, GameConfig.RequireClearedStageLevel.CombinationEquipmentAction },
                { ToggleType.Mail, GameConfig.RequireClearedStageLevel.UIBottomMenuMail },
                { ToggleType.Rank, 1 },
                { ToggleType.Chat, GameConfig.RequireClearedStageLevel.UIBottomMenuChat },
                { ToggleType.Settings, 1 },
                { ToggleType.Quit, 1 },
            };

        private long _blockIndex;

        public bool ChargingAP => actionPoint.NowCharging;
        public Gold Gold => ncg;
        public ActionPoint ActionPoint => actionPoint;
        public Crystal Crystal => crystal;
        public Hourglass Hourglass => hourglass;
        public RuneStone RuneStone => runeStone;
        public ArenaTickets ArenaTickets => arenaTickets;
        public EventDungeonTickets EventDungeonTickets => eventDungeonTickets;
        public WorldBossTickets WorldBossTickets => worldBossTickets;
        public MaterialAsset[] MaterialAssets => materialAssets;

        public override bool CanHandleInputEvent => false;

        private const string PortalRewardNotificationKey = "PORTAL_REWARD_NOTIFICATION";

        public override void Initialize()
        {
            base.Initialize();

            _toggleWidgets.Add(ToggleType.Quest, Find<QuestPopup>());
            _toggleWidgets.Add(ToggleType.AvatarInfo, Find<AvatarInfoPopup>());
            _toggleWidgets.Add(ToggleType.CombinationSlots, Find<CombinationSlotsPopup>());
            _toggleWidgets.Add(ToggleType.Mail, Find<MailPopup>());
            _toggleWidgets.Add(ToggleType.Rank, Find<RankPopup>());
            _toggleWidgets.Add(ToggleType.Settings, Find<SettingPopup>());
            _toggleWidgets.Add(ToggleType.Chat, Find<ChatPopup>());
            _toggleWidgets.Add(ToggleType.Quit, Find<QuitSystem>());

            foreach (var toggleInfo in toggles)
            {
                if (_toggleNotifications.ContainsKey(toggleInfo.Type))
                {
                    _toggleNotifications[toggleInfo.Type].SubscribeTo(toggleInfo.Notification)
                        .AddTo(gameObject);
                }

                toggleInfo.Toggle.onValueChanged.AddListener((value) =>
                {
                    var widget = _toggleWidgets[toggleInfo.Type];
                    if (value)
                    {
                        var requiredStage = _toggleUnlockStages[toggleInfo.Type];
                        if (!States.Instance.CurrentAvatarState.worldInformation.IsStageCleared(requiredStage))
                        {
                            OneLineSystem.Push(MailType.System,
                                L10nManager.Localize("UI_STAGE_LOCK_FORMAT", requiredStage),
                                NotificationCell.NotificationType.UnlockCondition);
                            toggleInfo.Toggle.isOn = false;
                            return;
                        }

                        var stage = Game.instance.Stage;
                        if (!Game.instance.IsInWorld || stage.SelectedPlayer.IsAlive)
                        {
                            widget.Show(() => { toggleInfo.Toggle.isOn = false; });
                        }
                    }
                    else
                    {
                        if (widget.isActiveAndEnabled)
                        {
                            widget.Close(true);
                        }
                    }
                });
            }

            menuToggleDropdown.onValueChanged.AddListener((value) =>
            {
                if (value)
                {
                    CloseWidget = () => { menuToggleDropdown.isOn = false; };
                    WidgetStack.Push(gameObject);
                    Animator.Play("HamburgerMenu@Show");
                }
                else
                {
                    Animator.Play("HamburgerMenu@Close");
                    CloseWidget = null;
                    Observable.NextFrame().Subscribe(_ =>
                    {
                        var list = WidgetStack.ToList();
                        list.Remove(gameObject);
                        WidgetStack.Clear();
                        foreach (var go in list)
                        {
                            WidgetStack.Push(go);
                        }
                    });
                }

                foreach (var toggleInfo in toggles)
                {
                    if (!value || !toggleInfo.Lock || !toggleInfo.LockText)
                    {
                        continue;
                    }

                    var requiredStage = _toggleUnlockStages[toggleInfo.Type];
                    var isLock = !States.Instance.CurrentAvatarState.worldInformation.IsStageCleared(requiredStage);
                    toggleInfo.Lock.SetActive(isLock);
                    toggleInfo.LockText.text = L10nManager.Localize("UI_STAGE") + requiredStage;
                }
            });

            Event.OnRoomEnter.AddListener(_ => UpdateAssets(AssetVisibleState.Main));
            Game.instance.Agent.BlockIndexSubject
                .ObserveOnMainThread()
                .Subscribe(SubscribeBlockIndex)
                .AddTo(gameObject);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _disposablesAtOnEnable.DisposeAllAndClear();
            ReactiveAvatarState.QuestList?.Subscribe(SubscribeAvatarQuestList)
                .AddTo(_disposablesAtOnEnable);
            LocalMailHelper.Instance.ObservableMailBox.Subscribe(SubscribeAvatarMailBox)
                .AddTo(_disposablesAtOnEnable);
            ReactiveAvatarState.Inventory?.Subscribe(SubscribeInventory)
                .AddTo(_disposablesAtOnEnable);
        }

        protected override void OnDisable()
        {
            _disposablesAtOnEnable.DisposeAllAndClear();
            base.OnDisable();
        }

        public void Show(AssetVisibleState assetVisibleState, bool ignoreShowAnimation = false)
        {
            UpdateAssets(assetVisibleState);
            Show(ignoreShowAnimation);
        }

        public override void Close(bool ignoreCloseAnimation = false)
        {
            foreach (var toggleInfo in toggles)
            {
                toggleInfo.Toggle.isOn = false;
            }

            menuToggleDropdown.isOn = false;
            base.Close(ignoreCloseAnimation);
        }

        public void PlayVFX(ItemMoveAnimation.EndPoint endPoint)
        {
            switch (endPoint)
            {
                case ItemMoveAnimation.EndPoint.Inventory:
                    inventoryVFX.Play();
                    break;
                case ItemMoveAnimation.EndPoint.Workshop:
                    workshopVFX.Play();
                    break;
            }
        }

        public Transform GetToggle(ToggleType toggleType)
        {
            var info = toggles.FirstOrDefault(x => x.Type.Equals(toggleType));
            var toggleTransform = info?.Toggle.transform;
            return toggleTransform ? toggleTransform : null;
        }

        public void UpdateAssets(AssetVisibleState state)
        {
            switch (state)
            {
                case AssetVisibleState.Main:
                    SetActiveAssets(isNcgActive: true, isActionPointActive: true, isDailyBonusActive: true);
                    break;
                case AssetVisibleState.Combination:
                    SetActiveAssets(isNcgActive: true, isActionPointActive: true, isHourglassActive: true);
                    break;
                case AssetVisibleState.Shop:
                case AssetVisibleState.Battle:
                    SetActiveAssets(isNcgActive: true, isActionPointActive: true);
                    break;
                case AssetVisibleState.Arena:
                    SetActiveAssets(isNcgActive: true, isActionPointActive: true, isArenaTicketsActive: true);
                    break;
                case AssetVisibleState.EventDungeon:
                    SetActiveAssets(isNcgActive: true, isActionPointActive: true, isEventDungeonTicketsActive: true);
                    break;
                case AssetVisibleState.WorldBoss:
                    SetActiveAssets(isNcgActive: true, isEventWorldBossTicketsActive: true);
                    break;
                case AssetVisibleState.CurrencyOnly:
                    SetActiveAssets(isNcgActive:true);
                    break;
                case AssetVisibleState.RuneStone:
                    SetActiveAssets(isNcgActive:true, isRuneStoneActive:true );
                    break;
                case AssetVisibleState.Mileage:
                    SetActiveAssets(isNcgActive:true, isMileageActive:true);
                    break;
                case AssetVisibleState.Summon:
                    SetActiveAssets(isNcgActive:true, isMaterialActiveCount: Summon.SummonGroup);
                    break;
            }
        }

        private void SetActiveAssets(
            bool isNcgActive = false,
            bool isActionPointActive = false,
            bool isDailyBonusActive = false,
            bool isHourglassActive = false,
            bool isArenaTicketsActive = false,
            bool isEventDungeonTicketsActive = false,
            bool isEventWorldBossTicketsActive = false,
            bool isRuneStoneActive = false,
            bool isMileageActive = false,
            int isMaterialActiveCount = 0)
        {
            ncg.gameObject.SetActive(isNcgActive);
            crystal.gameObject.SetActive(isNcgActive && !isMileageActive);
            actionPoint.gameObject.SetActive(isActionPointActive);
            dailyBonus.SetActive(isDailyBonusActive);
            hourglass.gameObject.SetActive(isHourglassActive);
            arenaTickets.gameObject.SetActive(isArenaTicketsActive);
            eventDungeonTickets.gameObject.SetActive(isEventDungeonTicketsActive);
            worldBossTickets.gameObject.SetActive(isEventWorldBossTicketsActive);
            runeStone.gameObject.SetActive(isRuneStoneActive);
            mileage.gameObject.SetActive(isMileageActive);
            for (var i = 0; i < materialAssets.Length; i++)
            {
                materialAssets[i].gameObject.SetActive(i < isMaterialActiveCount);
            }
        }

        private void SubscribeBlockIndex(long blockIndex)
        {
            _blockIndex = blockIndex;
            UpdateCombinationNotification(blockIndex);

            var mailBox = Find<MailPopup>().MailBox;
            if (mailBox is null)
            {
                return;
            }

            _toggleNotifications[ToggleType.Mail].Value =
                mailBox.Any(i => i.New && i.requiredBlockIndex <= blockIndex);
        }

        private void SubscribeAvatarMailBox(MailBox mailBox)
        {
            if (mailBox is null)
            {
                Debug.LogWarning($"{nameof(mailBox)} is null.");
                return;
            }

            _toggleNotifications[ToggleType.Mail].Value =
                mailBox.Any(i => i.New && i.requiredBlockIndex <= _blockIndex);
        }

        private void SubscribeAvatarQuestList(QuestList questList)
        {
            if (questList is null)
            {
                Debug.LogWarning($"{nameof(questList)} is null.");
                return;
            }

            var hasNotification =
                questList.Any(quest => quest.IsPaidInAction && quest.isReceivable);
            _toggleNotifications[ToggleType.Quest].Value = hasNotification;
            Find<QuestPopup>().SetList(questList);
        }

        private void SubscribeInventory(Nekoyume.Model.Item.Inventory inventory)
        {
            var blockIndex = Game.instance.Agent.BlockIndex;
            var avatarLevel = States.Instance.CurrentAvatarState?.level ?? 0;
            var sheets = Game.instance.TableSheets;
            var hasNotification = inventory?.HasNotification(avatarLevel, blockIndex,
                sheets.ItemRequirementSheet,
                sheets.EquipmentItemRecipeSheet,
                sheets.EquipmentItemSubRecipeSheetV2,
                sheets.EquipmentItemOptionSheet) ?? false;
            UpdateInventoryNotification(hasNotification);
        }

        private void UpdateCombinationNotification(long currentBlockIndex)
        {
            var avatarState = States.Instance.CurrentAvatarState;
            if (avatarState is null)
            {
                return;
            }

            var states = States.Instance.GetCombinationSlotState(avatarState, currentBlockIndex);
            var hasNotification = states?.Any(state =>
                HasCombinationNotification(state.Value, currentBlockIndex)) ?? false;
            _toggleNotifications[ToggleType.CombinationSlots].Value = hasNotification;
        }

        private bool HasCombinationNotification(CombinationSlotState state, long currentBlockIndex)
        {
            if (state?.Result is null)
            {
                return false;
            }

            var isAppraise = currentBlockIndex < state.StartBlockIndex +
                States.Instance.GameConfigState.RequiredAppraiseBlock;
            if (isAppraise)
            {
                return false;
            }

            var gameConfigState = Game.instance.States.GameConfigState;
            var diff = state.RequiredBlockIndex - currentBlockIndex;
            int cost;
            if (state.PetId.HasValue &&
                States.Instance.PetStates.TryGetPetState(state.PetId.Value, out var petState))
            {
                cost = PetHelper.CalculateDiscountedHourglass(
                    diff,
                    States.Instance.GameConfigState.HourglassPerBlock,
                    petState,
                    TableSheets.Instance.PetOptionSheet);
            }
            else
            {
                cost = RapidCombination0.CalculateHourglassCount(gameConfigState, diff);
            }
            var row = Game.instance.TableSheets.MaterialItemSheet.Values.First(r =>
                r.ItemSubType == ItemSubType.Hourglass);
            var isEnough =
                States.Instance.CurrentAvatarState.inventory.HasFungibleItem(row.ItemId, currentBlockIndex, cost);
            return isEnough;
        }

        public void UpdateInventoryNotification(bool hasNotification)
        {
            _toggleNotifications[ToggleType.AvatarInfo].Value = hasNotification;
        }

        public void UpdateMailNotification(bool hasNotification)
        {
            _toggleNotifications[ToggleType.Mail].Value = hasNotification;
        }

        public void UpdatePortalReward(bool hasNotification)
        {
            _toggleNotifications[ToggleType.PortalReward].Value = hasNotification;
            PlayerPrefs.SetInt(PortalRewardNotificationKey, hasNotification ? 1:0);
        }

        public void SetActiveAvatarInfo(bool value)
        {
            var avatarInfo = toggles.FirstOrDefault(x => x.Type == ToggleType.AvatarInfo);
            avatarInfo?.Toggle.gameObject.SetActive(value);
        }

        public void TutorialActionClickBottomMenuWorkShopButton()
        {
            var info = toggles.FirstOrDefault(x => x.Type.Equals(ToggleType.CombinationSlots));
            if (info != null)
            {
                info.Toggle.isOn = true;
            }
        }

        public void TutorialActionClickBottomMenuMailButton()
        {
            var info = toggles.FirstOrDefault(x => x.Type.Equals(ToggleType.Mail));
            if (info != null)
            {
                info.Toggle.isOn = true;
            }
        }

        public void TutorialActionClickBottomMenuCharacterButton()
        {
            var info = toggles.FirstOrDefault(x => x.Type.Equals(ToggleType.AvatarInfo));
            if (info != null)
            {
                info.Toggle.isOn = true;
            }
        }
    }
}
