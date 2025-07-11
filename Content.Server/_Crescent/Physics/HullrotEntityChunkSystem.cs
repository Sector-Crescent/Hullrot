/*
MIT
Copyright (c) 2025 MLGTASTICa

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
using System.Diagnostics.CodeAnalysis;
using System.Numerics;


namespace Content.Server._Crescent.Physics;

/// <summary>
/// This handles...
/// </summary>
public sealed class HullrotEntityChunkSystem : EntitySystem
{
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

    // Not needed to be a class rn , but in the future should be expanded to hold separate lists for fast query of constantly looked categories.
    public class HullrotChunk
    {
        public HashSet<EntityUid> containedEntities = new(chunkEntityMinimum);
    }

    // One of these is at worst 2 GB. You shouldn't have the need for multiple 64km X 64km maps though.
    // Each row has 16000 chunks , so it takes 0.128 MB to represent. So the minimum usage is exactly that
    public class HullrotChunkManager
    {
        // the starting point of this chunk
        public Vector2 bottomLeft;
        // holds all the chunks. EMpty rows if there are none on that row.
        public List<List<HullrotChunk?>?> chunks = new(minimumRowSize);

        public Vector2 getChunkKey(ref Vector2 targetPos)
        {
            return (targetPos - bottomLeft)/chunkSize;
        }

        public bool getChunk(Vector2 targetPos,[NotNullWhen(true)] out HullrotChunk? chunk)
        {
            chunk = null;
            Vector2 truePos = getChunkKey(ref targetPos);
            if (chunks[(int) truePos.X] is null)
                return false;
            if(chunks[(int) truePos.X]![(int) truePos.Y] is null)
                return false;
            chunk = chunks[(int) truePos.X]![(int) truePos.Y]!;
            return true;
        }

        // will initialize a chunk if it doesn't exist.
        public HullrotChunk initializeChunk(Vector2 targetPos)
        {
            Vector2 truePos = getChunkKey(ref targetPos);
            if (chunks[(int) truePos.X] is null)
            {
                chunks[(int) truePos.X] = new List<HullrotChunk?>(minimumRowSize);
            }
            if (chunks[(int) truePos.X]![(int) truePos.Y] is null)
            {
                chunks[(int) truePos.X]![(int) truePos.Y] = new HullrotChunk();
            }
            return chunks[(int) truePos.X]![(int) truePos.Y]!;
        }
    }

    /// <inheritdoc/>
    public override void Initialize()
    {

    }
}
