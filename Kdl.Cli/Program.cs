using System;
using Kdl.Core;

namespace Kdl.Cli
{
    public class Program
    {
        protected const string DataDir = "../../../../Kdl.Core/Data";
        static void Main(string[] args)
        {
            Console.WriteLine("program begin");
            BoardFiddle();
            Console.WriteLine("program end");
        }

        static void BoardFiddle()
        {
            var board = Board.FromJson(
                DataDir + "/RoomsMainAll.json",
                DataDir + "/RoomIdsMainWestWing.json");
        }
    }
}
