using Vintagestory.API.Common;

namespace LaVerace.ModBlock;

public interface ITablePlaceable
{
    public void TryPlaceOnTable(EntityAgent byEntity, BlockSelection blockSel);
}