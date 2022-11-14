﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Nekoyume.Battle;
using Nekoyume.Game.Controller;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.Item;
using Nekoyume.Model.Mail;
using Nekoyume.State;
using Nekoyume.TableData;
using Nekoyume.UI.Module;
using Nekoyume.UI.Module.Arena.Board;
using Nekoyume.UI.Scroller;
using UnityEngine;
using UnityEngine.UI;

namespace Nekoyume.UI
{
    using UniRx;

    public class ArenaBoard : Widget
    {
#if UNITY_EDITOR
        [SerializeField]
        private bool _useSo;

        [SerializeField]
        private ArenaBoardSO _so;
#endif

        [SerializeField]
        private ArenaBoardBillboard _billboard;

        [SerializeField]
        private ArenaBoardPlayerScroll _playerScroll;

        [SerializeField]
        private GameObject _noJoinedPlayersGameObject;

        [SerializeField]
        private Button _backButton;

        private ArenaSheet.RoundData _roundData;

        private RxProps.ArenaParticipant[] _boundedData;

        protected override void Awake()
        {
            base.Awake();

            InitializeScrolls();

            _backButton.OnClickAsObservable().Subscribe(_ =>
            {
                AudioController.PlayClick();
                Find<ArenaJoin>().Show();
                Close();
            }).AddTo(gameObject);
        }

        public async UniTaskVoid ShowAsync(bool ignoreShowAnimation = false)
        {
            var loading = Find<DataLoadingScreen>();
            loading.Show();
            await UniTask.WaitWhile(() =>
                RxProps.ArenaParticipantsOrderedWithScore.IsUpdating);
            loading.Close();
            Show(
                RxProps.ArenaParticipantsOrderedWithScore.Value,
                ignoreShowAnimation);
        }

        public void Show(
            RxProps.ArenaParticipant[] arenaParticipants,
            bool ignoreShowAnimation = false) =>
            Show(_roundData,
                arenaParticipants,
                ignoreShowAnimation);

        public void Show(
            ArenaSheet.RoundData roundData,
            RxProps.ArenaParticipant[] arenaParticipants,
            bool ignoreShowAnimation = false)
        {
            _roundData = roundData;
            _boundedData = arenaParticipants;
            Find<HeaderMenuStatic>().Show(HeaderMenuStatic.AssetVisibleState.Arena);
            UpdateBillboard();
            UpdateScrolls();

            // NOTE: This code assumes that '_playerScroll.Data' contains local player
            //       If `_playerScroll.Data` does not contains local player, change `2` in the line below to `1`.
            //       Not use `_boundedData` here because there is the case to
            //       use the mock data from `_so`.
            _noJoinedPlayersGameObject.SetActive(_playerScroll.Data.Count < 2);

            base.Show(ignoreShowAnimation);
        }

        private void UpdateBillboard()
        {
#if UNITY_EDITOR
            if (_useSo && _so)
            {
                _billboard.SetData(
                    _so.SeasonText,
                    _so.Rank,
                    _so.WinCount,
                    _so.LoseCount,
                    _so.CP,
                    _so.Rating);
                return;
            }
#endif
            var player = RxProps.PlayersArenaParticipant.Value;
            if (player is null)
            {
                Debug.Log($"{nameof(RxProps.PlayersArenaParticipant)} is null");
                _billboard.SetData();
                return;
            }

            if (player.CurrentArenaInfo is null)
            {
                Debug.Log($"{nameof(player.CurrentArenaInfo)} is null");
                _billboard.SetData();
                return;
            }

            var equipments = player.ItemSlotState.Equipments
                .Select(guid =>
                    player.AvatarState.inventory.Equipments.FirstOrDefault(x => x.ItemId == guid))
                .Where(item => item != null).ToList();

            var costumes = player.ItemSlotState.Costumes
                .Select(guid =>
                    player.AvatarState.inventory.Costumes.FirstOrDefault(x => x.ItemId == guid))
                .Where(item => item != null).ToList();
            var runeOptionSheet = Game.Game.instance.TableSheets.RuneOptionSheet;
            var rune = player.RuneSlotState.GetEquippedRuneOptions(runeOptionSheet);
            var lv = player.AvatarState.level;
            var characterSheet = Game.Game.instance.TableSheets.CharacterSheet;
            var costumeSheet = Game.Game.instance.TableSheets.CostumeStatSheet;
            if (!characterSheet.TryGetValue(player.AvatarState.characterId, out var row))
            {
                return;
            }

            var cp = CPHelper.TotalCP(equipments, costumes, rune, lv, row, costumeSheet);
            _billboard.SetData(
                "season",
                player.Rank,
                player.CurrentArenaInfo.Win,
                player.CurrentArenaInfo.Lose,
                cp,
                player.Score);
        }

        private void InitializeScrolls()
        {
            _playerScroll.OnClickCharacterView.Subscribe(index =>
                {
#if UNITY_EDITOR
                    if (_useSo && _so)
                    {
                        NotificationSystem.Push(
                            MailType.System,
                            "Cannot open when use mock data in editor mode",
                            NotificationCell.NotificationType.Alert);
                        return;
                    }
#endif
                    var data = _boundedData[index];
                    Find<FriendInfoPopup>().ShowAsync(data.AvatarState, BattleType.Arena).Forget();
                })
                .AddTo(gameObject);

            _playerScroll.OnClickChoice.Subscribe(index =>
                {
#if UNITY_EDITOR
                    if (_useSo && _so)
                    {
                        NotificationSystem.Push(
                            MailType.System,
                            "Cannot battle when use mock data in editor mode",
                            NotificationCell.NotificationType.Alert);
                        return;
                    }
#endif
                    var data = _boundedData[index];
                    Close();
                    Find<ArenaBattlePreparation>().Show(
                        _roundData,
                        data.AvatarState);
                })
                .AddTo(gameObject);
        }

        private void UpdateScrolls()
        {
            var (scrollData, playerIndex) =
                GetScrollData();
            _playerScroll.SetData(scrollData, playerIndex);
        }

        private (List<ArenaBoardPlayerItemData> scrollData, int playerIndex)
            GetScrollData()
        {
#if UNITY_EDITOR
            if (_useSo && _so)
            {
                return (_so.ArenaBoardPlayerScrollData, 0);
            }
#endif

            var currentAvatarAddr = States.Instance.CurrentAvatarState.address;
            var characterSheet = Game.Game.instance.TableSheets.CharacterSheet;
            var costumeSheet = Game.Game.instance.TableSheets.CostumeStatSheet;
            var runeOptionSheet = Game.Game.instance.TableSheets.RuneOptionSheet;
            var scrollData =
                _boundedData.Select(e =>
                {
                    var equipments = e.ItemSlotState.Equipments
                        .Select(guid =>
                            e.AvatarState.inventory.Equipments.FirstOrDefault(x => x.ItemId == guid))
                        .Where(item => item != null).ToList();
                    var costumes = e.ItemSlotState.Costumes
                        .Select(guid =>
                            e.AvatarState.inventory.Costumes.FirstOrDefault(x => x.ItemId == guid))
                        .Where(item => item != null).ToList();
                    var rune = e.RuneSlotState.GetEquippedRuneOptions(runeOptionSheet);
                    var lv = e.AvatarState.level;
                    var titleId = costumes.FirstOrDefault(costume =>
                        costume.ItemSubType == ItemSubType.Title && costume.Equipped)?.Id;

                    var portrait = GameConfig.DefaultAvatarArmorId;

                    var armor = equipments.FirstOrDefault(x => x.ItemSubType == ItemSubType.Armor);
                    if (armor != null)
                    {
                        portrait = armor.Id;
                    }

                    var fullCostume = costumes.FirstOrDefault(x => x.ItemSubType == ItemSubType.FullCostume);
                    if (fullCostume != null)
                    {
                        portrait = fullCostume.Id;
                    }

                    if (!characterSheet.TryGetValue(e.AvatarState.characterId, out var row))
                    {
                        throw new SheetRowNotFoundException("CharacterSheet",
                            $"{e.AvatarState.characterId}");
                    }

                    return new ArenaBoardPlayerItemData
                    {
                        name = e.AvatarState.NameWithHash,
                        level = e.AvatarState.level,
                        fullCostumeOrArmorId = portrait,
                        titleId = titleId,
                        cp = CPHelper.TotalCP(equipments, costumes, rune, lv, row, costumeSheet),
                        score = e.Score,
                        rank = e.Rank,
                        expectWinDeltaScore = e.ExpectDeltaScore.win,
                        interactableChoiceButton = !e.AvatarAddr.Equals(currentAvatarAddr),
                    };
                }).ToList();
            for (var i = 0; i < _boundedData.Length; i++)
            {
                var data = _boundedData[i];
                if (data.AvatarAddr.Equals(currentAvatarAddr))
                {
                    return (scrollData, i);
                }
            }

            return (scrollData, 0);
        }
    }
}
