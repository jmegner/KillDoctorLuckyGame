using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kdl.Core
{
    [Flags]
    public enum RuleFlag
    {
        Standard = 0,
        CantMoveVisibleStrangerAndAttackSameTurn = 1 << 0,
        AlternateBoardStairwaysDontGiveSight = 1 << 1,
    }

    public class GameState
    {
        public RuleFlag Rules { get; protected set; }
        public Board Board { get; protected set; }
        public RoomId DoctorRoomId { get; protected set; }
        public List<Player> Players { get; protected set; }
        public int TurnId { get; protected set; }
        public int CurrentPlayerIdx { get; protected set; }
        public TurnPhase CurrentPhase { get; protected set; }
    }
}
