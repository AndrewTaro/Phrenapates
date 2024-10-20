using Plana.Database;
using Plana.FlatData;
using Plana.NetworkProtocol;
using Phrenapates.Services;
using Phrenapates.Utils;

namespace Phrenapates.Controllers.Api.ProtocolHandlers
{
    public class Cafe : ProtocolHandlerBase
    {
        private readonly ISessionKeyService sessionKeyService;
        private readonly SCHALEContext context;
        private readonly ExcelTableService excelTableService;

        public Cafe(IProtocolHandlerFactory protocolHandlerFactory, ISessionKeyService _sessionKeyService, SCHALEContext _context, ExcelTableService _excelTableService) : base(protocolHandlerFactory)
        {
            sessionKeyService = _sessionKeyService;
            context = _context;
            excelTableService = _excelTableService;
        }

        [ProtocolHandler(Protocol.Cafe_Get)]
        public ResponsePacket GetHandler(CafeGetInfoRequest req)
        {
            var account = sessionKeyService.GetAccount(req.SessionKey);
            var cafeDbAll = account.Cafes.ToList();
            var cafeDbOne = cafeDbAll.FirstOrDefault(x => x.CafeId == 1);
            var defaultFurnitureExcel = excelTableService.GetTable<DefaultFurnitureExcelTable>().UnPack().DataList;

            var furnitures = account.Furnitures.Select(x => {
                return new FurnitureDB()
                {
                    CafeDBId = x.CafeDBId,
                    UniqueId = x.UniqueId,
                    Location = x.Location,
                    PositionX = x.PositionX,
                    PositionY = x.PositionY,
                    Rotation = x.Rotation,
                    ItemDeploySequence = x.ItemDeploySequence,
                    StackCount = x.StackCount,
                    ServerId = x.ServerId
                };
            }).ToList();

            cafeDbOne.LastUpdate = DateTime.Now;
            cafeDbOne.FurnitureDBs = furnitures
            .Where(x => x.CafeDBId == cafeDbOne.CafeDBId && x.ItemDeploySequence != 0)
            .Select(x => {
                return new FurnitureDB()
                {
                    CafeDBId = x.CafeDBId,
                    UniqueId = x.UniqueId,
                    Location = x.Location,
                    PositionX = x.PositionX,
                    PositionY = x.PositionY,
                    Rotation = x.Rotation,
                    ItemDeploySequence = x.ItemDeploySequence,
                    StackCount = x.StackCount,
                    ServerId = x.ServerId
                };
            }).ToList();

            if(cafeDbOne.CafeVisitCharacterDBs.Count == 0)
            {
                cafeDbOne.CafeVisitCharacterDBs.Clear();
                var count = 0;
                foreach (var character in RandomList.GetRandomList(account.Characters.ToList(), account.Characters.Count < 5 ? account.Characters.Count : new Random().Next(3, 6)))
                {
                    cafeDbOne.CafeVisitCharacterDBs.Add(count, 
                        new CafeCharacterDB()
                        {
                            IsSummon = false,
                            UniqueId = character.UniqueId,
                            ServerId = character.ServerId,
                        }
                    );
                    count++;
                };
            }
            context.SaveChanges();

            return new CafeGetInfoResponse()
            {
                CafeDB = cafeDbOne,
                CafeDBs = cafeDbAll,
                FurnitureDBs = furnitures
            };
        }

        [ProtocolHandler(Protocol.Cafe_Ack)]
        public ResponsePacket AckHandler(CafeAckRequest req)
        {
            // Unable to make the client send this protocol.
            var account = sessionKeyService.GetAccount(req.SessionKey);
            var cafeDb = account.Cafes.FirstOrDefault();
            
            // Cafe Handler stuff
            cafeDb.LastUpdate = DateTime.Now;
            if(cafeDb.CafeVisitCharacterDBs.Count == 0)
            {
                cafeDb.CafeVisitCharacterDBs.Clear();
                var count = 0;
                foreach (var character in RandomList.GetRandomList(account.Characters.ToList(), account.Characters.Count < 5 ? account.Characters.Count : new Random().Next(3, 6)))
                {
                    cafeDb.CafeVisitCharacterDBs.Add(count, 
                        new CafeCharacterDB()
                        {
                            IsSummon = false,
                            UniqueId = character.UniqueId,
                            ServerId = character.ServerId,
                        }
                    );
                    count++;
                };
            }
            context.SaveChanges();

            return new CafeAckResponse()
            {
                CafeDB = cafeDb
            };
        }

        [ProtocolHandler(Protocol.Cafe_Open)]
        public ResponsePacket OpenHandler(CafeOpenRequest req)
        {
            var account = sessionKeyService.GetAccount(req.SessionKey);
            var cafeDbAll = account.Cafes.ToList();
            var cafeDb = cafeDbAll.FirstOrDefault(x => x.CafeId == req.CafeId);
            context.SaveChanges();

            return new CafeOpenResponse()
            {
                OpenedCafeDB = cafeDb,
                FurnitureDBs = account.Furnitures.ToList()
            };
        }

        [ProtocolHandler(Protocol.Cafe_RankUp)]
        public ResponsePacket RankUpHandler(CafeRankUpRequest req)
        {
            return new CafeRankUpResponse();
        }

        [ProtocolHandler(Protocol.Cafe_Deploy)]
        public ResponsePacket DeployHandler(CafeDeployFurnitureRequest req)
        {
            var account = sessionKeyService.GetAccount(req.SessionKey);
            var cafeDb = account.Cafes.FirstOrDefault(x => x.CafeDBId == req.CafeDBId);

            var inventoryFurniture = account.Furnitures.FirstOrDefault(x =>
                x.Location == FurnitureLocation.Inventory &&
                req.FurnitureDB.UniqueId == x.UniqueId &&
                x.ItemDeploySequence == 0
            );

            var placedFurniture = new FurnitureDB()
            {
                CafeDBId = req.CafeDBId,
                UniqueId = req.FurnitureDB.UniqueId,
                Location = req.FurnitureDB.Location,
                PositionX = req.FurnitureDB.PositionX,
                PositionY = req.FurnitureDB.PositionY,
                Rotation = req.FurnitureDB.Rotation,
                StackCount = 1,
            };
            account.Furnitures.Add(placedFurniture);
            context.SaveChanges();
            
            placedFurniture = account.Furnitures.FirstOrDefault(x =>
                x.PositionX == req.FurnitureDB.PositionX &&
                x.PositionY == req.FurnitureDB.PositionY &&
                x.CafeDBId == req.CafeDBId &&
                x.UniqueId == req.FurnitureDB.UniqueId &&
                x.ItemDeploySequence == 0 &&
                x.StackCount == 1);
            placedFurniture.ItemDeploySequence = placedFurniture.ServerId;
            context.SaveChanges();

            placedFurniture = new FurnitureDB
            {
                CafeDBId = placedFurniture.CafeDBId,
                UniqueId = placedFurniture.UniqueId,
                Location = placedFurniture.Location,
                PositionX = placedFurniture.PositionX,
                PositionY = placedFurniture.PositionY,
                Rotation = placedFurniture.Rotation,
                ItemDeploySequence = placedFurniture.ItemDeploySequence,
                StackCount = placedFurniture.StackCount,
                ServerId = placedFurniture.ServerId
            };

            cafeDb.LastUpdate = DateTime.Now;
            cafeDb.FurnitureDBs = account.Furnitures
            .Where(x => 
                x.CafeDBId == req.CafeDBId &&
                x.ItemDeploySequence != 0)
            .Select(x => {
                return new FurnitureDB()
                {
                    CafeDBId = x.CafeDBId,
                    UniqueId = x.UniqueId,
                    Location = x.Location,
                    PositionX = x.PositionX,
                    PositionY = x.PositionY,
                    Rotation = x.Rotation,
                    ItemDeploySequence = x.ItemDeploySequence,
                    StackCount = x.StackCount,
                    ServerId = x.ServerId
                };
            }).ToList();
            context.SaveChanges();

            return new CafeDeployFurnitureResponse()
            {
                CafeDB = cafeDb,
                NewFurnitureServerId = placedFurniture.ServerId,
                ChangedFurnitureDBs = [inventoryFurniture, placedFurniture]
            };
        }

        [ProtocolHandler(Protocol.Cafe_Relocate)]
        public ResponsePacket RelocateHandler(CafeRelocateFurnitureRequest req)
        {
            return new CafeRelocateFurnitureResponse();
        }

        [ProtocolHandler(Protocol.Cafe_Remove)]
        public ResponsePacket RemoveHanlder(CafeRemoveFurnitureRequest req)
        {
            var account = sessionKeyService.GetAccount(req.SessionKey);
            var cafeDb = account.Cafes.FirstOrDefault(x => x.CafeDBId == req.CafeDBId);

            var removedFurniture = account.Furnitures.FirstOrDefault(x =>
                x.CafeDBId == req.CafeDBId &&
                x.ServerId == req.FurnitureServerIds[0]
            );

            account.Furnitures.Remove(removedFurniture);
            context.SaveChanges();

            cafeDb.LastUpdate = DateTime.Now;
            cafeDb.FurnitureDBs = account.Furnitures
            .Where(x => 
                x.CafeDBId == req.CafeDBId &&
                x.ItemDeploySequence != 0)
            .Select(x => {
                return new FurnitureDB()
                {
                    CafeDBId = x.CafeDBId,
                    UniqueId = x.UniqueId,
                    Location = x.Location,
                    PositionX = x.PositionX,
                    PositionY = x.PositionY,
                    Rotation = x.Rotation,
                    ItemDeploySequence = x.ItemDeploySequence,
                    StackCount = x.StackCount,
                    ServerId = x.ServerId
                };
            }).ToList();

            context.SaveChanges();

            return new CafeRemoveFurnitureResponse()
            {
                CafeDB = cafeDb,
                FurnitureDBs = [removedFurniture]
            };
        }

        [ProtocolHandler(Protocol.Cafe_RemoveAll)]
        public ResponsePacket CafeRemoveAllHandler(CafeRemoveAllFurnitureRequest req)
        {
            var account = sessionKeyService.GetAccount(req.SessionKey);
            var cafeDb = account.Cafes.FirstOrDefault(x => x.CafeDBId == req.CafeDBId);
            var defaultFurnitureExcel = excelTableService.GetTable<DefaultFurnitureExcelTable>().UnPack().DataList;

            var removedFurniture = account.Furnitures.Where(x =>
                x.CafeDBId == req.CafeDBId &&
                x.Location != FurnitureLocation.Inventory &&
                x.ItemDeploySequence != 0 &&
                !defaultFurnitureExcel.Any(y => y.Id == x.UniqueId)
            ).ToList();

            context.Furnitures.RemoveRange(removedFurniture);
            context.SaveChanges();

            cafeDb.LastUpdate = DateTime.Now;
            cafeDb.FurnitureDBs = account.Furnitures
            .Where(x => 
                x.CafeDBId == req.CafeDBId &&
                x.ItemDeploySequence != 0)
            .Select(x => {
                return new FurnitureDB()
                {
                    CafeDBId = x.CafeDBId,
                    UniqueId = x.UniqueId,
                    Location = x.Location,
                    PositionX = x.PositionX,
                    PositionY = x.PositionY,
                    Rotation = x.Rotation,
                    ItemDeploySequence = x.ItemDeploySequence,
                    StackCount = x.StackCount,
                    ServerId = x.ServerId
                };
            }).ToList();
            
            context.SaveChanges();

            return new CafeRemoveAllFurnitureResponse()
            {
                CafeDB = cafeDb,
                FurnitureDBs = removedFurniture
            };
        }

        [ProtocolHandler(Protocol.Cafe_SummonCharacter)]
        public ResponsePacket SummonCharacterHandler(CafeSummonCharacterRequest req)
        {
            var account = sessionKeyService.GetAccount(req.SessionKey);
            var cafeDbAll = account.Cafes.ToList();
            var cafeDb = cafeDbAll.FirstOrDefault(x => x.CafeDBId == req.CafeDBId);
            var characterData = account.Characters.FirstOrDefault(x => x.ServerId == req.CharacterServerId);
            
            cafeDb.LastUpdate = DateTime.Now;
            var count = cafeDb.CafeVisitCharacterDBs.Keys.Last();
            count++;
            cafeDb.CafeVisitCharacterDBs.Add(count, 
                new CafeCharacterDB()
                {
                    IsSummon = true,
                    UniqueId = characterData.UniqueId,
                    ServerId = characterData.ServerId,
                }
            );
            context.SaveChanges();

            return new CafeSummonCharacterResponse()
            {
                CafeDB = cafeDb,
                CafeDBs = cafeDbAll
            };
        }

        [ProtocolHandler(Protocol.Cafe_Interact)]
        public ResponsePacket InteractHandler(CafeInteractWithCharacterRequest req)
        {
            return new CafeInteractWithCharacterResponse();
        }

        [ProtocolHandler(Protocol.Cafe_GiveGift)]
        public ResponsePacket CafeGiveGiftHandler(CafeGiveGiftRequest req)
        {
            return new CafeGiveGiftResponse();
        }

        [ProtocolHandler(Protocol.Cafe_ReceiveCurrency)]
        public ResponsePacket ReceiveCurrencyHandler(CafeReceiveCurrencyRequest req)
        {
            return new CafeReceiveCurrencyResponse();
        }

        [ProtocolHandler(Protocol.Cafe_ListPreset)]
        public ResponsePacket ListPresetHandler(CafeListPresetRequest req)
        {
            return new CafeListPresetResponse();
        }

        [ProtocolHandler(Protocol.Cafe_ApplyPreset)]
        public ResponsePacket ApplyPresetHandler(CafeApplyPresetRequest req)
        {
            return new CafeApplyPresetResponse();
        }
        
        [ProtocolHandler(Protocol.Cafe_ApplyTemplate)]
        public ResponsePacket ApplyTemplateHandler(CafeApplyTemplateRequest req)
        {
            return new CafeApplyTemplateResponse();
        }

        [ProtocolHandler(Protocol.Cafe_RenamePreset)]
        public ResponsePacket RenamePresetHandler(CafeRenamePresetRequest req)
        {
            return new CafeRenamePresetResponse();
        }

        [ProtocolHandler(Protocol.Cafe_ClearPreset)]
        public ResponsePacket ClearPresetHandler(CafeClearPresetRequest req)
        {
            return new CafeClearPresetResponse();
        }

        [ProtocolHandler(Protocol.Cafe_TrophyHistory)]
        public ResponsePacket TrophyHistoryHandler(CafeTrophyHistoryRequest req)
        {
            return new CafeTrophyHistoryResponse();
        }
        
        [ProtocolHandler(Protocol.Cafe_UpdatePresetFurniture)]
        public ResponsePacket UpdatePresetFurnitureHandler(CafeUpdatePresetFurnitureRequest req)
        {
            return new CafeUpdatePresetFurnitureResponse();
        }
        
        public static CafeDB CreateCafe(long accountId)
        {
            return new()
            {
                CafeDBId = 0,
                CafeId = 1,
                AccountId = accountId,
                CafeRank = 10,
                LastUpdate = DateTime.Now,
                LastSummonDate = DateTimeOffset.Parse("2023-01-01T00:00:00Z").UtcDateTime,
                CafeVisitCharacterDBs = [],
                FurnitureDBs = [],
                ProductionAppliedTime = DateTime.Now,
                ProductionDB = new()
                {
                    CafeDBId = 0,
                    AppliedDate = DateTime.Now,
                    ComfortValue = 5500,
                    ProductionParcelInfos =
                    [
                        new CafeProductionParcelInfo()
                        {
                            Key = {
                                Type = ParcelType.Currency,
                                Id = (long)CurrencyTypes.Gold,
                            },
                            Amount = 9999999
                        },
                        new CafeProductionParcelInfo()
                        {
                            Key = {
                                Type = ParcelType.Currency,
                                Id = (long)CurrencyTypes.ActionPoint
                            },
                            Amount = 500
                        },
                    ]
                },
            };
        }

        public static CafeDB CreateSecondCafe(long accountId)
        {
            return new()
            {
                CafeDBId = 0,
                CafeId = 2,
                AccountId = accountId,
                CafeRank = 10,
                LastUpdate = DateTime.Now,
                LastSummonDate = DateTimeOffset.Parse("2023-01-01T00:00:00Z").UtcDateTime,
                CafeVisitCharacterDBs = [],
                FurnitureDBs = [],
                ProductionAppliedTime = DateTime.Now,
                ProductionDB = new()
                {
                    CafeDBId = 0,
                    AppliedDate = DateTime.Now,
                    ComfortValue = 5500,
                    ProductionParcelInfos =
                    [
                        new CafeProductionParcelInfo()
                        {
                            Key = {
                                Type = ParcelType.Currency,
                                Id = (long)CurrencyTypes.Gold,
                            },
                            Amount = 9999999
                        }
                    ]
                },
            };
        }
    }
}
