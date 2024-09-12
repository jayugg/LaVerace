using System;
using System.Collections.Generic;
using System.Linq;
using ACulinaryArtillery;
using LaVerace.ModBlock;
using LaVerace.ModBlockEntity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LaVerace.ModItem;

  public class ItemPizzaDough : ItemExpandedRawFood
  {
    private ItemStack[] tableStacks;
    private GuiDialog dlg;

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
            if ((attributes != null ? (attributes.IsTrue("pizzaFormingSurface") ? 1 : 0) : 0) != 0)
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
      if (blockSel == null)
        return;

      LvCore.Logger.Warning("Interact Start");
      
      var blockEntity1 = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityDoughForm;
      
      if (blockEntity1 != null && !(blockEntity1.SelectedRecipe?.DoughType?.SatisfiesAsIngredient(slot.Itemstack, false) ?? false)) return;

      var surfeceBlock = api.World.BlockAccessor.GetBlock(blockSel.Position);
      JsonObject attributes = surfeceBlock.Attributes;
      
      LvCore.Logger.Warning("Block: {0}", surfeceBlock.Code);
      
      if ((attributes != null ? (attributes.IsTrue("pieFormingSurface") ? 1 : 0) : 0) != 0 || surfeceBlock.Code.ToString().Contains("doughform"))
      {
        LvCore.Logger.Warning("Trying to place pie");
        IPlayer player = byEntity.World.PlayerByUid(byEntity is EntityPlayer entityPlayer ? entityPlayer.PlayerUID : (string) null);
        BlockPos blockPos = blockSel.Position.AddCopy(blockSel.Face);
        if (!byEntity.World.Claims.TryAccess(player, blockPos, EnumBlockAccessFlags.BuildOrBreak))
          slot.MarkDirty();
        else if (blockEntity1 != null)
        {
          slot.TakeOut(1);
          slot.MarkDirty();
          blockEntity1.DoughAmount += 1;
          blockEntity1.OnUseOver(player, blockSel);
          blockEntity1.MarkDirty();
        }
        else
        {
          IWorldAccessor world = byEntity.World;
          Block block = world.GetBlock(new AssetLocation($"{LvCore.Modid}:doughform"));
          if (block == null)
            return;
          BlockPos pos = blockSel.Position.AddCopy(blockSel.Face).Down();
          if (!world.BlockAccessor.GetBlock(pos).CanAttachBlockAt(byEntity.World.BlockAccessor, block, pos, BlockFacing.UP) || !world.BlockAccessor.GetBlock(blockPos).IsReplacableBy(block))
            return;
          world.BlockAccessor.SetBlock(block.BlockId, blockPos);
          if (block.Sounds != null)
            world.PlaySoundAt(block.Sounds.Place, (double) blockSel.Position.X, (double) blockSel.Position.Y, (double) blockSel.Position.Z);
          if (byEntity.World.BlockAccessor.GetBlockEntity(blockPos) is BlockEntityDoughForm blockEntity2)
            blockEntity2.OnBeginUse(player, blockSel);
          handling = EnumHandHandling.PreventDefaultAction;
        }
      }
      base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }
    
    public override void OnHeldInteractStop(
      float secondsUsed,
      ItemSlot slot,
      EntityAgent byEntity,
      BlockSelection blockSel,
      EntitySelection entitySel)
    {
      if (blockSel == null || byEntity.World.BlockAccessor.GetBlock(blockSel.Position) == null || byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityDoughForm blockEntity)
        return;
      
      LvCore.Logger.Warning("Interact Stop");
      
      IPlayer player = (IPlayer) null;
      if (byEntity is EntityPlayer)
        player = byEntity.World.PlayerByUid(((EntityPlayer) byEntity).PlayerUID);
      if (player == null || !byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.Use))
        return;
      
      LvCore.Logger.Warning("Selected recipe: {0}", blockEntity.SelectedRecipe);
      if (blockEntity.SelectedRecipe.DoughType.ResolvedItemstack.Satisfies(slot.Itemstack))
      {
        slot.TakeOut(1);
        slot.MarkDirty();
        blockEntity.DoughAmount += 1;
      }
      if (!(byEntity.World is IClientWorldAccessor))
        return;
      blockEntity.OnUseOver(player, blockSel);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
      return new WorldInteraction[1]
      {
        new WorldInteraction()
        {
          ActionLangCode = "heldhelp-makepizza",
          Itemstacks = this.tableStacks,
          HotKeyCode = "sneak",
          MouseButton = EnumMouseButton.Right
        }
      }.Append(base.GetHeldInteractionHelp(inSlot));
    }
  }