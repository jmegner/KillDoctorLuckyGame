using System.Collections.Generic;

namespace Kdl.Core
{
    public class Player
    {
        public PlayerType PlayerType; 
        public int Strength { get; protected set; }
        public RoomId Room { get; protected set; }
        protected List<Card> _cards;
    }
}
