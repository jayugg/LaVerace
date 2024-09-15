using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LaVerace.ModItem;

public class ItemExpandedPlantableSeed : ItemPlantableSeed
{
        public override void OnHeldInteractStart(
          ItemSlot itemslot,
          EntityAgent byEntity,
          BlockSelection blockSel,
          EntitySelection entitySel,
          bool firstEvent,
          ref EnumHandHandling handHandling)
        {
          if (blockSel == null)
            return;
          BlockPos position = blockSel.Position;
          string str = itemslot.Itemstack.Collectible.LastCodePart();
          if (str == "bellpepper")
            return;
          BlockEntity blockEntity = byEntity.World.BlockAccessor.GetBlockEntity(position);
          if (!(blockEntity is BlockEntityFarmland))
            return;
          Block block = byEntity.World.GetBlock(this.CodeWithPath("crop-" + str + "-1"));
          if (block == null)
          {
            str = this.Variant["type"] + "-" + this.Variant["variety"];
            block = byEntity.World.GetBlock(this.CodeWithPath("crop-" + str + "-1"));
            if (block == null) return;
          }
          IPlayer dualCallByPlayer = (IPlayer) null;
          if (byEntity is EntityPlayer)
            dualCallByPlayer = byEntity.World.PlayerByUid(((EntityPlayer) byEntity).PlayerUID);
          int num1 = ((BlockEntityFarmland) blockEntity).TryPlant(block) ? 1 : 0;
          if (num1 != 0)
          {
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), (double) position.X, (double) position.Y, (double) position.Z, dualCallByPlayer);
            if ((byEntity is EntityPlayer entityPlayer ? entityPlayer.Player : (IPlayer) null) is IClientPlayer player)
              player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            int num2;
            if (dualCallByPlayer == null)
            {
              num2 = 1;
            }
            else
            {
              EnumGameMode? currentGameMode = dualCallByPlayer.WorldData?.CurrentGameMode;
              EnumGameMode enumGameMode = EnumGameMode.Creative;
              num2 = !(currentGameMode.GetValueOrDefault() == enumGameMode & currentGameMode.HasValue) ? 1 : 0;
            }
            if (num2 != 0)
            {
              itemslot.TakeOut(1);
              itemslot.MarkDirty();
            }
          }
          if (num1 == 0)
            return;
          handHandling = EnumHandHandling.PreventDefault;
        }
}