using System.Collections.Generic;
using System.Linq;

namespace Kdl.Core
{
    public class Player
    {
        public int Id { get; init; }
        public int DisplayNum => Id + 1;
        public PlayerType PlayerType { get; init; }
        public int Strength { get; set; } = 1;
        public int RoomId { get; set; }
        public List<Card> Cards;
        public int SimpleMovePoints => Cards.Count;

        public override string ToString()
            => PlayerType == PlayerType.Normal
            ? $"{BriefText()}(R{RoomId:D2},S{Strength},M{SimpleMovePoints})"
            : $"{BriefText()}(R{RoomId:D2},S{Strength})";

        public string BriefText()
            => PlayerType == PlayerType.Normal
            ? "P" + DisplayNum
            : "p" + DisplayNum;

        public Player(
            int id,
            PlayerType playerType,
            int roomId,
            IEnumerable<Card> cards)
        {
            Id = id;
            PlayerType = playerType;
            RoomId = roomId;
            Cards = new List<Card>(cards);
        }

        public void SubtractSimpleMovePoints(int numMovePoints)
        {
            if(numMovePoints > 0 && Cards.Count >= numMovePoints)
            {
                Cards.RemoveRange(Cards.Count - numMovePoints, numMovePoints);
            }
        }

        public void AddCard(Card card)
        {
            if(card != null)
            {
                Cards.Add(card);
            }
        }

        public int NumSlotsAfter(Player priorPlayer, int numPlayers)
        {
            if(Id > priorPlayer.Id)
            {
                return Id - priorPlayer.Id;
            }

            // deliberately chose result to be numPlayers if our Id == priorPlayer.Id
            // because this is for deciding who the doctor activates
            return numPlayers - (priorPlayer.Id - Id);
        }
    }

    public static class PlayerExtensions
    {
        public static IEnumerable<int> Ids(this IEnumerable<Player> players)
            => players.Select(p => p.Id);

        public static IEnumerable<string> IdTexts(this IEnumerable<Player> players)
            => players.Select(p => "P" + p.Id);

        public static IEnumerable<int> RoomIds(this IEnumerable<Player> players)
            => players.Select(p => p.RoomId);

        public static IEnumerable<int> OtherPlayerRoomIds(this IEnumerable<Player> players, Player playerToExclude)
            => players.OtherPlayerRoomIds(playerToExclude.Id);
        public static IEnumerable<int> OtherPlayerRoomIds(this IEnumerable<Player> players, int playerIdToExclude)
            => players.Where(p => p.Id != playerIdToExclude).Select(p => p.RoomId);
    }
}
