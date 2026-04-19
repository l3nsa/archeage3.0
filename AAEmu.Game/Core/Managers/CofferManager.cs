using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.DoodadObj.Funcs;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;

using NLog;

namespace AAEmu.Game.Core.Managers
{
    public class CofferManager : Singleton<CofferManager>
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        // CofferContainer keyed by doodad ObjId
        private readonly ConcurrentDictionary<uint, CofferContainer> _coffers = new();

        // Track which doodad each character currently has open (characterId -> doodadObjId)
        private readonly ConcurrentDictionary<uint, uint> _openCoffers = new();

        public void Load()
        {
            _log.Info("CofferManager initialized");
        }

        /// <summary>
        /// Gets or creates a CofferContainer for a specific doodad
        /// </summary>
        private CofferContainer GetOrCreateCoffer(Doodad doodad, int capacity)
        {
            return _coffers.GetOrAdd(doodad.ObjId, _ => new CofferContainer(doodad.ObjId, capacity));
        }

        /// <summary>
        /// Called when a player opens or closes a coffer doodad
        /// </summary>
        public void InteractCoffer(Character character, uint doodadObjId, bool open)
        {
            var doodad = WorldManager.Instance.GetDoodad(doodadObjId);
            if (doodad == null)
            {
                _log.Warn("CofferInteraction: doodad {0} not found", doodadObjId);
                return;
            }

            if (open)
            {
                OpenCoffer(character, doodad);
            }
            else
            {
                CloseCoffer(character, doodadObjId);
            }
        }

        private void OpenCoffer(Character character, Doodad doodad)
        {
            // Check if another character already has this coffer open
            var existingUser = _openCoffers.FirstOrDefault(kvp => kvp.Value == doodad.ObjId);
            if (existingUser.Value == doodad.ObjId && existingUser.Key != 0 && existingUser.Key != character.Id)
            {
                character.SendErrorMessage(ErrorMessageType.CofferInUse);
                return;
            }

            var capacity = GetCofferCapacity(doodad);
            var coffer = GetOrCreateCoffer(doodad, capacity);

            _openCoffers[character.Id] = doodad.ObjId;

            character.SendPacket(new SCCofferContentsPacket(
                doodad.ObjId,
                coffer.Container.Items,
                coffer.Container.ContainerSize));

            _log.Debug("Character {0} opened coffer doodad {1} (capacity={2}, items={3})",
                character.Name, doodad.ObjId, capacity, coffer.Container.Items.Count);
        }

        private void CloseCoffer(Character character, uint doodadObjId)
        {
            _openCoffers.TryRemove(character.Id, out _);
            _log.Debug("Character {0} closed coffer doodad {1}", character.Name, doodadObjId);
        }

        /// <summary>
        /// Called when a player closes all coffers (e.g., on disconnect)
        /// </summary>
        public void OnCharacterDisconnect(uint characterId)
        {
            _openCoffers.TryRemove(characterId, out _);
        }

        /// <summary>
        /// Swap items between player inventory and coffer, or within coffer
        /// </summary>
        public void SwapCofferItems(Character character, ulong fromItemId, ulong toItemId,
            SlotType fromSlotType, byte fromSlot, SlotType toSlotType, byte toSlot, long dbDoodadId)
        {
            if (!_openCoffers.TryGetValue(character.Id, out var doodadObjId))
            {
                _log.Warn("SwapCofferItems: character {0} has no coffer open", character.Name);
                return;
            }

            if (!_coffers.TryGetValue(doodadObjId, out var coffer))
            {
                _log.Warn("SwapCofferItems: coffer {0} not found", doodadObjId);
                return;
            }

            character.Inventory.SplitOrMoveItem(
                ItemTaskType.SwapCofferItems,
                fromItemId, fromSlotType, fromSlot,
                toItemId, toSlotType, toSlot);

            _log.Debug("SwapCofferItems: {0} swapped items in coffer {1}",
                character.Name, doodadObjId);
        }

        /// <summary>
        /// Split items between player inventory and coffer
        /// </summary>
        public void SplitCofferItem(Character character, uint count, ulong srcId, ulong dstId,
            SlotType srcSlotType, byte srcSlot, SlotType dstSlotType, byte dstSlot, long dbDoodadId)
        {
            if (!_openCoffers.TryGetValue(character.Id, out var doodadObjId))
            {
                _log.Warn("SplitCofferItem: character {0} has no coffer open", character.Name);
                return;
            }

            if (!_coffers.TryGetValue(doodadObjId, out var coffer))
            {
                _log.Warn("SplitCofferItem: coffer {0} not found", doodadObjId);
                return;
            }

            var success = character.Inventory.SplitOrMoveItem(
                ItemTaskType.SplitCofferItems,
                srcId, srcSlotType, srcSlot,
                dstId, dstSlotType, dstSlot,
                (int)count);

            character.SendPacket(new SCSplitCofferItemResultPacket(success, srcId, (int)count));

            _log.Debug("SplitCofferItem: {0} split item in coffer {1}, success={2}",
                character.Name, doodadObjId, success);
        }

        /// <summary>
        /// Extract coffer capacity from doodad's func template
        /// </summary>
        private static int GetCofferCapacity(Doodad doodad)
        {
            // Look up the DoodadFuncCoffer template by the doodad's FuncGroupId
            var func = DoodadManager.Instance.GetFunc(doodad.FuncGroupId, 0);
            if (func != null)
            {
                var template = DoodadManager.Instance.GetFuncTemplate(func.FuncId, func.FuncType);
                if (template is DoodadFuncCoffer cofferFunc && cofferFunc.Capacity > 0)
                    return cofferFunc.Capacity;
            }

            return 50; // default capacity
        }

        /// <summary>
        /// Remove coffer data when doodad is destroyed
        /// </summary>
        public void RemoveCoffer(uint doodadObjId)
        {
            if (_coffers.TryRemove(doodadObjId, out var coffer))
            {
                coffer.Container.Wipe();
                _log.Debug("Removed coffer for doodad {0}", doodadObjId);
            }

            // Close for any players that had it open
            var toClose = _openCoffers.Where(kvp => kvp.Value == doodadObjId).Select(kvp => kvp.Key).ToList();
            foreach (var charId in toClose)
            {
                _openCoffers.TryRemove(charId, out _);
            }
        }
    }

    /// <summary>
    /// Represents a single coffer's storage
    /// </summary>
    public class CofferContainer
    {
        public uint DoodadObjId { get; }
        public ItemContainer Container { get; }

        public CofferContainer(uint doodadObjId, int capacity)
        {
            DoodadObjId = doodadObjId;
            Container = new ItemContainer(null, SlotType.None, false)
            {
                ContainerSize = capacity
            };
        }
    }
}
