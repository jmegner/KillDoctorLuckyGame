using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kdl.Core
{
    public class GameState
    {
        public RoomId DoctorRoomId { get; protected set; }
        public List<Player> Players { get; protected set; }
        public int TurnId { get; protected set; }
        public int CurrentPlayerIdx { get; protected set; }
        public TurnPhase CurrentPhase { get; protected set; }
    }
}
