using System;
using System.Collections.Generic;
using System.Linq;
using LaVerace.ModBlock;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LaVerace.ModBlockEntity;

public class BlockEntityDoughForm : BlockEntity
{
    private GuiDialog dlg;
    
    public int DoughAmount = 0;
    private string selectedRecipeCode = "something";
    private TableRecipe selectedRecipe;
    public TableRecipe SelectedRecipe => this.selectedRecipe;
    
    public void OpenDialog(IClientWorldAccessor world, BlockPos pos, ItemStack dough, ItemStack tool = null)
    {
        if (this.dlg != null && this.dlg.IsOpened())
            return;
        List<TableRecipe> recipes = world.Api.GetTableRecipes().Where((System.Func<TableRecipe, bool>) (r => r.DoughType.SatisfiesAsIngredient(dough) && r.OffHandTool == null || r.DoughType.SatisfiesAsIngredient(dough) && r.OffHandTool.SatisfiesAsIngredient(tool))).OrderBy((System.Func<TableRecipe, AssetLocation>) (r => r.Output.ResolvedItemstack.Collectible.Code)).ToList();
        List<ItemStack> list = recipes.Select((System.Func<TableRecipe, ItemStack>) (r => r.Output.ResolvedItemstack)).ToList();
        ICoreClientAPI capi = world.Api as ICoreClientAPI;
        this.dlg = new GuiDialogBlockEntityRecipeSelector(Lang.Get("Select recipe"), list.ToArray(), (Action<int>) (selectedIndex =>
        {
            capi?.Logger.VerboseDebug("Select table from recipe {0}, have {1} recipes.", (object) selectedIndex, (object) recipes.Count);
            this.selectedRecipe = recipes[selectedIndex];
            this.selectedRecipeCode = this.selectedRecipe.Code;
            capi.Network.SendBlockEntityPacket(pos.X, pos.Y, pos.Z, 1001, SerializerUtil.Serialize<string>(recipes[selectedIndex].Code));
        }), (Action) (() => capi?.Network.SendBlockEntityPacket(pos.X, pos.Y, pos.Z, 1003, (byte[]) null)), pos, world.Api as ICoreClientAPI);
        this.dlg.OnClosed += new Action(this.dlg.Dispose);
        this.dlg.TryOpen();
    }
    
    public void OnBeginUse(IPlayer byPlayer, BlockSelection blockSel)
    {
        if (this.SelectedRecipe != null || this.Api.Side != EnumAppSide.Client)
            return;
        this.OpenDialog(this.Api.World as IClientWorldAccessor, this.Pos, byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack);
    }
    
    public void OnUseOver(
      IPlayer byPlayer,
      BlockSelection blockSel)
    {
      if (this.SelectedRecipe == null)
        return;
      if (this.Api.Side == EnumAppSide.Client)
        this.SendUseOverPacket();
      ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
      if (activeHotbarSlot.Itemstack == null)
        return;
      this.Api.World.FrameProfiler.Mark("doughform-regenmesh");
      this.Api.World.BlockAccessor.MarkBlockDirty(this.Pos);
      this.Api.World.BlockAccessor.MarkBlockEntityDirty(this.Pos);
      LvCore.Logger.Warning("DoughAmount: " + DoughAmount);
      if (this.DoughAmount == 0)
      {
        this.Api.World.BlockAccessor.SetBlock(0, this.Pos);
      }
      else
      {
        LvCore.Logger.Warning("Check if finished");
        LvCore.Logger.Warning("Api side: " + this.Api.Side);
        this.CheckIfFinished(byPlayer, blockSel);
        this.Api.World.FrameProfiler.Mark("doughform-checkfinished");
        this.MarkDirty();
      }
    }
    
    public void SendUseOverPacket()
    {
      LvCore.Logger.Warning("SendUseOverPacket");
      ((ICoreClientAPI) this.Api).Network.SendBlockEntityPacket(this.Pos.X, this.Pos.Y, this.Pos.Z, 1002);
      LvCore.Logger.Warning("Sent use over packet");
    }

    public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
    {
      LvCore.Logger.Warning("Received client packet, id: " + packetid);
      if (packetid == 1003)
      {
        if (this.selectedRecipe?.DoughType?.ResolvedItemstack != null)
        {
          var stack = this.selectedRecipe.DoughType.ResolvedItemstack.Clone();
          stack.StackSize = DoughAmount;
          this.Api.World.SpawnItemEntity(stack,
            this.Pos.ToVec3d().Add(0.5));
        }
        this.Api.World.BlockAccessor.SetBlock(0, this.Pos);
      }
      if (packetid == 1001)
      {
        string recipeCode = SerializerUtil.Deserialize<string>(data);
        TableRecipe tableRecipe = this.Api.GetTableRecipes().FirstOrDefault<TableRecipe>((System.Func<TableRecipe, bool>) (r => r.Code == recipeCode));
        if (tableRecipe == null)
        {
          this.Api.World.Logger.Error("Client tried to selected table recipe with id {0}, but no such recipe exists!", recipeCode);
          this.selectedRecipe = (TableRecipe) null;
          return;
        }
        this.selectedRecipe = tableRecipe;
        this.selectedRecipeCode = tableRecipe.Code;
        this.MarkDirty();
        this.Api.World.BlockAccessor.GetChunkAtBlockPos(this.Pos).MarkModified();
      }
      if (packetid != 1002)
        return;
      LvCore.Logger.Warning("Received use over packet");
      this.Api.World.FrameProfiler.Enter("doughform-useover");
      this.OnUseOver(player, player.CurrentBlockSelection);
      this.Api.World.FrameProfiler.Leave();
    }
    
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        this.setSelectedRecipe(tree.GetString("selectedRecipeCode", ""));
        this.DoughAmount = tree.GetInt("doughAmount");
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("selectedRecipeCode", this.selectedRecipeCode);
        tree.SetInt("doughAmount", this.DoughAmount);
    }
    
    public void CheckIfFinished(IPlayer byPlayer, BlockSelection blockSel)
    {
      if (!this.MatchesRecipe(DoughAmount) || this.Api.World is not IServerWorldAccessor)
        return;
      LvCore.Logger.Warning("Check if finished: " + DoughAmount);
      this.DoughAmount = 0;
      ItemStack itemStack = this.SelectedRecipe.Output.ResolvedItemstack.Clone();
      this.selectedRecipe = null;
      if (itemStack.StackSize == 1 && itemStack.Class == EnumItemClass.Block)
      {
        var selection = new BlockSelection() { Position = blockSel.Position.DownCopy(), Face = blockSel.Face };
        if (itemStack.Block is ITablePlaceable placeable)
        {
          byPlayer.Entity.World.BlockAccessor.SetBlock(0, this.Pos);
          byPlayer.Entity.World.BlockAccessor.RemoveBlockEntity(Pos);
          placeable.TryPlaceOnTable(byPlayer.Entity, selection);
        }
        if (itemStack.Block is BlockPie)
        {
          byPlayer.Entity.World.BlockAccessor.RemoveBlockEntity(Pos);
          byPlayer.Entity.World.BlockAccessor.SetBlock(0, this.Pos);
          (this.Api.World.GetBlock(new AssetLocation("pie-raw")) as BlockPie)?.TryPlacePie(byPlayer.Entity, selection);
        }
      }
      else
      {
        int num = 500;
        while (itemStack.StackSize > 0 && num-- > 0)
        {
          ItemStack itemstack = itemStack.Clone();
          itemstack.StackSize = Math.Min(itemStack.StackSize, itemStack.Collectible.MaxStackSize);
          itemStack.StackSize -= itemstack.StackSize;
          this.Api.Event.PushEvent("onitemclayformed", (IAttribute) new TreeAttribute()
          {
            ["itemstack"] = (IAttribute) new ItemstackAttribute(itemstack),
            ["byentityid"] = (IAttribute) new LongAttribute(byPlayer.Entity.EntityId)
          });
          if (byPlayer.InventoryManager.TryGiveItemstack(itemstack))
            this.Api.World.PlaySoundAt(new AssetLocation("sounds/player/collect"), byPlayer);
          else
            this.Api.World.SpawnItemEntity(itemstack, this.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }
        if (num <= 1)
          this.Api.World.Logger.Error("Tried to drop finished dough forming item but failed after 500 times?! Gave up doing so. Out stack was " + itemStack?.ToString());
        this.Api.World.BlockAccessor.SetBlock(0, this.Pos);
      }
    }

    private bool MatchesRecipe(int amount)
    {
      return amount >= selectedRecipe?.DoughType.Quantity;
    }

    protected void setSelectedRecipe(string newCode)
    {
        if (this.selectedRecipeCode == newCode && (this.selectedRecipe != null || newCode != ""))
            return;
        this.selectedRecipe = newCode != "" ? (this.Api != null ? this.Api.GetTableRecipes().FirstOrDefault<TableRecipe>((System.Func<TableRecipe, bool>) (r => r.Code == newCode)) : null) : null;
        this.selectedRecipeCode = newCode;
    }
}