using LaVerace.ModBlockEntity;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace LaVerace.ModItem;

public static class Extensions
{
    public static InPizzaProperties GetInPizzaProperties(this ItemStack stack)
    {
        var pizzaProps = stack.ItemAttributes?["inPizzaProperties"]?.AsObject<InPizzaProperties>(null, stack.Collectible.Code.Domain);

        var containerFlag = false;
        var container = stack.Collectible as BlockLiquidContainerBase;
            
        if (stack.Collectible is BlockLiquidContainerBase)
        {
            pizzaProps = container.GetContent(stack)?.ItemAttributes?["inPizzaProperties"]?.AsObject<InPizzaProperties>(null);
            // LvCore.Logger.Warning("Container flag set");
            // LvCore.Logger.Warning("Container content: " + container.GetContent(stack));
            // LvCore.Logger.Warning("Container content props: " + container.GetContent(stack)?.ItemAttributes?["inPizzaProperties"]);
            containerFlag = true;
        }

        pizzaProps ??= InPizzaProperties.FromPie(stack.ItemAttributes?["inPieProperties"]
            ?.AsObject<InPieProperties>(null, stack.Collectible.Code.Domain));
            
        if (stack.Collectible.Code.ToString().Contains("cheese")) pizzaProps.PartType = EnumPizzaPartType.Cheese;
        return pizzaProps;
    }
}