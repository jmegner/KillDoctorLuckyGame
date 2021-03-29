using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Util;

namespace Kdl.Core
{
    public class RuleHelper
    {
        public class SuperSimple
        {
            public const int PlayerStartingMovePoints = 2;
            public const double MovePointsPerLoot = 1.0 / 2.0;

            public static double Score(
                int scoringPlayerId,
                int numNormalPlayers,
                IEnumerable<int> attackers)
            {
                return Score(scoringPlayerId, numNormalPlayers, attackers, out _);

            }
            public static double Score(
                int scoringPlayerId,
                int numNormalPlayers,
                IEnumerable<int> attackers,
                out double gainFromAttackingNext)
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
                    var sign = attackerSideId == scoringPlayerId ? 1.0 : -1.0;

                    score += sign * attackerSideWeights[attackerSideId] * playerStrengths[attackerPlayerId];
                    playerStrengths[attackerPlayerId]++;

                    // right now just weaken last normal player to defend the attack
                    var weakenedSideId = numNormalPlayers == RuleHelper.NumNormalPlayersWhenHaveStrangers
                        ? 2 - attackerSideId
                        : (attackerSideId - 1).PositiveRemainder(numAllPlayers);

                    attackerSideWeights[weakenedSideId] *= attenuation;
                }

                gainFromAttackingNext = attackerSideWeights[scoringPlayerId] * playerStrengths[scoringPlayerId];
                return score;
            }

        }

        public const int NormalPlayerNumStartingCards = 6;
        public const int NumNormalPlayersWhenHaveStrangers = 2;
        public const int NumAllPlayersWhenHaveStrangers = 4;
        public const int StrangerPlayerIdFirst = 1;
        public const int StrangerPlayerIdSecond = 3;

        public RuleFlags RuleFlags { get; set; }

        #if false
        public RuleHelper(RuleFlags ruleFlags)
        {
            RuleFlags = ruleFlags;
        }
        #endif

        public static IDeck NewDeck(RuleFlags ruleFlags, string cardsPath, Random rng)
        {
            if(ruleFlags.HasFlag(RuleFlags.SuperSimple))
            {
                return new SuperSimpleDeck();
            }
            else if(ruleFlags.HasFlag(RuleFlags.FairCards))
            {
                return new FairDeck();
            }
            else
            {
                return NormalDeck.FromJson(cardsPath, rng);
            }
        }

        public static int NumAllPlayers(int numNormalPlayers)
            => numNormalPlayers == NumNormalPlayersWhenHaveStrangers ? NumAllPlayersWhenHaveStrangers : numNormalPlayers;

        public static int ToNormalPlayerId(int playerId, int numNormalPlayers)
        {
            if(numNormalPlayers != NumNormalPlayersWhenHaveStrangers)
            {
                return playerId;
            }

            return (playerId == 0 || playerId == 3) ? 0 : 2;
        }

    }
}
