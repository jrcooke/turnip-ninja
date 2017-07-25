using MountainView.Base;

namespace MountainView.ChunkManagement
{
    interface IChunkPointAccessor<T>
    {
        bool TryGetDataAtPoint(Angle lat, Angle lon, out T data);
        bool HasDataAtLat(Angle lat);
        bool HasDataAtLon(Angle lon);
    }
}
