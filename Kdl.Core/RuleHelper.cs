using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kdl.Core
{
    public class RuleHelper
    {
        public class SuperSimple
        {
            public const int PlayerStartingMovePoints = 2;
            public const double MovePointsPerLoot = 1.0 / 2.0;
        }

        public const int NormalPlayerNumStartingCards = 6;
        public const int NumNormalPlayersWhenHaveStrangers = 2;
        public const int NumAllPlayersWhenHaveStrangers = 4;

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
