using System.Collections.Generic;
using LaVerace.ModBlockEntity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LaVerace.ModBlock;

  public class BlockDoughForm : Block
  {
    private WorldInteraction[] interactions;
    private Cuboidf box = new Cuboidf(0.0f, 0.0f, 0.0f, 1f, 1f / 16f, 1f);

    public override void OnLoaded(ICoreAPI api)
    {
      base.OnLoaded(api);
      if (api.Side != EnumAppSide.Client)
        return;
      ICoreAPI coreApi = api;
      this.interactions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, "doughformBlockInteractions", (CreateCachableObjectDelegate<WorldInteraction[]>) (() =>
      {
        List<ItemStack> itemStackList = new List<ItemStack>();
        foreach (CollectibleObject collectible in api.World.Collectibles)
        {
          if (collectible is ItemClay)
            itemStackList.Add(new ItemStack(collectible));
        }
        return new WorldInteraction[]
        {
          new WorldInteraction()
          {
            ActionLangCode = $"{LvCore.Modid}:blockhelp-doughformform-adddough",
            HotKeyCode = (string) null,
            MouseButton = EnumMouseButton.Right,
            Itemstacks = itemStackList.ToArray(),
            GetMatchingStacks = new InteractionStacksDelegate(this.getMatchingStacks)
          },
          new WorldInteraction()
          {
            ActionLangCode = $"{LvCore.Modid}:blockhelp-doughform-removedough",
            HotKeyCode = (string) null,
            MouseButton = EnumMouseButton.Left,
            Itemstacks = itemStackList.ToArray(),
            GetMatchingStacks = new InteractionStacksDelegate(this.getMatchingStacks)
          }
        };
      }));
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
      return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
      LvCore.Logger.Warning("Interact end block");
      var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityDoughForm;
      be?.OnUseOver(byPlayer, blockSel);
      base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
    }

    private ItemStack[] getMatchingStacks(
      WorldInteraction wi,
      BlockSelection bs,
      EntitySelection es)
    {
      BlockEntityDoughForm blockEntity = this.api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityDoughForm;
      List<ItemStack> itemStackList = new List<ItemStack>();
      foreach (ItemStack itemstack in wi.Itemstacks)
      {
        if (blockEntity?.SelectedRecipe != null && blockEntity.SelectedRecipe.DoughType.SatisfiesAsIngredient(itemstack, false))
          itemStackList.Add(itemstack);
      }
      return itemStackList.ToArray();
    }

    public override BlockDropItemStack[] GetDropsForHandbook(
      ItemStack handbookStack,
      IPlayer forPlayer)
    {
      return new BlockDropItemStack[0];
    }

    public override ItemStack[] GetDrops(
      IWorldAccessor world,
      BlockPos pos,
      IPlayer byPlayer,
      float dropQuantityMultiplier = 1f)
    {
      return new ItemStack[0];
    }

    public override void OnNeighbourBlockChange(
      IWorldAccessor world,
      BlockPos pos,
      BlockPos neibpos)
    {
      --pos.Y;
      if (!world.BlockAccessor.GetMostSolidBlock(pos.X, pos.Y, pos.Z).CanAttachBlockAt(world.BlockAccessor, (Block) this, pos, BlockFacing.UP))
      {
        ++pos.Y;
        world.BlockAccessor.BreakBlock(pos, (IPlayer) null);
      }
      else
        ++pos.Y;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(
      IWorldAccessor world,
      BlockSelection selection,
      IPlayer forPlayer)
    {
      return this.interactions.Append<WorldInteraction>(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }
  }