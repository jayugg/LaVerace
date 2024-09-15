using System;
using System.Collections.Generic;
using System.Linq;
using LaVerace.ModBlockEntity;
using LaVerace.ModItem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LaVerace.ModBlock
{
    public class PizzaMeshCache : ModSystem, ITexPositionSource
    {
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }
        
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.capi = api;
        }

        ICoreClientAPI capi;
        Block mealtextureSourceBlock;

        AssetLocation[] pizzaShapeBySize = new AssetLocation[]
        {
            new AssetLocation($"{LvCore.Modid}:block/pizza/pizza1"),
            new AssetLocation($"{LvCore.Modid}:block/pizza/pizza2"),
            new AssetLocation($"{LvCore.Modid}:block/pizza/pizza3"),
            new AssetLocation($"{LvCore.Modid}:block/pizza/pizza4"),
        };

        private AssetLocation pizzaShape = new AssetLocation($"{LvCore.Modid}:block/pizza/pizza");

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        protected Shape nowTesselatingShape;

        BlockPizza nowTesselatingBlock;
        ItemStack[] contentStacks;
        AssetLocation baseTextureLoc;
        AssetLocation sauceTextureLoc;
        AssetLocation cheeseTextureLoc;
        AssetLocation[] toppingsTextureLocs;
        AssetLocation transparentTextureLoc = new AssetLocation("block/transparent");

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath = baseTextureLoc;
                if (textureCode == "sauce") texturePath = sauceTextureLoc;
                if (textureCode == "cheese") texturePath = cheeseTextureLoc;
                if (textureCode.Contains("topping"))
                {
                    int index = int.Parse(textureCode[7].ToString()) - 1;
                    texturePath = toppingsTextureLocs[index];
                }

                if (texturePath == null)
                {
                    LvCore.Logger.Warning("Missing texture path for pizza mesh texture code {0}, seems like a missing texture definition or invalid pizza block.", textureCode);
                    return capi.BlockTextureAtlas.UnknownTexturePosition;
                }

                TextureAtlasPosition texpos = capi.BlockTextureAtlas[texturePath];

                if (texpos == null)
                {
                    IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (texAsset != null)
                    {
                        BitmapRef bmp = texAsset.ToBitmap(capi);
                        capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texpos, () => bmp);
                    }
                    else
                    {
                        LvCore.Logger.Warning("Pizza mesh texture {1} not found.", nowTesselatingBlock.Code, texturePath);
                        texpos = capi.BlockTextureAtlas.UnknownTexturePosition;
                    }
                }
                return texpos;
            }
        }

        public MultiTextureMeshRef GetOrCreatePizzaMeshRef(ItemStack pizzaStack)
        {
            Dictionary<int, MultiTextureMeshRef> meshrefs;
            object obj;
            if (capi.ObjectCache.TryGetValue("pizzaMeshRefs", out obj))
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;
            else
                capi.ObjectCache["pizzaMeshRefs"] = meshrefs = new Dictionary<int, MultiTextureMeshRef>();
            if (pizzaStack == null) return null;
            ItemStack[] contents = (pizzaStack.Block as BlockPizza)?.GetContents(capi.World, pizzaStack);
            string extrakey = "ct" + "-bl" + pizzaStack.Attributes.GetAsInt("bakeLevel", 0) + "-ps" + pizzaStack.Attributes.GetAsInt("pizzaSize");
            int mealhashcode = GetMealHashCode(pizzaStack.Block, contents, null, extrakey);
            MultiTextureMeshRef mealMeshRef;
            if (!meshrefs.TryGetValue(mealhashcode, out mealMeshRef))
            {
                MeshData mesh = GetPizzaMesh(pizzaStack);
                if (mesh == null) return null;
                meshrefs[mealhashcode] = mealMeshRef = capi.Render.UploadMultiTextureMesh(mesh);
            }
            return mealMeshRef;
        }


        public MeshData GetPizzaMesh(ItemStack pizzaStack, ModelTransform transform = null)
        {
            // Slot 0: Base dough
            // Slot 1: Sauce
            // Slot 2: Cheese
            // Slot 3-5: Toppings

            nowTesselatingBlock = pizzaStack.Block as BlockPizza;
            if (nowTesselatingBlock == null) return null;  //This will occur if the pizzaStack changed to rot

            contentStacks = nowTesselatingBlock.GetContents(capi.World, pizzaStack);

            int pizzaSize = pizzaStack.Attributes.GetAsInt("pizzaSize");

            // At this spot we have to determine the textures for "dough" and "filling"
            // Texture determination rules:
            // 1. dough is simple: first itemstack must be dough, take from attributes
            // 2. pizza needs sauce
            // 3. pizza allows cheese
            // 4. pizza allows 3 items as toppings

            // Thus we need to determine the texture for the dough, sauce, cheese and toppings

            var stackprops = contentStacks.Select(stack => stack?.GetInPizzaProperties()).ToArray();

            int bakeLevel = pizzaStack.Attributes.GetAsInt("bakeLevel", 0);

            if (stackprops.Length == 0) return null;
            if (stackprops.Length < 6) stackprops = stackprops.Concat(new InPizzaProperties[6 - stackprops.Length]).ToArray();
            
            if (ContentsRotten(contentStacks))
            {
                baseTextureLoc = new AssetLocation("game:block/rot/rot");
                sauceTextureLoc = new AssetLocation("game:block/rot/rot");
                cheeseTextureLoc = new AssetLocation("game:block/rot/rot");
                toppingsTextureLocs = new AssetLocation[] { new ("game:block/rot/rot"), new ("game:block/rot/rot"), new ("game:block/rot/rot") };
            }
            else
            {
                if (stackprops[0] != null)
                {
                    baseTextureLoc = stackprops[0].Texture.Clone() ?? transparentTextureLoc;
                    baseTextureLoc.Path = baseTextureLoc.Path.Replace("{bakelevel}", "" + (bakeLevel + 1));
                    sauceTextureLoc = stackprops[1]?.Texture.Clone() ?? transparentTextureLoc;
                    cheeseTextureLoc = stackprops[2]?.Texture.Clone() ?? transparentTextureLoc;
                    toppingsTextureLocs = new AssetLocation[] {
                        stackprops[3]?.Texture.Clone() ?? transparentTextureLoc,
                        stackprops[4]?.Texture.Clone() ?? transparentTextureLoc,
                        stackprops[5]?.Texture.Clone() ?? transparentTextureLoc
                    };
                }
            }

            // AssetLocation shapeloc = pizzaShapeBySize[pizzaSize - 1];

            AssetLocation shapeloc = pizzaShape;

            shapeloc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape shape = Shape.TryGet(capi, shapeloc);
            MeshData mesh;
            
            string[] fillElements = new string[] { "base" };
            if (stackprops[1] != null) fillElements = fillElements.Concat(new [] {"salsa"}).ToArray();
            if (stackprops[2] != null) fillElements = fillElements.Concat(new [] {"mozzarella"}).ToArray();
            if (stackprops[3] != null || stackprops[4] != null || stackprops[5]!= null) fillElements = fillElements.Concat(new [] {"toppings"}).ToArray();

            // LvCore.Logger.Warning($"Filling elements: {string.Join(", ", fillElements)}");
            
            string[] selectiveElements = System.Array.Empty<string>();
            
            foreach (var element in fillElements)
            {
                for (int i = 0; i < pizzaSize; i++)
                {
                    // LvCore.Logger.Warning($"Adding element " + $"origin/quarter{i + 1}/" + element + $"{i + 1}/*");
                    selectiveElements = selectiveElements.Concat(new [] {$"origin/quarter{i + 1}/" + element + $"{i + 1}/*"}).ToArray();
                }
            }
            capi.Tesselator.TesselateShape("pizza", shape, out mesh, this, null, 0, 0, 0, null, selectiveElements);
            if (transform != null) mesh.ModelTransform(transform);
            return mesh;
        }
        
        public static bool ContentsRotten(ItemStack[] contentStacks)
        {
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i]?.Collectible.Code.Path == "rot") return true;
            }
            return false;
        }

        protected int GetMealHashCode(Block block, ItemStack[] contentStacks, Vec3f translate = null, string extraKey = null)
        {
            string shapestring = block.Shape.ToString() + block.Code.ToShortString();
            if (translate != null) shapestring += translate.X + "/" + translate.Y + "/" + translate.Z;

            string contentstring = "";
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null) continue;

                if (contentStacks[i].Collectible.Code.Path == "rot")
                {
                    return (shapestring + "rotten").GetHashCode();
                }

                contentstring += contentStacks[i].Collectible.Code.ToShortString();
            }

            return (shapestring + contentstring + extraKey).GetHashCode();
        }

    }
}