using LaVerace.ModBlock;
using LaVerace.ModBlockEntity;
using LaVerace.ModItem;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace LaVerace;

public class LvCore : ModSystem
{
    public static ILogger Logger;
    public static string Modid;

    public override void StartPre(ICoreAPI api)
    {
        Modid = Mod.Info.ModID;
        Logger = Mod.Logger;
    }

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass($"{Modid}.{nameof(BlockPizza)}", typeof(BlockPizza));
        api.RegisterBlockEntityClass($"{Modid}.{nameof(BlockEntityPizza)}", typeof(BlockEntityPizza));
        api.RegisterItemClass($"{Modid}.{nameof(ItemPizzaDough)}", typeof(ItemPizzaDough));
        Logger.Notification("Starting LaVerace");
    }

    public override void Dispose()
    {
        Logger = null;
        Modid = null;
        base.Dispose();
    }
}