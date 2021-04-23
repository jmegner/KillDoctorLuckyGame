using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Util;

namespace Kdl.Core
{
    public class GameHistory
    {
        public class PlayerStat
        {
            public int NumCardsDrawn { get; set; }
            public int NumAttacks { get; set; }
        }

        public List<PlayerStat> PlayerStats { get; set; }
        public List<int> Attackers { get; set; } = new();
        public List<SimpleTurn> Turns { get; set; } = new();

        public GameHistory(int numPlayers)
        {
            if(numPlayers == 2)
            {
                numPlayers = 4;
            }

            PlayerStats = new(numPlayers.Times(() => new PlayerStat()));
        }

        public void RememberAttack(int playerId)
        {
            PlayerStats[playerId].NumAttacks++;
            Attackers.Add(playerId);
        }

        public void RememberLoot(int playerId)
        {
            PlayerStats[playerId].NumCardsDrawn++;
        }

        public string AttackDisplayNums()
            => string.Join(',', Attackers.Select(pid => pid + 1));

        public string BriefTurnHist() => string.Join(" ", Turns);

    }
}
