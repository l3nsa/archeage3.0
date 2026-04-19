using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs
{
    public class DoodadFuncCoffer : DoodadFuncTemplate
    {
        public int Capacity { get; set; }

        public override void Use(Unit caster, Doodad owner, uint skillId)
        {
            _log.Debug("DoodadFuncCoffer: caster={0}, doodad={1}, capacity={2}", caster.Name, owner.ObjId, Capacity);

            if (caster is Character character)
            {
                CofferManager.Instance.InteractCoffer(character, owner.ObjId, true);
            }
        }
    }
}
