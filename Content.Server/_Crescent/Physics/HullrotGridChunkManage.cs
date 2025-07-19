namespace Content.Server._Crescent.Physics;


/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class HullrotGridChunkManagerComponent : Component
{
    public int Width;
    public int Height;
    public Vector2i PositiveTileOffset;
    // maps a position on the list to the X , Y coordinates on a grid and attaches a list of all the entities there
    public List<List<EntityUid>?> posToEntities = new ();

    public int getKey(Vector2i gridPos)
    {
        gridPos += PositiveTileOffset;
        if (Width > Height)
            return gridPos.X * Width + gridPos.Y;
        return gridPos.Y * Height + gridPos.X;
    }
}
