using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Util;

namespace Kdl.Core
{
    public class Board
    {
        #region types

        public record BoardSpecification(
            string Name,
            ImmutableArray<int> PlayerStartRoomIds,
            ImmutableArray<int> DoctorStartRoomIds,
            ImmutableArray<int> CatStartRoomIds,
            ImmutableArray<int> DogStartRoomIds,
            ImmutableArray<Wing> Wings,
            ImmutableArray<Room> Rooms);

        #endregion
        #region public properties and fields

        public string Name { get; init; }
        public ImmutableDictionary<int, Room> Rooms { get; init; } // key is roomId
        public ImmutableArray<int> RoomIds { get; init; } // sorted
        public bool[,] Adjacency { get; init; } // double-indexed by roomId
        public bool[,] Sight { get; init; } // double-indexed by roomId
        public int[,] Distance { get; init; } // double-indexed by roomId
        public int[] AdjacencyCount { get; init; } // indexed by roomId
        public int PlayerStartRoomId { get; init; }
        public int DoctorStartRoomId { get; init; }
        public int CatStartRoomId { get; init; }
        public int DogStartRoomId { get; init; }
        public BoardSpecification Spec { get; init; }

        #endregion
        #region constructors and static factory methods

        public Board(
            string name,
            IEnumerable<Room> rooms,
            int playerStartRoomId,
            int doctorStartRoomId,
            int catStartRoomId,
            int dogStartRoomId,
            BoardSpecification spec = null)
        {
            Name = name;
            Rooms = rooms.ToImmutableDictionary(room => room.Id);
            RoomIds = Rooms.Keys.ToImmutableSortedSet().ToImmutableArray();
            PlayerStartRoomId = playerStartRoomId;
            DoctorStartRoomId = doctorStartRoomId;
            CatStartRoomId = catStartRoomId;
            DogStartRoomId = dogStartRoomId;
            Spec = spec;

            var matrixDim = Rooms.Keys.Max() + 1;
            Adjacency = new bool[matrixDim, matrixDim];
            Sight = new bool[matrixDim, matrixDim];
            AdjacencyCount = new int[matrixDim];

            foreach (var room in Rooms.Values)
            {
                Adjacency[room.Id, room.Id] = true;
                Sight[room.Id, room.Id] = true;
                AdjacencyCount[room.Id] = room.Adjacent.Length;

                foreach(var adjacentRoomId in room.Adjacent)
                {
                    Adjacency[room.Id, adjacentRoomId] = true;
                }

                foreach(var visibleRoomId in room.Visible)
                {
                    Sight[room.Id, visibleRoomId] = true;
                }
            }



            Distance = new int[matrixDim, matrixDim];
            for(int i = 0; i < Distance.Length; i++)
            {
                var r = i / matrixDim;
                var c = i % matrixDim;
                int initialDist;

                if(r == c)
                {
                    initialDist = 0;
                }
                else if(Adjacency[r, c])
                {
                    initialDist = 1;
                }
                else
                {
                    initialDist = 999;
                }

                Distance[r, c] = initialDist;
            }

            var isImprovingDistance = true;
            while(isImprovingDistance)
            {
                isImprovingDistance = false;

                for(int source = 1; source < matrixDim; source++)
                {
                    for(int destination = 1; destination < matrixDim; destination++)
                    {
                        if(source == destination)
                        {
                            continue;
                        }

                        for(int intermediate = 1; intermediate < matrixDim; intermediate++)
                        {
                            var distanceViaIntermediate = Distance[source, intermediate] + Distance[intermediate, destination];

                            if(distanceViaIntermediate < Distance[source, destination])
                            {
                                Distance[source, destination] = distanceViaIntermediate;
                                isImprovingDistance = true;
                            }
                        }
                    }
                }
            }
        }

        public static Board FromJson(string boardPath, string boardNameSuffix)
        {
            return FromJson(boardPath, boardNameSuffix, Enumerable.Empty<string>());
        }

        public static Board FromJson(string boardPath, string boardNameSuffix, IEnumerable<string> closedWingNames)
        {
            var boardText = File.ReadAllText(boardPath);
            var boardSpec = JsonHelper.Deserialize<BoardSpecification>(boardText);
            ImmutableArray<Room> openRooms;

            if(closedWingNames.Any())
            {
                var closedRoomIds = boardSpec.Wings
                    .Where(wing => closedWingNames.Contains(wing.Name, StringComparer.OrdinalIgnoreCase))
                    .SelectMany(wing => wing.RoomIds)
                    .ToImmutableSortedSet();
                openRooms = boardSpec.Rooms
                    .Where(room => !closedRoomIds.Contains(room.Id))
                    .Select(room => room.WithoutClosed(closedRoomIds))
                    .ToImmutableArray();
            }
            else
            {
                openRooms = boardSpec.Rooms;
            }

            var openRoomIdSet = openRooms.Ids().ToHashSet();
            int chooseFirstOpen(IEnumerable<int> desiredRoomIds)
                => desiredRoomIds.First(id => openRoomIdSet.Contains(id));

            var board = new Board(
                name:              boardSpec.Name + boardNameSuffix,
                rooms:             openRooms,
                playerStartRoomId: chooseFirstOpen(boardSpec.PlayerStartRoomIds),
                doctorStartRoomId: chooseFirstOpen(boardSpec.DoctorStartRoomIds),
                catStartRoomId:    chooseFirstOpen(boardSpec.CatStartRoomIds),
                dogStartRoomId:    chooseFirstOpen(boardSpec.DogStartRoomIds));

            return board;
        }

        #endregion
        #region public methods

        public bool IsValid(out List<string> mistakes)
        {
            bool isValid = true;
            mistakes = new();

            if(PlayerStartRoomId <= 0 || DoctorStartRoomId <= 0 || CatStartRoomId <= 0 || DogStartRoomId <= 0)
            {
                mistakes.Add("bad start room id");
                isValid = false;
            }

            foreach(var room in Rooms.Values)
            {
                if(room.Adjacent.Contains(room.Id))
                {
                    mistakes.Add($"room {room.Id} is in own adjacent list");
                    isValid = false;
                }
                if(room.Visible.Contains(room.Id))
                {
                    mistakes.Add($"room {room.Id} is in own visible list");
                }
            }

            int maxRoomId = Adjacency.GetLength(0);

            foreach(var r1 in maxRoomId.ToRange())
            {
                foreach(var r2 in maxRoomId.ToRange())
                {
                    if(Adjacency[r1, r2] != Adjacency[r2, r1])
                    {
                        mistakes.Add($"Adjacency[{r1},{r2}] contradiction");
                        isValid = false;
                    }

                    if(Sight[r1, r2] != Sight[r2, r1])
                    {
                        mistakes.Add($"Visibility[{r1},{r2}] contradiction");
                        isValid = false;
                    }
                }
            }

            return isValid;
        }

        public bool RoomIsSeenBy(int roomOfConcern, IEnumerable<int> roomsWithOtherPeople)
            => roomsWithOtherPeople.Any(roomId => Sight[roomOfConcern, roomId]);

        public int NextRoomId(int roomId, int delta)
        {
            var idx = RoomIds.IndexOf(roomId);
            var nextIdx = (idx + delta).PositiveRemainder(RoomIds.Count());
            var nextRoomId = RoomIds[nextIdx];
            return nextRoomId;
        }

        #endregion
    }
}
