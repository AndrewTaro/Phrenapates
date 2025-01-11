using Phrenapates.Services;
using Plana.Database;
using Plana.Database.ModelExtensions;
using Plana.FlatData;
using Plana.MX.GameLogic.DBModel;
using Plana.MX.NetworkProtocol;

namespace Phrenapates.Controllers.Api.ProtocolHandlers
{
    public class Equipment : ProtocolHandlerBase
    {
        private readonly ISessionKeyService sessionKeyService;
        private readonly SCHALEContext context;
        private readonly ExcelTableService excelTableService;

        public Equipment(IProtocolHandlerFactory protocolHandlerFactory, ISessionKeyService _sessionKeyService, SCHALEContext _context, ExcelTableService _excelTableService) : base(protocolHandlerFactory)
        {
            sessionKeyService = _sessionKeyService;
            context = _context;
            excelTableService = _excelTableService;
        }

        [ProtocolHandler(Protocol.Equipment_Equip)]
        public ResponsePacket EquipHandler(EquipmentItemEquipRequest req)
        {
            var account = sessionKeyService.GetAccount(req.SessionKey);

            var originalStack = account.Equipment.FirstOrDefault(x => x.ServerId == req.EquipmentServerId);
            var newEquipment = new EquipmentDB()
            {
                UniqueId = originalStack.UniqueId,
                Level = originalStack.Level,
                StackCount = 1,
                BoundCharacterServerId = req.CharacterServerId,
            };

            var equippedCharacter = account.Characters.FirstOrDefault(x => x.ServerId == req.CharacterServerId);

            // remove 1 from original equipment stack
            originalStack.StackCount--;

            // add new equipment w BoundCharacterServerId (with different EquipmentServerId)
            account.AddEquipment(context, [newEquipment]);
            context.SaveChanges(); // (need newEquipment.ServerId in the next line, so save here first, otherwise its 0)

            // set the character's EquipmentServerIds
            equippedCharacter.EquipmentServerIds[req.SlotIndex] = newEquipment.ServerId;

            context.SaveChanges();

            return new EquipmentItemEquipResponse()
            {
                CharacterDB = equippedCharacter,
                EquipmentDBs = [newEquipment, originalStack]
            };
        }

        // dont use this, too lazy to implement, just use batch growth ty
        [ProtocolHandler(Protocol.Equipment_LevelUp)]
        public ResponsePacket LevelUpHandler(EquipmentItemLevelUpRequest req)
        {
            var account = sessionKeyService.GetAccount(req.SessionKey);
            var targetEquipment = account.Equipment.FirstOrDefault(x => x.ServerId == req.TargetServerId);

            targetEquipment.Level = 65;
            targetEquipment.Tier = 9;

            context.SaveChanges();

            return new EquipmentItemLevelUpResponse()
            {
                EquipmentDB = targetEquipment,
            };
        }

        [ProtocolHandler(Protocol.Equipment_BatchGrowth)]
        public ResponsePacket Equipment_BatchGrowthHandler(EquipmentBatchGrowthRequest req)
        {
            var account = sessionKeyService.GetAccount(req.SessionKey);
            var packetData = new EquipmentBatchGrowthResponse();

            if (req.EquipmentBatchGrowthRequestDBs.Count != 0)
            {
                var upgradedEquipment = new List<EquipmentDB>();
                foreach (var batchGrowthDB in req.EquipmentBatchGrowthRequestDBs)
                {
                    var targetEquipment = account.Equipment.FirstOrDefault(x => x.ServerId == batchGrowthDB.TargetServerId);

                    targetEquipment.Tier = (int)batchGrowthDB.AfterTier;
                    targetEquipment.Level = (int)batchGrowthDB.AfterLevel;
                    targetEquipment.UniqueId = targetEquipment.UniqueId + batchGrowthDB.AfterTier - 1; // should prob use excel, im lazyzz...
                    targetEquipment.IsNew = true;
                    targetEquipment.StackCount = 1;

                    context.SaveChanges();
                    upgradedEquipment.Add(targetEquipment);    
                }
                packetData.EquipmentDBs = upgradedEquipment;
            }

            if (req.GearTierUpRequestDB.TargetServerId != null)
            {
                var gearExcelTable = excelTableService.GetExcelDB<CharacterGearExcel>();
                var targetGear = account.Gears.FirstOrDefault(x => x.ServerId == req.GearTierUpRequestDB.TargetServerId);
                var targetCharacter = account.Characters.FirstOrDefault(x => x.ServerId == targetGear.BoundCharacterServerId);
                
                var gearId = gearExcelTable.FirstOrDefault(x => 
                    x.CharacterId == targetCharacter.UniqueId &&
                    x.Tier == 2
                ).Id;

                targetGear.UniqueId = gearId;
                targetGear.Tier = 2;
                
                context.SaveChanges();
                packetData.GearDB = targetGear;
            }

            context.SaveChanges();
            
            return packetData;
        }

    }
}
