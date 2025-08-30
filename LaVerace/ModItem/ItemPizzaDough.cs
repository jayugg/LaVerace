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
    private ItemStack[] _tableStacks;

    public override void OnLoaded(ICoreAPI coreApi)
    {
      if (_tableStacks == null)
      {
        var itemStackList = new List<ItemStack>();
        foreach (var collectible in coreApi.World.Collectibles)
        {
          if (collectible is not Block block) continue;
          var attributes = block.Attributes;
          if ((attributes != null ? (attributes.IsTrue("pieFormingSurface") ? 1 : 0) : 0) != 0)
            itemStackList.Add(new ItemStack(collectible));
        }
        _tableStacks = itemStackList.ToArray();
      }
      base.OnLoaded(coreApi);
    }

    public override void OnUnloaded(ICoreAPI coreApi)
    {
      _tableStacks = null;
      base.OnUnloaded(coreApi);
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
        var attributes = this.api.World.BlockAccessor.GetBlock(blockSel.Position).Attributes;
        if ((attributes != null ? (attributes.IsTrue("pieFormingSurface") ? 1 : 0) : 0) != 0)
        {
          if (slot.StackSize >= 1)
            (this.api.World.GetBlock(new AssetLocation($"{LvCore.Modid}:pizza-raw")) as BlockPizza)?.TryPlacePizza(byEntity, blockSel);
          else if (this.api is ICoreClientAPI capi)
            capi.TriggerIngameError(this, "notpizzaable", Lang.Get("Need at least 1 dough"));
          handling = EnumHandHandling.PreventDefault;
          return;
        }
      }
      base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
      return new WorldInteraction[]
      {
        new()
        {
          ActionLangCode = $"{LvCore.Modid}:heldhelp-makepizza",
          Itemstacks = _tableStacks,
          HotKeyCode = "sneak",
          MouseButton = EnumMouseButton.Right
        }
      }.Append(base.GetHeldInteractionHelp(inSlot));
    }
  }