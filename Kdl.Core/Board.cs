using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kdl.Core
{
    public class Board
    {
        public ImmutableArray<Room> Rooms { get; init; }

        public Board(IEnumerable<Room> rooms)
        {
            Rooms = rooms.ToImmutableArray();
        }

        public static Board FromJson(string roomsPath, string closedRoomIdsPath = null)
        {
            var jsonOptions = new JsonSerializerOptions()
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true,
            };
            var roomsJson = File.ReadAllText(roomsPath);
            var allRooms = JsonSerializer.Deserialize<List<Room>>(roomsJson, jsonOptions);

            Board board;

            if(string.IsNullOrEmpty(closedRoomIdsPath))
            {
                board = new Board(allRooms);
            }
            else
            {
                var closedRoomIdsJson = File.ReadAllText(closedRoomIdsPath);
                var closedRoomIds = JsonSerializer.Deserialize<HashSet<int>>(closedRoomIdsJson, jsonOptions);
                var openRooms = allRooms.Where(room => !closedRoomIds.Contains(room.Id));
                board = new Board(openRooms);
            }

            return board;
        }
    }
}
