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
    private ICoreAPI _api;

    public override void StartPre(ICoreAPI api)
    {
        _api = api;
        Modid = Mod.Info.ModID;
        Logger = Mod.Logger;
    }

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass($"{Modid}.{nameof(BlockPizza)}", typeof(BlockPizza));
        api.RegisterBlockEntityClass($"{Modid}.{nameof(BlockEntityPizza)}", typeof(BlockEntityPizza));
        api.RegisterItemClass($"{Modid}.{nameof(ItemPizzaDough)}", typeof(ItemPizzaDough));
        api.RegisterItemClass($"{Modid}.{nameof(ItemExpandedPlantableSeed)}", typeof(ItemExpandedPlantableSeed));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Logger.Notification("Hello from template mod server side: " + Lang.Get("laverace:hello"));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Logger.Notification("Hello from template mod client side: " + Lang.Get("laverace:hello"));
    }
}