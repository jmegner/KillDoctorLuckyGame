using System;
using Kdl.Core;

namespace Kdl.Cli
{
    public class Program
    {
        protected const string DataDir = "../../../../Kdl.Core/Data";
        static int Main(string[] args)
        {
            Console.WriteLine("program begin");
            BoardFiddle();
            Console.WriteLine("program end");
            return 0;
        }

        static void BoardFiddle()
        {
            var board = Board.FromJson(
                DataDir + "/BoardMain.json",
                new[] { "west" });
            var deck = Deck.FromJson(DataDir + "/Cards.json");
        }
    }
}
