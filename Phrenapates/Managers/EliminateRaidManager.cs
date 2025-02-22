﻿using Phrenapates.Services;
using Phrenapates.Utils;
using Plana.Database;
using Plana.FlatData;
using Plana.MX.GameLogic.DBModel;
using Plana.MX.Logic.Battles.Summary;

namespace Phrenapates.Managers
{
    public class EliminateRaidManager : Singleton<EliminateRaidManager>
    {
        public EliminateRaidLobbyInfoDB EliminateRaidLobbyInfoDB { get; private set; }
        public RaidDB RaidDB { get; private set; }
        public RaidBattleDB RaidBattleDB { get; private set; }

        // Track boss data and time.
        public long SeasonId { get; private set; }
        public DateTime OverrideServerTimeTicks { get; private set; }
        public List<long> BossCharacterIds { get; private set; }

        public DateTime CreateServerTime(EliminateRaidSeasonManageExcelT targetSeason, ContentInfo contentInfo)
        {
            if (OverrideServerTimeTicks == default || SeasonId != contentInfo.EliminateRaidDataInfo.SeasonId)
            {
                OverrideServerTimeTicks = DateTime.Parse(targetSeason.SeasonStartData);
                SeasonId = contentInfo.EliminateRaidDataInfo.SeasonId;
            }
            return OverrideServerTimeTicks;
        }
        public DateTime GetServerTime() => OverrideServerTimeTicks;

        public EliminateRaidLobbyInfoDB GetLobby(ContentInfo raidInfo, EliminateRaidSeasonManageExcelT targetSeasonData)
        {
            if (EliminateRaidLobbyInfoDB == null || EliminateRaidLobbyInfoDB.SeasonId != raidInfo.EliminateRaidDataInfo.SeasonId)
            {
                ClearBossData();
                EliminateRaidLobbyInfoDB = new EliminateRaidLobbyInfoDB()
                {
                    Tier = 4,
                    Ranking = 1,
                    SeasonId = raidInfo.EliminateRaidDataInfo.SeasonId,
                    BestRankingPoint = 0,
                    TotalRankingPoint = 0,
                    BestRankingPointPerBossGroup = [],
                    ReceiveRewardIds = targetSeasonData.SeasonRewardId,
                    OpenedBossGroups = [],
                    PlayableHighestDifficulty = new()
                    {
                        { targetSeasonData.OpenRaidBossGroup01, Difficulty.Lunatic },
                        { targetSeasonData.OpenRaidBossGroup02, Difficulty.Lunatic },
                        { targetSeasonData.OpenRaidBossGroup03, Difficulty.Lunatic }
                    },
                    SweepPointByRaidUniqueId = [],
                    SeasonStartDate = OverrideServerTimeTicks.AddHours(-1),
                    SeasonEndDate = OverrideServerTimeTicks.AddDays(7),
                    SettlementEndDate = OverrideServerTimeTicks.AddDays(8),
                    NextSeasonId = 999,
                    NextSeasonStartDate = OverrideServerTimeTicks.AddMonths(1),
                    NextSeasonEndDate = OverrideServerTimeTicks.AddMonths(1).AddDays(7),
                    NextSettlementEndDate = OverrideServerTimeTicks.AddMonths(1).AddDays(8),
                    RemainFailCompensation = new() { 
                        { 0, true },
                        { 1, true },
                        { 2, true },
                    }
                };
            }
            
            else
            {
                EliminateRaidLobbyInfoDB.BestRankingPoint = raidInfo.EliminateRaidDataInfo.BestRankingPoint;
                EliminateRaidLobbyInfoDB.TotalRankingPoint = raidInfo.EliminateRaidDataInfo.TotalRankingPoint;
            }

            return EliminateRaidLobbyInfoDB;
        }

        public RaidDB CreateRaid(
            ContentInfo raidInfo,
            long ownerId, string ownerNickname, int ownerLevel, long characterId,
            bool isPractice, long raidId,
            EliminateRaidStageExcelT currentRaidData, List<CharacterStatExcelT> characterStatExcel
        )
        {
            if (RaidDB == null)
            {
                List<RaidBossDB> raidBossDBs = currentRaidData.BossCharacterId.Select((x, index) => {
                    return new RaidBossDB()
                    {
                        ContentType = ContentType.Raid,
                        BossCurrentHP = characterStatExcel.FirstOrDefault(y => y.CharacterId == x).MaxHP100,
                        BossGroggyPoint = 0,
                        BossIndex = index
                    };
                }).ToList();

                BossCharacterIds ??= currentRaidData.BossCharacterId;
                
                RaidDB = new()
                {
                    Owner = new()
                    {
                        AccountId = ownerId,
                        AccountName = ownerNickname,
                        CharacterId = characterId
                    },
                    ContentType = ContentType.EliminateRaid,
                    RaidState = RaidStatus.Playing,
                    SeasonId = EliminateRaidLobbyInfoDB.SeasonId,
                    UniqueId = raidId,
                    ServerId = 1,
                    SecretCode = "0",
                    Begin = OverrideServerTimeTicks,
                    End = OverrideServerTimeTicks.AddHours(1),
                    PlayerCount = 1,
                    IsPractice = isPractice,
                    AccountLevelWhenCreateDB = ownerLevel,
                    RaidBossDBs = raidBossDBs
                };
            }

            else
            {
                RaidDB.BossDifficulty = raidInfo.EliminateRaidDataInfo.CurrentDifficulty;
                RaidDB.UniqueId = raidId;
                RaidDB.IsPractice = isPractice;
            }

            EliminateRaidLobbyInfoDB.PlayingRaidDB = RaidDB;

            return RaidDB;
        }

        public RaidBattleDB CreateBattle(
            long ownerId, string ownerNickname, long characterId,
            long raidId, long bossHp
        )
        {
            if (RaidBattleDB == null)
            {
                RaidBattleDB = new()
                {
                    ContentType = ContentType.EliminateRaid,
                    RaidUniqueId = raidId,
                    CurrentBossHP = bossHp,
                    RaidMembers = [
                        new() {
                            AccountId = ownerId,
                            AccountName = ownerNickname,
                            CharacterId = characterId
                        }
                    ],
                };
            }

            else
            {
                RaidBattleDB.RaidUniqueId = raidId;
            }

            return RaidBattleDB;
        }

        public bool SaveBattle(
            long keyId, BattleSummary summary,
            EliminateRaidStageExcelT raidStageExcel, List<CharacterStatExcelT> characterStatExcels
        )
        {
            RaidBattleDB.RaidMembers.FirstOrDefault().DamageCollection ??= [];
            var raidMember = RaidBattleDB.RaidMembers.FirstOrDefault();
            foreach (var raidDamage in summary.RaidSummary.RaidBossResults)
            {
                var existingDamageCol = raidMember.DamageCollection.FirstOrDefault(x => x.Index == raidDamage.RaidDamage.Index);

                if (existingDamageCol != null)
                {
                    existingDamageCol.GivenDamage += raidDamage.RaidDamage.GivenDamage;
                    existingDamageCol.GivenGroggyPoint += raidDamage.RaidDamage.GivenGroggyPoint;
                }
                else raidMember.DamageCollection.Add(RaidService.CreateRaidCollection(raidDamage.RaidDamage));
            }


            foreach (var bossResult in summary.RaidSummary.RaidBossResults)
            {
                var characterStat = characterStatExcels.FirstOrDefault(x => x.CharacterId == BossCharacterIds[bossResult.RaidDamage.Index]);

                // Calculate updated HP and Groggy points
                long hpLeft = RaidDB.RaidBossDBs[bossResult.RaidDamage.Index].BossCurrentHP - bossResult.RaidDamage.GivenDamage;
                long givenGroggy = RaidDB.RaidBossDBs[bossResult.RaidDamage.Index].BossGroggyPoint + bossResult.RaidDamage.GivenGroggyPoint;
                long groggyPoint = RaidService.CalculateGroggyAccumulation(givenGroggy, characterStat);
                long bossAIPhase = RaidService.AIPhaseCheck(
                    bossResult.RaidDamage.Index, hpLeft, bossResult.AIPhase,
                    raidStageExcel.GroundDevName, raidStageExcel.Difficulty, raidStageExcel.BossCharacterId,
                    characterStatExcels
                );

                if (hpLeft <= 0)
                {
                    // Boss defeated
                    RaidDB.RaidBossDBs[bossResult.RaidDamage.Index].BossCurrentHP = 0;
                    RaidDB.RaidBossDBs[bossResult.RaidDamage.Index].BossGroggyPoint = groggyPoint;

                    int nextBossIndex = bossResult.RaidDamage.Index + 1;
                    if (nextBossIndex < RaidDB.RaidBossDBs.Count)
                    {
                        // Move to the next boss
                        long nextBossAIPhase = RaidService.AIPhaseCheck(
                            nextBossIndex, hpLeft, bossResult.AIPhase,
                            raidStageExcel.GroundDevName, raidStageExcel.Difficulty, raidStageExcel.BossCharacterId,
                            characterStatExcels
                        );
                        var nextBoss = RaidDB.RaidBossDBs[nextBossIndex];
                        RaidBattleDB.CurrentBossHP = nextBoss.BossCurrentHP;
                        RaidBattleDB.CurrentBossGroggy = 0;
                        RaidBattleDB.CurrentBossAIPhase = nextBossAIPhase;
                        RaidBattleDB.SubPartsHPs = bossResult.SubPartsHPs;
                        RaidBattleDB.RaidBossIndex = nextBossIndex;
                    }
                    else
                    {
                        // Raid complete
                        RaidBattleDB.CurrentBossHP = 0;
                        RaidBattleDB.CurrentBossGroggy = groggyPoint;
                        RaidBattleDB.CurrentBossAIPhase = bossResult.AIPhase;
                        RaidBattleDB.SubPartsHPs = bossResult.SubPartsHPs;
                    }
                }
                else
                {
                    // Boss not defeated
                    RaidDB.RaidBossDBs[bossResult.RaidDamage.Index].BossCurrentHP = hpLeft;
                    RaidDB.RaidBossDBs[bossResult.RaidDamage.Index].BossGroggyPoint = groggyPoint;

                    RaidBattleDB.CurrentBossHP = hpLeft;
                    RaidBattleDB.CurrentBossGroggy = groggyPoint;
                    RaidBattleDB.CurrentBossAIPhase = bossAIPhase;
                    RaidBattleDB.SubPartsHPs = bossResult.SubPartsHPs;
                }
            }
            EliminateRaidLobbyInfoDB.PlayingRaidDB.RaidBossDBs = RaidDB.RaidBossDBs;

            // Disabled for now until futher update on assistant character
            /*List<long> characterId = RaidService.CharacterParticipation(summary.Group01Summary);
            if (EliminateRaidLobbyInfoDB.PlayingRaidDB.ParticipateCharacterServerIds == null) EliminateRaidLobbyInfoDB.PlayingRaidDB.ParticipateCharacterServerIds = new();
            
            if (EliminateRaidLobbyInfoDB.PlayingRaidDB.ParticipateCharacterServerIds.ContainsKey(keyId))
            {
                EliminateRaidLobbyInfoDB.PlayingRaidDB.ParticipateCharacterServerIds[keyId].AddRange(characterId);
                EliminateRaidLobbyInfoDB.ParticipateCharacterServerIds.AddRange(characterId);
            }
            else
            {
                EliminateRaidLobbyInfoDB.PlayingRaidDB.ParticipateCharacterServerIds[keyId] = characterId;
                EliminateRaidLobbyInfoDB.ParticipateCharacterServerIds = characterId;
            }*/

            if (RaidDB.RaidBossDBs.All(x => x.BossCurrentHP == 0)) return true;
            else return false;
        }

        public void ClearBossData()
        {
            RaidDB = null;
            EliminateRaidLobbyInfoDB = null;
            RaidBattleDB = null;
            BossCharacterIds = null;
        }
    }
}
