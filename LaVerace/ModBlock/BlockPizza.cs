using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ACulinaryArtillery;
using LaVerace.ModBlockEntity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LaVerace.ModBlock
{
    // Definition: GetContents() must always return a ItemStack[] of array length 6
    // [0] = base
    // [1] = sauce
    // [2] = cheese
    // [3-5] = toppings
    public class BlockPizza : BlockMeal, IBakeableCallback, ITablePlaceable
    {
        public string State => Variant["state"];
        protected override bool PlacedBlockEating => false;

        PizzaMeshCache ms;

        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            InteractionHelpYOffset = 0.375f;

            interactions = ObjectCacheUtil.GetOrCreate(api, "pizzaInteractions-", () =>
            {
                var knifeStacks = BlockUtil.GetKnifeStacks(api);
                List<ItemStack> fillStacks = new List<ItemStack>();
                List<ItemStack> doughStacks = new List<ItemStack>();

                if (fillStacks.Count == 0 && doughStacks.Count == 0)
                {
                    foreach (CollectibleObject obj in api.World.Collectibles)
                    {
                        if (obj is ItemDough)
                        {
                            doughStacks.Add(new ItemStack(obj, 2));
                        }

                        var pizzaProps = obj.Attributes?["inPizzaProperties"]?.AsObject<InPizzaProperties>(null, obj.Code.Domain);
                        if (pizzaProps != null && !(obj is ItemDough))
                        {
                            fillStacks.Add(new ItemStack(obj, 2));
                        }
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-pizza-cut",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = knifeStacks,
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityPizza bec = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPizza;
                            if (bec?.Inventory[0]?.Itemstack != null && (bec.Inventory[0].Itemstack.Collectible as BlockPizza)?.State != "raw" && bec.SlicesLeft > 1)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-pizza-addfilling",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fillStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityPizza bec = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPizza;
                            if (bec?.Inventory[0]?.Itemstack != null && (bec.Inventory[0].Itemstack.Collectible as BlockPizza)?.State == "raw" && !bec.HasAllFilling)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    }
                };
            });

            ms = api.ModLoader.GetModSystem<PizzaMeshCache>();
            displayContentsInfo = false;
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (!canEat(slot)) return;
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (!canEat(slot)) return false;

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (!canEat(slot)) return;

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }


        protected bool canEat(ItemSlot slot) {
            return
                slot.Itemstack.Attributes.GetAsInt("pizzaSize") == 1
                && State != "raw"
            ;
        }
        

        ModelTransform oneSliceTranformGui = new ModelTransform()
        {
            Origin = new Vec3f(0.375f, 0.1f, 0.375f),
            Scale = 2.82f,
            Rotation = new Vec3f(-27, 132, -5)
        }.EnsureDefaultValues();

        ModelTransform oneSliceTranformTp = new ModelTransform()
        {
            Translation = new Vec3f(-0.82f, -0.34f, -0.57f),
            Origin = new Vec3f(0.5f, 0.13f, 0.5f),
            Scale = 0.7f,
            Rotation = new Vec3f(-49, 29, -112)
        }.EnsureDefaultValues();


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            //base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            if (itemstack.Attributes.GetAsInt("pizzaSize") == 1)
            {
                if (target == EnumItemRenderTarget.Gui)
                {
                    renderinfo.Transform = oneSliceTranformGui;
                }
                if (target == EnumItemRenderTarget.HandTp)
                {
                    renderinfo.Transform = oneSliceTranformTp;
                }
            }

            renderinfo.ModelRef = ms.GetOrCreatePizzaMeshRef(itemstack);
        }
        
        public override MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos = null)
        {
            return ms.GetPizzaMesh(itemstack);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityPizza bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPizza;
            if (bec?.Inventory[0]?.Itemstack != null) return bec.Inventory[0].Itemstack.Clone();

            return base.OnPickBlock(world, pos);
        }

        public void OnBaked(ItemStack oldStack, ItemStack newStack)
        {
            // Copy over properties and bake the contents
            newStack.Attributes["contents"] = oldStack.Attributes["contents"];
            newStack.Attributes.SetInt("pizzaSize", oldStack.Attributes.GetAsInt("pizzaSize"));
            newStack.Attributes.SetInt("bakeLevel", oldStack.Attributes.GetAsInt("bakeLevel", 0) + 1);

            ItemStack[] stacks = GetContents(api.World, newStack);
            
            // 1. Cook contents, if there is a cooked version of it
            for (int i = 0; i < stacks.Length; i++)
            {
                CombustibleProperties props = stacks[i]?.Collectible?.CombustibleProps;
                if (props != null)
                {
                    ItemStack cookedStack = props.SmeltedStack?.ResolvedItemstack.Clone();

                    TransitionState state = UpdateAndGetTransitionState(api.World, new DummySlot(cookedStack), EnumTransitionType.Perish);

                    if (state != null)
                    {
                        if (cookedStack != null)
                        {
                            TransitionState smeltedState = cookedStack.Collectible.UpdateAndGetTransitionState(api.World, new DummySlot(cookedStack), EnumTransitionType.Perish);

                            float nowTransitionedHours = (state.TransitionedHours / (state.TransitionHours + state.FreshHours)) * 0.8f * (smeltedState.TransitionHours + smeltedState.FreshHours) - 1;

                            cookedStack.Collectible.SetTransitionState(cookedStack, EnumTransitionType.Perish, Math.Max(0, nowTransitionedHours));
                        }
                    }
                }
            }

            // Carry over and set perishable properties
            TransitionableProperties[] tprops = newStack.Collectible.GetTransitionableProperties(api.World, newStack, null);
            
            var perishProps = tprops.FirstOrDefault(p => p.Type == EnumTransitionType.Perish);
            if (perishProps != null)
            {
                perishProps.TransitionedStack.Resolve(api.World, "pizza perished stack");

                var inv = new DummyInventory(api, 6);
                inv[0].Itemstack = stacks[0];
                inv[1].Itemstack = stacks[1];
                inv[2].Itemstack = stacks[2];
                inv[3].Itemstack = stacks[3];
                inv[4].Itemstack = stacks[4];
                inv[5].Itemstack = stacks[5];

                CarryOverFreshness(api, inv.Slots, stacks, perishProps);
            }

            SetContents(newStack, stacks);
        }

        public void TryPlaceOnTable(EntityAgent byEntity, BlockSelection blockSel)
        {
            (this.api.World.GetBlock(new AssetLocation($"{LvCore.Modid}:pizza-raw")) as BlockPizza)?.TryPlacePizza(
                byEntity, blockSel);
        }

        public void TryPlacePizza(EntityAgent byEntity, BlockSelection blockSel)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer != null)
            {
                ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

                var pizzaprops = hotbarSlot.Itemstack.ItemAttributes["inPizzaProperties"]?.AsObject<InPizzaProperties>();
                if (pizzaprops == null || pizzaprops.PartType != EnumPizzaPartType.Base) return;
            }

            BlockPos abovePos = blockSel.Position.UpCopy();

            Block atBlock = api.World.BlockAccessor.GetBlock(abovePos);
            if (atBlock.Replaceable < 6000) return;

            api.World.BlockAccessor.SetBlock(Id, abovePos);

            BlockEntityPizza bepizza = api.World.BlockAccessor.GetBlockEntity(abovePos) as BlockEntityPizza;
            bepizza?.OnPlaced(byPlayer);
        }


        
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityPizza bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPizza;
            if (bec?.Inventory[0]?.Itemstack != null) return GetHeldItemName(bec.Inventory[0].Itemstack);

            return base.GetPlacedBlockName(world, pos);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            ItemStack[] cStacks = GetContents(api.World, itemStack);
            if (cStacks.Length <= 1) return Lang.Get("pizza-empty");

            ItemStack cstack = cStacks[1];

            if (cstack == null) return Lang.Get("pizza-empty");

            bool equal = true;
            for (int i = 2; equal && i < cStacks.Length - 1; i++)
            {
                if (cStacks[i] == null) continue;

                equal &= cstack.Equals(api.World, cStacks[i], GlobalConstants.IgnoredStackAttributes);
                cstack = cStacks[i];
            }

            string state = Variant["state"];

            if (MealMeshCache.ContentsRotten(cStacks))
            {
                return Lang.Get("pizza-single-rotten");
            }

            if (equal)
            {
                return Lang.Get("pizza-single-" + cstack.Collectible.Code.ToShortString() + "-" + state);
            } else
            {
                EnumFoodCategory fillingFoodCat =
                    cStacks[1].Collectible.NutritionProps?.FoodCategory
                    ?? cStacks[1].ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory
                    ?? EnumFoodCategory.Vegetable
                ;

                return Lang.Get("pizza-mixed-" + fillingFoodCat.ToString().ToLowerInvariant() + "-" + state);
            }           
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            int pizzaSie = inSlot.Itemstack.Attributes.GetAsInt("pizzaSize");
            ItemStack pizzaStack = inSlot.Itemstack;
            float servingsLeft = GetQuantityServings(world, inSlot.Itemstack);
            if (!inSlot.Itemstack.Attributes.HasAttribute("quantityServings")) servingsLeft = 1;

            if (pizzaSie == 1)
            {
                dsc.AppendLine(Lang.Get("pizza-slice-single", servingsLeft));
            } else
            {
                dsc.AppendLine(Lang.Get("pizza-slices", pizzaSie));
            }


            TransitionableProperties[] propsm = pizzaStack.Collectible.GetTransitionableProperties(api.World, pizzaStack, null);
            if (propsm != null && propsm.Length > 0)
            {
                pizzaStack.Collectible.AppendPerishableInfoText(inSlot, dsc, api.World);
            }

            ItemStack[] stacks = GetContents(api.World, pizzaStack);

            var forEntity = (world as IClientWorldAccessor)?.Player?.Entity;


            float[] nmul = GetNutritionHealthMul(null, inSlot, forEntity);
            dsc.AppendLine(GetContentNutritionFacts(api.World, inSlot, stacks, null, true, servingsLeft * nmul[0], servingsLeft * nmul[1]));
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockEntityPizza bep = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPizza;
            if (bep?.Inventory == null || bep.Inventory.Count < 1 || bep.Inventory.Empty) return "";

            BlockMeal mealblock = api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;

            ItemStack pizzaStack = bep.Inventory[0].Itemstack;
            ItemStack[] stacks = GetContents(api.World, pizzaStack);
            StringBuilder sb = new StringBuilder();

            TransitionableProperties[] propsm = pizzaStack.Collectible.GetTransitionableProperties(api.World, pizzaStack, null);
            if (propsm != null && propsm.Length > 0)
            {
                pizzaStack.Collectible.AppendPerishableInfoText(bep.Inventory[0], sb, api.World);
            }

            float servingsLeft = GetQuantityServings(world, bep.Inventory[0].Itemstack);
            if (!bep.Inventory[0].Itemstack.Attributes.HasAttribute("quantityServings")) servingsLeft = bep.SlicesLeft / 4f;

            float[] nmul = GetNutritionHealthMul(pos, null, forPlayer.Entity);

            if (mealblock != null)
                sb.AppendLine(mealblock.GetContentNutritionFacts(api.World, bep.Inventory[0], stacks, null, true,
                    nmul[0] * servingsLeft, nmul[1] * servingsLeft));

            return sb.ToString();
        }

        public override TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot inslot, EnumTransitionType type)
        {
            ItemStack[] cstacks = GetContents(world, inslot.Itemstack);
            UnspoilContents(world, cstacks);
            SetContents(inslot.Itemstack, cstacks);

            return base.UpdateAndGetTransitionState(world, inslot, type);
        }

        public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            ItemStack[] cstacks = GetContents(world, inslot.Itemstack);
            UnspoilContents(world, cstacks);
            SetContents(inslot.Itemstack, cstacks);

            
            return base.UpdateAndGetTransitionStatesNative(world, inslot);
        }


        public override string GetContentNutritionFacts(IWorldAccessor world, ItemSlot inSlotorFirstSlot, ItemStack[] contentStacks, EntityAgent forEntity, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
        {
            UnspoilContents(world, contentStacks);

            return base.GetContentNutritionFacts(world, inSlotorFirstSlot, contentStacks, forEntity, mulWithStacksize, nutritionMul, healthMul);
        }


        protected void UnspoilContents(IWorldAccessor world, ItemStack[] cstacks)
        {
            // Dont spoil the pizza contents, the pizza itself has a spoilage timer. Semi hacky fix reset their spoil timers each update
            
            for (int i = 0; i < cstacks.Length; i++)
            {
                ItemStack cstack = cstacks[i];
                if (cstack == null) continue;

                if (!(cstack.Attributes["transitionstate"] is ITreeAttribute))
                {
                    cstack.Attributes["transitionstate"] = new TreeAttribute();
                }
                ITreeAttribute attr = (ITreeAttribute)cstack.Attributes["transitionstate"];

                if (attr.HasAttribute("createdTotalHours"))
                {
                    attr.SetDouble("createdTotalHours", world.Calendar.TotalHours);
                    attr.SetDouble("lastUpdatedTotalHours", world.Calendar.TotalHours);
                    var transitionedHours = (attr["transitionedHours"] as FloatArrayAttribute)?.value;
                    for (int j = 0; transitionedHours != null && j < transitionedHours.Length; j++)
                    {
                        transitionedHours[j] = 0;
                    }
                }
            }
        }


        public override float[] GetNutritionHealthMul(BlockPos pos, ItemSlot slot, EntityAgent forEntity)
        {
            float satLossMul = 1f;

            if (slot == null && pos != null)
            {
                BlockEntityPizza bep = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPizza;
                if (bep != null) slot = bep.Inventory[0];
            }

            if (slot != null)
            {
                TransitionState state = slot.Itemstack.Collectible.UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;
                satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
            }

            return new[] { Attributes["nutritionMul"].AsFloat(1) * satLossMul, satLossMul };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityPizza bep = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPizza;
            if (bep != null && !bep.OnInteract(byPlayer))
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            return true;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Don't call eating stuff from blockmeal
            //base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        public override int GetRandomContentColor(ICoreClientAPI capi, ItemStack[] stacks)
        {
            ItemStack[] cstacks = GetContents(capi.World, stacks[0]);
            if (cstacks.Length == 0) return 0;

            ItemStack rndStack = cstacks[capi.World.Rand.Next(stacks.Length)];
            return rndStack.Collectible.GetRandomColor(capi, rndStack);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            var baseinteractions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            baseinteractions = baseinteractions.RemoveEntry(1);

            var allinteractions = interactions.Append(baseinteractions);
            return allinteractions;
        }
    }
}
