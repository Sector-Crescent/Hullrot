/*
MIT
Copyright (c) 2025 MLGTASTICa/SPCR

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

1. The above copyright notice and this permission notice shall be included in
   all copies or substantial portions of the Software.

2. THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
   FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
   DEALINGS IN THE SOFTWARE.

*/

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;


namespace Content.Server._Crescent.Physics;

/// <summary>
/// This handles map and grid chunking.
/// </summary>
public sealed class HullrotEntityChunkSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    // Size of the side a chunk in the world map. It is a square. The smaller it is the better queries/intersection/rays calculation get.
    // The smaller it is , the more frequent memory movements/allocations get though.
    // 4x4 strikes a nice balance in my opinion.
    public const int chunkSize = 4;
    // Size of the side of a chunk sector size.
    public const int chunkManagerSectorSize = 64000;
    // Allocation size of a single row/column. (aka how many chunks a single column/row stores)
    public const int minimumRowSize = chunkManagerSectorSize / chunkSize;
    // Minimum entity list size of a chunk. Setting this to a sensible value prevents constant resizes!
    public const int chunkEntityMinimum = 16;
    // All chunk managers regarding the WORLD. Grids get their own chunk managers in their own components.
    public FrozenDictionary<MapId, HullrotChunkManager> chunkManagers = new Dictionary<MapId, HullrotChunkManager>().ToFrozenDictionary();

    // Not needed to be a class rn , but in the future should be expanded to hold separate lists for fast query of constantly looked categories.
    // You also really dont want these to be stored as actual structs
    public class HullrotChunk
    {
        public HashSet<EntityUid> containedEntities = new(chunkEntityMinimum);
    }

    // One of these is at worst 2 GB. You shouldn't have the need for multiple 64km X 64km maps though.
    // Each row has 16000 chunks , so it takes 0.128 MB to represent. So the minimum usage is exactly that
    public class HullrotChunkManager : IDisposable
    {
        // the starting point of this chunk
        public Vector2 bottomLeft = new Vector2(-chunkManagerSectorSize/2, -chunkManagerSectorSize/2);

        // holds all the chunks. Empty rows if there are none on that row.
        public List<List<HullrotChunk?>?> chunks = new List<List<HullrotChunk?>?>(new List<HullrotChunk?>?[minimumRowSize]);

        public Vector2 getChunkKey(Vector2 targetPos)
        {
            if(targetPos.X - bottomLeft.X > chunkManagerSectorSize || targetPos.X - bottomLeft.X < 0)
                return Vector2.Zero;
            if(targetPos.Y - bottomLeft.Y > chunkManagerSectorSize || targetPos.Y - bottomLeft.Y < 0)
                return Vector2.Zero;
            return ((targetPos - bottomLeft) / chunkSize).Rounded();
        }

        public void Dispose()
        {
            foreach (var chunkList in chunks)
            {
                if (chunkList is null)
                    continue;
                foreach (var chunk in chunkList)
                {
                    if (chunk is null)
                        continue;
                    chunk.containedEntities.Clear();
                }
                chunkList.Clear();
            }
            chunks.Clear();
        }

        public bool getChunk(Vector2 targetPos, [NotNullWhen(true)] out HullrotChunk? chunk)
        {
            chunk = null;
            Vector2 truePos = getChunkKey(targetPos);
            if (chunks[(int) truePos.X] is null)
                return false;
            if (chunks[(int) truePos.X]![(int) truePos.Y] is null)
                return false;
            chunk = chunks[(int) truePos.X]![(int) truePos.Y]!;
            return true;
        }

        // will initialize a chunk if it doesn't exist.
        public HullrotChunk initializeChunk(Vector2 targetPos)
        {
            Vector2 truePos = getChunkKey(targetPos);
            if (chunks[(int) truePos.X] is null)
            {
                chunks[(int) truePos.X] = new List<HullrotChunk?> (new HullrotChunk?[minimumRowSize]);
            }

            if (chunks[(int) truePos.X]![(int) truePos.Y] is null)
            {
                chunks[(int) truePos.X]![(int) truePos.Y] = new HullrotChunk();
            }

            return chunks[(int) truePos.X]![(int) truePos.Y]!;
        }
    }

    public Vector2 EntityCoordToWorld(EntityCoordinates input)
    {
        return _transformSystem.GetWorldPosition(input.EntityId) + input.Position;
    }

    public void OnEntityMove(ref MoveEvent input)
    {
        // idk why this gets sent but ss14 sucks god damn - SPCR 2025
        if (TerminatingOrDeleted(input.Entity.Owner))
            return;
        if (TerminatingOrDeleted(input.OldPosition.EntityId) || TerminatingOrDeleted(input.NewPosition.EntityId))
            return;
        MapId oldMap = _transformSystem.GetMapId(input.OldPosition);
        MapId newMap = _transformSystem.GetMapId(input.NewPosition);
        HullrotChunkManager oldManager = chunkManagers[oldMap];
        HullrotChunkManager newManager = chunkManagers[newMap];
        Vector2 oldKey = oldManager.getChunkKey( EntityCoordToWorld(input.OldPosition));
        Vector2 newKey = newManager.getChunkKey(EntityCoordToWorld(input.NewPosition));
        // SAME POSITION
        if (oldMap == newMap && oldKey == newKey)
            return;
        if (oldManager.getChunk(oldKey, out var oldChunk))
        {
            oldChunk.containedEntities.Remove(input.Entity.Owner);
        }

        if (!newManager.getChunk(newKey, out var newChunk))
        {
            newChunk = newManager.initializeChunk(newKey);
        }
        newChunk.containedEntities.Add(input.Entity.Owner);
        Logger.Debug($"Moved Entity {input.Entity} from chunk {oldKey} to {newKey}");

    }

    private void onMapCreation(MapChangedEvent args)
    {
        Dictionary<MapId, HullrotChunkManager> buildingDict = chunkManagers.ToDictionary();
        if(args.Created)
            buildingDict.Add(args.Map, new HullrotChunkManager());
        if(args.Destroyed)
        {
            HullrotChunkManager manager = buildingDict[args.Map];
            buildingDict.Remove(args.Map);
            manager.Dispose();
        }
        chunkManagers = buildingDict.ToFrozenDictionary();
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MapChangedEvent>(onMapCreation);
        // create chunk managers for any map
        _transformSystem.OnGlobalMoveEvent += OnEntityMove;
    }
}
