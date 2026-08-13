using System.Collections.Generic;

// Plain static class, not a MonoBehaviour - just shared state both spawners can read/write.
// Lane indices only mean the same thing across spawners if they use the same
// totalLanes/minY/maxY setup.
public static class LaneOccupancy
{
    private static readonly HashSet<int> occupiedLanes = new HashSet<int>();

    public static void Occupy(int lane) => occupiedLanes.Add(lane);
    public static void Free(int lane) => occupiedLanes.Remove(lane);
    public static bool IsOccupied(int lane) => occupiedLanes.Contains(lane);
}
