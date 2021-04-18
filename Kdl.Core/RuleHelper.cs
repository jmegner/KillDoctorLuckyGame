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
        public class Simple
        {
            public const double JustOverOneThird = 11.0 / 32.0;

            public const double PlayerStartingMoveCards = 1.0; //2.5;
            public const double MoveCardsPerLoot = JustOverOneThird;
            public const double CloversPerMoveCard = 1.0;

            public const double PlayerStartingWeapons = 2.0;
            public const double WeaponsPerLoot = JustOverOneThird;
            public const double StrengthPerWeapon = 53.0 / 24.0;
            public const double CloversPerWeapon = 1.0;

            public const double PlayerStartingFailures = 2.0;
            public const double FailuresPerLoot = JustOverOneThird;
            public const double CloversPerFailure = 50.0 / 24.0;

            public const double CloversContributedPerStranger = 1.0;

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
                    var attackerSideId = ToNormalPlayerId(attackerPlayerId, numNormalPlayers);
                    var sign = attackerSideId == scoringPlayerId ? 1.0 : -1.0 / (numNormalPlayers - 1);

                    score += sign * attackerSideWeights[attackerSideId] * playerStrengths[attackerPlayerId];
                    playerStrengths[attackerPlayerId]++;

                    // right now just weaken last normal player to defend the attack
                    var weakenedSideId = numNormalPlayers == NumNormalPlayersWhenHaveStrangers
                        ? 2 - attackerSideId
                        : (attackerSideId - 1).PositiveRemainder(numAllPlayers);

                    attackerSideWeights[weakenedSideId] *= attenuation;
                }

                gainFromAttackingNext = attackerSideWeights[scoringPlayerId] * playerStrengths[scoringPlayerId];
                return score;
            }

        }

        public const int PlayerStartingStrength = 1;
        public const int NormalPlayerNumStartingCards = 6;
        public const int NumNormalPlayersWhenHaveStrangers = 2;
        public const int NumAllPlayersWhenHaveStrangers = 4;

        public const int InvalidPlayerId = -1;

        public const int NormalPlayerIdFirst = 0;
        public const int StrangerPlayerIdFirst = 1;
        public const int NormalPlayerIdSecond = 2;
        public const int StrangerPlayerIdSecond = 3;

        public const int SideANormalPlayerId = 0;
        public const int SideBStrangerPlayerId = 1;
        public const int SideBNormalPlayerId = 2;
        public const int SideAStrangerPlayerId = 3;

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

        public static int ToNormalPlayerId(int playerId, int numNormalPlayers = NumNormalPlayersWhenHaveStrangers)
        {
            if(numNormalPlayers != NumNormalPlayersWhenHaveStrangers)
            {
                return playerId;
            }

            return (playerId == SideANormalPlayerId || playerId == SideAStrangerPlayerId)
                ? SideANormalPlayerId : SideBNormalPlayerId;
        }

        // only for two-player games
        public static int AlliedStranger(int playerId)
            => playerId switch
            {
                SideANormalPlayerId   => SideAStrangerPlayerId,
                SideAStrangerPlayerId => SideAStrangerPlayerId,
                SideBNormalPlayerId   => SideBStrangerPlayerId,
                SideBStrangerPlayerId => SideBStrangerPlayerId,
                _ => RuleHelper.InvalidPlayerId,
            };

        // only for two-player games
        public static int OpposingNormalPlayer(int playerId)
            => playerId == SideANormalPlayerId || playerId == SideAStrangerPlayerId
            ? SideBNormalPlayerId : SideANormalPlayerId;

        // only for two-player games
        public static int OpposingStranger(int playerId)
            => AlliedStranger(OpposingNormalPlayer(playerId));

    }
}
