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

        public double Score(int myPlayerId, int numNormalPlayers)
        {
            return Score(myPlayerId, numNormalPlayers, Attackers);

        }

        public static double Score(int theNormalPlayerId, int numNormalPlayers, IList<int> attackers)
        {
            var numAllPlayers = RuleHelper.NumAllPlayers(numNormalPlayers);
            var score = 0.0;

            // in real games (with defending via discarding clovers), an attack
            // from one side makes the other side weaker (they might discard weapon and move cards);
            // we model this by weighting attacks by an amount that is decreased by opponents' prior attacks;
            const double attenuation = 7.0 / 8.0;
            var attackerSideWeights = numAllPlayers.Times(1.0).ToArray();

            // start with strength=2 instead of 1 to approximate using a 2-strength weapon half the time
            const double initialStrength = 2.0;
            var playerStrengths = numAllPlayers.Times(initialStrength).ToArray();

            foreach(var attackerPlayerId in attackers)
            {
                // 'attacker side' is just the corresponding normal player id to take care of allied strangers
                var attackerSideId = RuleHelper.ToNormalPlayerId(attackerPlayerId, numNormalPlayers);
                var sign = attackerSideId == theNormalPlayerId ? 1.0 : -1.0;

                score += sign * attackerSideWeights[attackerSideId] * playerStrengths[attackerPlayerId];
                playerStrengths[attackerPlayerId]++;

                // right now just weaken last normal player to defend the attack
                var weakenedSideId = numNormalPlayers == RuleHelper.NumNormalPlayersWhenHaveStrangers
                    ? 2 - attackerSideId
                    : (attackerSideId - 1).PositiveRemainder(numAllPlayers);

                attackerSideWeights[weakenedSideId] *= attenuation;
            }

            return score;
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
