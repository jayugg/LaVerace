using System.Collections.Generic;
using ACulinaryArtillery;
using LaVerace.ModBlock;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace LaVerace.ModItem;

  public class ItemPizzaDough : ItemExpandedRawFood
  {
    private ItemStack[] tableStacks;

    public override void OnLoaded(ICoreAPI api)
    {
      if (this.tableStacks == null)
      {
        List<ItemStack> itemStackList = new List<ItemStack>();
        foreach (CollectibleObject collectible in api.World.Collectibles)
        {
          if (collectible is Block block)
          {
            JsonObject attributes = block.Attributes;
            if ((attributes != null ? (attributes.IsTrue("pieFormingSurface") ? 1 : 0) : 0) != 0)
              itemStackList.Add(new ItemStack(collectible));
          }
        }
        this.tableStacks = itemStackList.ToArray();
      }
      base.OnLoaded(api);
    }

    public override void OnUnloaded(ICoreAPI api)
    {
      this.tableStacks = (ItemStack[]) null;
      base.OnUnloaded(api);
    }
    
    public override void OnHeldInteractStart(
      ItemSlot slot,
      EntityAgent byEntity,
      BlockSelection blockSel,
      EntitySelection entitySel,
      bool firstEvent,
      ref EnumHandHandling handling)
    {
      if (blockSel != null)
      {
        JsonObject attributes = this.api.World.BlockAccessor.GetBlock(blockSel.Position).Attributes;
        if ((attributes != null ? (attributes.IsTrue("pieFormingSurface") ? 1 : 0) : 0) != 0)
        {
          if (slot.StackSize >= 1)
            (this.api.World.GetBlock(new AssetLocation($"{LvCore.Modid}:pizza-raw")) as BlockPizza)?.TryPlacePizza(byEntity, blockSel);
          else if (this.api is ICoreClientAPI api)
            api.TriggerIngameError((object) this, "notpizzaable", Lang.Get("Need at least 1 dough"));
          handling = EnumHandHandling.PreventDefault;
          return;
        }
      }
      base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
      return new WorldInteraction[1]
      {
        new WorldInteraction()
        {
          ActionLangCode = $"{LvCore.Modid}:heldhelp-makepizza",
          Itemstacks = this.tableStacks,
          HotKeyCode = "sneak",
          MouseButton = EnumMouseButton.Right
        }
      }.Append(base.GetHeldInteractionHelp(inSlot));
    }
  }