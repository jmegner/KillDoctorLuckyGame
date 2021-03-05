namespace Kdl.Core
{
    public record Card(
        string Name,
        string RoomName,
        int Clover,
        int Move,
        int Attack,
        int SpecialAttack)
    {
        public static Card NewWeapon(
            string name,
            string roomName,
            int clover,
            int attack,
            int specialAttack)
        {
            var card = new Card(
                name,
                roomName,
                clover,
                0,
                attack,
                specialAttack);
            return card;
        }

        public static Card NewFailure(
            string name,
            int clover)
        {
            var card = new Card(
                name,
                "",
                clover,
                0,
                0,
                0);
            return card;
        }

        public static Card NewMove(
            string name,
            string roomName,
            int clover,
            int move)
        {
            var card = new Card(
                name,
                roomName,
                clover,
                move,
                0,
                0);
            return card;
        }
    }
}
