using System;
using System.Numerics;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Tasks.UnitMove;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.Units.Route
{
    /// <summary>
    /// Возвращаемся на место респавна, после боя
    /// </summary>
    internal class Line : Patrol
    {
        private const uint NpcIdSeaTransport = 13677;
        private const uint NpcIdLandTransport = 13676;
        private const uint NpcIdAirTransport = 13680;

        //public Point LastPatrolPosition { get; set; }

        private bool move;
        //private float returnDistance;
        //private float absoluteReturnDistance;
        //private Vector3 newPosition;
        //private float diffX;
        //private float diffY;
        //private float diffZ;
        private float velocity;
        private Vector3 direction;
        private Vector3 diff;
        //private float newx;
        //private float newy;
        private readonly float newz;
        //private float x;
        //private float y;
        //private float z;
        //private float mov;
        private sbyte rotZ;

        public override void Execute(Npc npc)
        {
            if (npc == null) { return; }

            if (PausePosition == null)
            {
                Stop(npc);
                return;
            }

            move = false;
            // текущая позиция
            vPosition = new Vector3(npc.Position.X, npc.Position.Y, npc.Position.Z);

            if (!InPatrol)
            {
                var tmp = new Vector3(PausePosition.X, PausePosition.Y, PausePosition.Z); // точка возврата
                vPausePosition = tmp != Vector3.Zero ? tmp : vPosition;
                //vPausePosition = Vector3.Zero;

                // откуда идем (место после боя)
                vBeginPoint = new Vector3(npc.Position.X, npc.Position.Y, npc.Position.Z);
                // куда идем (место респавна)
                vEndPoint = new Vector3(vPausePosition.X, vPausePosition.Y, vPausePosition.Z);
                Angle = MathUtil.CalculateAngleFrom(vBeginPoint.X, vBeginPoint.Y, vEndPoint.X, vEndPoint.Y);
                //Angle = MathUtil.DegreeToRadian(Angle);
                InPatrol = true;
                npc.IsInBattle = false;
            }

            vDistance = vEndPoint - vPosition;
            // скорость движения velociti за период времени DeltaTime
            //DeltaTime = (float)(DateTime.UtcNow - UpdateTime).TotalMilliseconds;
            DeltaTime = 0.1f; // temporarily took a constant, later it will be necessary to take the current time
            velocity = vMaxVelocityForwardWalk.X * DeltaTime;
            // вектор направление на таргет (последовательность аргументов важно, чтобы смотреть на таргет)
            direction = vEndPoint - vPosition;
            // вектор направления необходимо нормализовать
            if (direction != Vector3.Zero)
            {
                direction = Vector3.Normalize(direction);
            }

            // вектор скорости (т.е. координаты, куда попадём двигаясь со скоростью velociti по направдению direction)
            diff = direction * velocity;

            npc.Position.X += diff.X;
            npc.Position.Y += diff.Y;
            npc.Position.Z += diff.Z;
            if (Math.Abs(diff.X) > 0 || Math.Abs(diff.Y) > 0) { move = true; }
            if (Math.Abs(vDistance.X) < RangeToCheckPoint) { npc.Position.X = vEndPoint.X; }
            if (Math.Abs(vDistance.Y) < RangeToCheckPoint) { npc.Position.Y = vEndPoint.Y; }
            if (Math.Abs(vDistance.Z) < RangeToCheckPoint) { npc.Position.Z = vEndPoint.Z; }
            if (!(Math.Abs(diff.X) > 0) && !(Math.Abs(diff.Y) > 0))
            {
                move = false;
                InPatrol = false;
            }
            // Simulated unit
            moveType = (ActorData)UnitMovement.GetType(UnitMovementType.Actor);
            // Change NPC coordinates
            moveType.X = npc.Position.X;
            moveType.Y = npc.Position.Y;
            if (npc.TemplateId == NpcIdSeaTransport || npc.TemplateId == NpcIdLandTransport) // swimming
            {
                moveType.Z = 98.5993f;
            }
            else if (npc.TemplateId == NpcIdAirTransport) // shark
            {
                moveType.Z = 95.5993f;
            }
            else // other
            {
                moveType.Z = AppConfiguration.Instance.HeightMapsEnable ? WorldManager.Instance.GetHeight(npc.Position.ZoneId, npc.Position.X, npc.Position.Y) : npc.Position.Z;
                if (npc.Position.Z - moveType.Z > 2.0)
                {
                    moveType.Z = npc.Position.Z;
                }
            }
            // looks in the direction of movement
            rotZ = MathUtil.ConvertDegreeToDirection(Angle);
            moveType.Rot = new Quaternion(0f, 0f, Helpers.ConvertDirectionToRadian(rotZ), 1f);
            npc.Rot = moveType.Rot;
            moveType.DeltaMovement = new Vector3(0, 1.0f, 0);
            moveType.actorFlags = ActorMoveType.Walk; // 5-walk, 4-run, 3-stand still
            moveType.Stance = EStance.Idle;           // COMBAT = 0x0, IDLE = 0x1
            moveType.Alertness = AiAlertness.Idle;    // IDLE = 0x0, ALERT = 0x1, COMBAT = 0x2
            moveType.Time = Seq;                      // has to change all the time for normal motion.

            if (move)
            {
                // 广播移动状态
                // broadcast mobile status
                npc.BroadcastPacket(new SCOneUnitMovementPacket(npc.ObjId, moveType), true);
                LoopDelay = 500;
                Repeat(npc);
            }
            else
            {
                // 停止移动
                // stop moving
                moveType.DeltaMovement = Vector3.Zero;
                moveType.Velocity = Vector3.Zero;
                npc.BroadcastPacket(new SCOneUnitMovementPacket(npc.ObjId, moveType), true);
                //LoopAuto(npc);
                // остановиться на time секунд
                double time = (uint)Rand.Next(5, 15);
                TaskManager.Instance.Schedule(new UnitMovePause(this, npc), TimeSpan.FromSeconds(time));
            }
        }
        public override void Execute(Transfer transfer)
        {
            // TODO: Implement transfer route execution for Line patrol
        }
        public override void Execute(Gimmick gimmick)
        {
            // TODO: Implement gimmick route execution for Line patrol
        }
        public override void Execute(BaseUnit unit)
        {
            // TODO: Implement base unit route execution for Line patrol
        }
    }
}
