using System.Linq;
using System.Text;
using LaVerace.ModBlock;
using LaVerace.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace LaVerace.ModBlockEntity
{
    public enum EnumPizzaContentSlot
    {
        Base, Sauce, Cheese, Topping1, Topping2, Topping3
    }
    public enum EnumPizzaPartType
    {
        Base, Sauce, Cheese, Topping
    }
    public class InPizzaProperties
    {
        /// <summary>
        /// If true, allows mixing of the same nutritionprops food category
        /// </summary>
        public EnumPizzaPartType PartType;
        public AssetLocation Texture;
        public int Quantity = 1;
        public float QuantityLitres = 0.25f;
        
        public static InPizzaProperties FromPie (InPieProperties pieProps)
        {
            if (pieProps == null) return null;
            return new InPizzaProperties()
            {
                PartType = EnumPizzaPartType.Topping,
                Texture = pieProps.Texture,
                Quantity = 1,
                QuantityLitres = 0.25f
            };
        }
    }

    // Idea:
    // BlockEntityPizza is a single slot inventory BE that hold a pizza item stack
    // that pizza item stack is a container with always 6 slots:
    // [0] = base dough
    // [1] = sauce
    // [2] = cheese
    // [3-5] = topping
    // 
    // Eliminates the need to convert it to an itemstack once it's placed in inventory
    public class BlockEntityPizza : BlockEntityContainer
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "pizza";

        public bool HasAnyFilling
        {
            get
            {
                if (inv[0].Itemstack.Block is not BlockPizza pizzaBlock) return false;
                ItemStack[] cStacks = pizzaBlock.GetContents(Api.World, inv[0].Itemstack);
                return cStacks[1] != null || cStacks[2] != null || cStacks[3] != null || cStacks[4] != null || cStacks[5] != null;
            }
        }

        public bool HasAllFilling
        {
            get
            {
                if (inv[0].Itemstack.Block is not BlockPizza pizzaBlock) return false;
                ItemStack[] cStacks = pizzaBlock.GetContents(Api.World, inv[0].Itemstack);
                LvCore.Logger.Warning($"Checking filling: {cStacks.Length}");
                if (cStacks.Length < 6) return false;
                return cStacks[1] != null && cStacks[2] != null && cStacks[3] != null && cStacks[4] != null && cStacks[5] != null;
            }
        }
        
        public bool HasSauce
        {
            get
            {
                if (inv[0].Itemstack.Block is not BlockPizza pizzaBlock) return false;
                ItemStack[] cStacks = pizzaBlock.GetContents(Api.World, inv[0].Itemstack);
                return cStacks[1] != null;
            }
        }


        PizzaMeshCache ms;
        MeshData mesh;
        ICoreClientAPI capi;

        public BlockEntityPizza() : base()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ms = api.ModLoader.GetModSystem<PizzaMeshCache>();

            capi = api as ICoreClientAPI;

            loadMesh();
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);

            if (inv[0].Itemstack?.Collectible.Code.Path == "rot")
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
                Api.World.SpawnItemEntity(inv[0].Itemstack, Pos.ToVec3d().Add(0.5, 0.1, 0.5));
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack != null)
            {
                inv[0].Itemstack = byItemStack.Clone();
                inv[0].Itemstack.StackSize = 1;
                if (inv[0].Itemstack.Attributes.HasAttribute("quantityServings"))
                {
                    inv[0].Itemstack.Attributes.SetFloat("quantityServings", 0.25f);
                }

                var blockPizza = inv[0].Itemstack.Block as BlockPizza;
                var cStacks = blockPizza?.GetContents(Api.World, byItemStack);

                if (cStacks == null) return;
                if (cStacks.Length < 6)
                {
                    var paddedCStacks = new ItemStack[6];
                    for (var i = 0; i < cStacks.Length; i++)
                    {
                        paddedCStacks[i] = cStacks[i];
                    }
                    blockPizza.SetContents(inv[0].Itemstack, paddedCStacks);
                }
                else
                {
                    blockPizza.SetContents(inv[0].Itemstack, cStacks);
                }
            }
            loadMesh();
        }

        public int SlicesLeft { 
            get
            {
                if (inv[0].Empty) return 0;
                return inv[0].Itemstack.Attributes.GetAsInt("pizzaSize");
            }
        }

        public ItemStack TakeSlice()
        {
            if (inv[0].Empty) return null;

            int size = inv[0].Itemstack.Attributes.GetAsInt("pizzaSize");
            MarkDirty(true);

            ItemStack stack = inv[0].Itemstack.Clone();

            if (size <= 1)
            {
                if (!stack.Attributes.HasAttribute("quantityServings"))
                {
                    stack.Attributes.SetFloat("quantityServings", 0.25f);
                }
                inv[0].Itemstack = null;
                Api.World.BlockAccessor.SetBlock(0, Pos);
            }
            else
            {
                inv[0].Itemstack.Attributes.SetInt("pizzaSize", size - 1);

                stack.Attributes.SetInt("pizzaSize", 1);
                stack.Attributes.SetFloat("quantityServings", 0.25f);
            }

            stack.Attributes.SetBool("bakeable", false);

            loadMesh(); 
            MarkDirty(true);
            
            return stack;
        }

        public void OnPlaced(IPlayer byPlayer)
        {
            ItemStack doughStack = byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
            if (doughStack == null) return;

            inv[0].Itemstack = new ItemStack(Block);
            (inv[0].Itemstack.Block as BlockPizza)?.SetContents(inv[0].Itemstack, new ItemStack[6] { doughStack, null, null, null, null, null });
            inv[0].Itemstack.Attributes.SetInt("pizzaSize", 4);
            inv[0].Itemstack.Attributes.SetBool("bakeable", false);

            loadMesh();
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            var pizzaBlock = inv[0].Itemstack.Block as BlockPizza;

            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            EnumTool? tool = hotbarSlot?.Itemstack?.Collectible.Tool;
            if (tool == EnumTool.Knife || tool == EnumTool.Sword)
            {
                if (pizzaBlock != null && pizzaBlock.State != "raw")
                {
                    if (Api.Side == EnumAppSide.Server)
                    {
                        ItemStack slicestack = TakeSlice();
                        if (!byPlayer.InventoryManager.TryGiveItemstack(slicestack))
                        {
                            Api.World.SpawnItemEntity(slicestack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                        }
                    }

                }

                return true;
            }
            
            if (pizzaBlock != null && pizzaBlock.State != "raw")
            {
                return false;
            }

            // Filling rules:
            // 1. get inPizzaProperties
            // 2. any sauce there yet? if not, all good
            // 3. Is full: Can't add more.
            // 3. If partially full, can add

            if (hotbarSlot is { Empty: false })
            {
                bool added = TryAddIngredientFrom(hotbarSlot, byPlayer);
                if (added)
                {
                    loadMesh();
                    MarkDirty(true);
                }

                inv[0].Itemstack.Attributes.SetBool("bakeable", HasSauce);

                return added;
            } else
            {
                if (SlicesLeft == 1 && !inv[0].Itemstack.Attributes.HasAttribute("quantityServings"))
                {
                    inv[0].Itemstack.Attributes.SetBool("bakeable", false);
                    inv[0].Itemstack.Attributes.SetFloat("quantityServings", 0.25f);
                }

                if (Api.Side == EnumAppSide.Server)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(inv[0].Itemstack))
                    {
                        Api.World.SpawnItemEntity(inv[0].Itemstack, Pos.ToVec3d().Add(0.5, 0.25, 0.5));
                    }
                    this.inv[0].Itemstack = null;
                }

                Api.World.BlockAccessor.SetBlock(0, Pos);
            }

            return true;
        }

        private bool TryAddIngredientFrom(ItemSlot slot, IPlayer byPlayer = null)
        {
            var pizzaProps = slot.Itemstack.ItemAttributes?["inPizzaProperties"]?.AsObject<InPizzaProperties>(null, slot.Itemstack.Collectible.Code.Domain);
            
            var containerFlag = false;
            var container = slot.Itemstack.Collectible as BlockLiquidContainerBase;
            
            if (slot.Itemstack.Collectible is BlockLiquidContainerBase)
            {
                pizzaProps = container.GetContent(slot.Itemstack)?.ItemAttributes?["inPizzaProperties"]?.AsObject<InPizzaProperties>(null);
                LvCore.Logger.Warning("Container flag set");
                LvCore.Logger.Warning("Container content: " + container.GetContent(slot.Itemstack));
                LvCore.Logger.Warning("Container content props: " + container.GetContent(slot.Itemstack)?.ItemAttributes?["inPizzaProperties"]);
                containerFlag = true;
            }
            LvCore.Logger.Warning("PizzaProps: " + pizzaProps);

            pizzaProps ??= InPizzaProperties.FromPie(slot.Itemstack.ItemAttributes?["inPieProperties"]
                ?.AsObject<InPieProperties>(null, slot.Itemstack.Collectible.Code.Domain));
            
            if (pizzaProps == null)
            {
                if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "notpizzaable", Lang.Get("This item cannot be added to pizzas"));
                return false;
            }

            if (containerFlag && container.GetCurrentLitres(slot.Itemstack) < pizzaProps.QuantityLitres)
            {
                if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "notpizzaable", Lang.Get($"Need at least {pizzaProps.QuantityLitres} litres"));
                return false;
            }
            if (slot.Itemstack.StackSize < pizzaProps.Quantity)
            {
                if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "notpizzaable", Lang.Get($"Need at least {pizzaProps.Quantity} items"));
                return false;
            }

            var pizzaBlock = (inv[0].Itemstack.Block as BlockPizza);
            if (pizzaBlock == null) return false;

            ItemStack[] cStacks = pizzaBlock.GetContents(Api.World, inv[0].Itemstack);

            bool isFull = cStacks[1] != null && cStacks[2] != null && cStacks[3] != null && cStacks[4] != null && cStacks[5] != null;
            bool hasSauce = cStacks[1] != null;
            bool hasCheese = cStacks[2] != null;
            bool hasTopping = cStacks[3] != null || cStacks[4] != null || cStacks[5] != null;;

            if (!hasSauce && pizzaProps.PartType == EnumPizzaPartType.Sauce)
            {
                return AddIngredientFromSlot(slot, pizzaProps, EnumPizzaContentSlot.Sauce, pizzaBlock, containerFlag, byPlayer);
            }
            
            if (isFull)
            {
                if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "pizzafullfilling", Lang.Get("Can't add more filling - already completely filled pizza"));
                return false;
            }

            /*
            if (hasCheese && pizzaProps.PartType != EnumPizzaPartType.Topping)
            {
                if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "pizzaneedstopping", Lang.Get("Need to add a topping next"));
                return false;
            }
            */

            if (!hasCheese && pizzaProps.PartType == EnumPizzaPartType.Cheese)
            {
                return AddIngredientFromSlot(slot, pizzaProps, EnumPizzaContentSlot.Cheese, pizzaBlock, containerFlag, byPlayer);
            }

            if (!hasTopping)
            {
                return AddIngredientFromSlot(slot, pizzaProps, EnumPizzaContentSlot.Topping1, pizzaBlock, containerFlag, byPlayer);
            }
            if (cStacks[5] != null)
            {
                if (byPlayer != null && capi != null) capi.TriggerIngameError(this, "pizzafullfilling", Lang.Get("Can't add more filling - already completely filled pizza"));
            }
            int emptySlotIndex = 3 + (cStacks[3] != null ? 1 + (cStacks[4] != null ? 1 : 0) : 0);
            AddIngredientFromSlot(slot, pizzaProps, (EnumPizzaContentSlot) emptySlotIndex, pizzaBlock, containerFlag, byPlayer);
            return true;
        }

        private bool AddIngredientFromSlot(ItemSlot slot, InPizzaProperties pizzaProps, EnumPizzaContentSlot contentSlot, BlockPizza pizzaBlock, bool containerFlag, IPlayer byPlayer)
        {
            ItemStack[] cStacks = pizzaBlock.GetContents(Api.World, inv[0].Itemstack);
            if (containerFlag && slot.Itemstack.Collectible is BlockLiquidContainerBase container)
            {
                if (slot.Itemstack.Collectible is not ILiquidSource { AllowHeldLiquidTransfer: true })
                {
                    return false;
                }
                var quantity = pizzaProps.QuantityLitres;
                var cStack = container.GetContent(slot.Itemstack).Clone();
                var itemsPerLitre = container.GetContentProps(slot.Itemstack).ItemsPerLitre;
                var moved = (int)(quantity * itemsPerLitre);
                
                container.CallMethod<int>("splitStackAndPerformAction", byPlayer.Entity, slot, delegate (ItemStack stack)
                {
                    container.TryTakeContent(stack, moved);
                    return moved;
                });

                cStack.StackSize = moved;
                container.DoLiquidMovedEffects(byPlayer, cStack, moved, BlockLiquidContainerBase.EnumLiquidDirection.Pour);
                cStacks[(int) contentSlot] = cStack;
                pizzaBlock.SetContents(inv[0].Itemstack, cStacks);
                return true;
            }
            else
            {
                cStacks[(int) contentSlot] = slot.TakeOut(pizzaProps.Quantity);
                pizzaBlock.SetContents(inv[0].Itemstack, cStacks);
                return true;
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (inv[0].Empty) return true;
            mesher.AddMeshData(mesh);
            return true;
        }

        void loadMesh()
        {
            if (Api == null || Api.Side == EnumAppSide.Server || inv[0].Empty) return;
            mesh = ms.GetPizzaMesh(inv[0].Itemstack);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            bool isRotten = MealMeshCache.ContentsRotten(inv);
            if (isRotten)
            {
                dsc.Append(Lang.Get("Rotten"));
            }
            else
            {
                dsc.Append(BlockEntityShelf.PerishableInfoCompact(Api, inv[0], 0, false));
            }
        }
        
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                MarkDirty(true);
                loadMesh();
            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            //base.OnBlockBroken(); - dont drop inventory contents, the GetDrops() method already handles pizza dropping
        }
    }
}