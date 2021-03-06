using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kdl.Core
{
    public class Board
    {
        #region types

        public record JsonBoard(string Name, ImmutableArray<Wing> Wings, ImmutableArray<Room> Rooms);

        #endregion
        #region public properties and fields

        public string Name { get; init; }
        public ImmutableArray<Wing> Wings { get; init; }
        public ImmutableSortedSet<string> ClosedWings { get; init; }
        public ImmutableArray<Room> AllRooms { get; init; }
        public ImmutableArray<Room> OpenRooms { get; init; }

        #endregion
        #region constructors and static factory methods

        public Board(string name, IEnumerable<Room> rooms)
        {
            Name = name;
            Wings = new ImmutableArray<Wing>();
            ClosedWings = ImmutableSortedSet<string>.Empty;
            AllRooms = rooms.ToImmutableArray();
            OpenRooms = AllRooms;
        }

        public Board(
            string name,
            IEnumerable<Wing> wings,
            IEnumerable<string> closedWings,
            IEnumerable<Room> allRooms,
            IEnumerable<Room> openRooms )
        {
            Name = name;
            Wings = wings.ToImmutableArray();
            ClosedWings = closedWings.ToImmutableSortedSet();
            AllRooms = allRooms.ToImmutableArray();
            OpenRooms = openRooms.ToImmutableArray();
        }

        public static Board FromJson(string boardPath, IEnumerable<string> closedWingNames = null)
        {
            var boardText = File.ReadAllText(boardPath);
            var boardRaw = JsonHelper.Deserialize<JsonBoard>(boardText);
            ImmutableArray<Room> openRooms;
            

            if(closedWingNames?.Any() == true)
            {
                var closedRoomIds = boardRaw.Wings
                    .Where(wing => closedWingNames.Contains(wing.Name, StringComparer.OrdinalIgnoreCase))
                    .SelectMany(wing => wing.RoomIds)
                    .ToImmutableSortedSet();
                openRooms = boardRaw.Rooms
                    .Where(room => !closedRoomIds.Contains(room.Id))
                    .ToImmutableArray();
            }
            else
            {
                openRooms = boardRaw.Rooms;
            }

            var board = new Board(
                boardRaw.Name,
                boardRaw.Wings,
                closedWingNames.ToImmutableSortedSet(),
                boardRaw.Rooms,
                openRooms);

            return board;
        }

        #endregion
    }
}
