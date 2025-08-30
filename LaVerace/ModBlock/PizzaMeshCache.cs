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
            _capi = api;
        }

        private ICoreClientAPI _capi;
        private Block _mealtextureSourceBlock;

        private AssetLocation[] _pizzaShapeBySize =
        [
            new($"{LvCore.Modid}:block/pizza/pizza1"),
            new($"{LvCore.Modid}:block/pizza/pizza2"),
            new($"{LvCore.Modid}:block/pizza/pizza3"),
            new($"{LvCore.Modid}:block/pizza/pizza4")
        ];

        private AssetLocation _pizzaShape = new($"{LvCore.Modid}:block/pizza/pizza");

        public Size2i AtlasSize => _capi.BlockTextureAtlas.Size;
        protected Shape NowTesselatingShape;

        private BlockPizza _nowTesselatingBlock;
        private ItemStack[] _contentStacks;
        private AssetLocation _baseTextureLoc;
        private AssetLocation _sauceTextureLoc;
        private AssetLocation _cheeseTextureLoc;
        private AssetLocation[] _toppingsTextureLocs;
        private AssetLocation _transparentTextureLoc = new("block/transparent");

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                var texturePath = _baseTextureLoc;
                if (textureCode == "sauce") texturePath = _sauceTextureLoc;
                if (textureCode == "cheese") texturePath = _cheeseTextureLoc;
                if (textureCode.Contains("topping"))
                {
                    var index = int.Parse(textureCode[7].ToString()) - 1;
                    texturePath = _toppingsTextureLocs[index];
                }

                if (texturePath == null)
                {
                    LvCore.Logger.Warning("Missing texture path for pizza mesh texture code {0}, seems like a missing texture definition or invalid pizza block.", textureCode);
                    return _capi.BlockTextureAtlas.UnknownTexturePosition;
                }

                var texpos = _capi.BlockTextureAtlas[texturePath];

                if (texpos == null)
                {
                    var texAsset = _capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (texAsset != null)
                    {
                        var bmp = texAsset.ToBitmap(_capi);
                        _capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texpos, () => bmp);
                    }
                    else
                    {
                        LvCore.Logger.Warning("Pizza mesh texture {1} not found.", _nowTesselatingBlock.Code, texturePath);
                        texpos = _capi.BlockTextureAtlas.UnknownTexturePosition;
                    }
                }
                return texpos;
            }
        }

        public MultiTextureMeshRef GetOrCreatePizzaMeshRef(ItemStack pizzaStack)
        {
            Dictionary<int, MultiTextureMeshRef> meshrefs;
            object obj;
            if (_capi.ObjectCache.TryGetValue("pizzaMeshRefs", out obj))
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;
            else
                _capi.ObjectCache["pizzaMeshRefs"] = meshrefs = new Dictionary<int, MultiTextureMeshRef>();
            if (pizzaStack == null) return null;
            var contents = (pizzaStack.Block as BlockPizza)?.GetContents(_capi.World, pizzaStack);
            var extrakey = "ct" + "-bl" + pizzaStack.Attributes.GetAsInt("bakeLevel", 0) + "-ps" + pizzaStack.Attributes.GetAsInt("pizzaSize");
            var mealhashcode = GetMealHashCode(pizzaStack.Block, contents, null, extrakey);
            MultiTextureMeshRef mealMeshRef = null;
            if (meshrefs == null || meshrefs.TryGetValue(mealhashcode, out mealMeshRef)) return mealMeshRef;
            var mesh = GetPizzaMesh(pizzaStack);
            if (mesh == null) return null;
            meshrefs[mealhashcode] = mealMeshRef = _capi.Render.UploadMultiTextureMesh(mesh);
            return mealMeshRef;
        }


        public MeshData GetPizzaMesh(ItemStack pizzaStack, ModelTransform transform = null)
        {
            // Slot 0: Base dough
            // Slot 1: Sauce
            // Slot 2: Cheese
            // Slot 3-5: Toppings

            _nowTesselatingBlock = pizzaStack.Block as BlockPizza;
            if (_nowTesselatingBlock == null) return null;  //This will occur if the pizzaStack changed to rot

            _contentStacks = _nowTesselatingBlock.GetContents(_capi.World, pizzaStack);

            var pizzaSize = pizzaStack.Attributes.GetAsInt("pizzaSize");

            // At this spot we have to determine the textures for "dough" and "filling"
            // Texture determination rules:
            // 1. dough is simple: first itemstack must be dough, take from attributes
            // 2. pizza needs sauce
            // 3. pizza allows cheese
            // 4. pizza allows 3 items as toppings

            // Thus we need to determine the texture for the dough, sauce, cheese and toppings

            var stackprops = _contentStacks.Select(stack => stack?.GetInPizzaProperties()).ToArray();

            var bakeLevel = pizzaStack.Attributes.GetAsInt("bakeLevel", 0);

            if (stackprops.Length == 0) return null;
            if (stackprops.Length < 6) stackprops = stackprops.Concat(new InPizzaProperties[6 - stackprops.Length]).ToArray();
            
            if (ContentsRotten(_contentStacks))
            {
                _baseTextureLoc = new AssetLocation("game:block/rot/rot");
                _sauceTextureLoc = new AssetLocation("game:block/rot/rot");
                _cheeseTextureLoc = new AssetLocation("game:block/rot/rot");
                _toppingsTextureLocs = [new ("game:block/rot/rot"), new ("game:block/rot/rot"), new ("game:block/rot/rot")
                ];
            }
            else
            {
                if (stackprops[0] != null)
                {
                    _baseTextureLoc = stackprops[0].Texture.Clone() ?? _transparentTextureLoc;
                    _baseTextureLoc.Path = _baseTextureLoc.Path.Replace("{bakelevel}", "" + (bakeLevel + 1));
                    _sauceTextureLoc = stackprops[1]?.Texture.Clone() ?? _transparentTextureLoc;
                    _cheeseTextureLoc = stackprops[2]?.Texture.Clone() ?? _transparentTextureLoc;
                    _toppingsTextureLocs =
                    [
                        stackprops[3]?.Texture.Clone() ?? _transparentTextureLoc,
                        stackprops[4]?.Texture.Clone() ?? _transparentTextureLoc,
                        stackprops[5]?.Texture.Clone() ?? _transparentTextureLoc
                    ];
                }
            }

            // AssetLocation shapeloc = pizzaShapeBySize[pizzaSize - 1];

            var shapeloc = _pizzaShape;

            shapeloc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            var shape = Shape.TryGet(_capi, shapeloc);
            MeshData mesh;
            
            var fillElements = new string[] { "base" };
            if (stackprops[1] != null) fillElements = fillElements.Concat(["salsa"]).ToArray();
            if (stackprops[2] != null) fillElements = fillElements.Concat(["mozzarella"]).ToArray();
            if (stackprops[3] != null || stackprops[4] != null || stackprops[5]!= null) fillElements = fillElements.Concat(
                ["toppings"]).ToArray();

            // LvCore.Logger.Warning($"Filling elements: {string.Join(", ", fillElements)}");
            
            var selectiveElements = Array.Empty<string>();
            
            foreach (var element in fillElements)
            {
                for (var i = 0; i < pizzaSize; i++)
                {
                    // LvCore.Logger.Warning($"Adding element " + $"origin/quarter{i + 1}/" + element + $"{i + 1}/*");
                    selectiveElements = selectiveElements.Concat([$"origin/quarter{i + 1}/" + element + $"{i + 1}/*"]).ToArray();
                }
            }

            try
            {
                _capi.Tesselator.TesselateShape("pizza", shape, out mesh, this, null, 0, 0, 0, null, selectiveElements);
                if (transform != null) mesh.ModelTransform(transform);
                return mesh;
            }
            catch (Exception e)
            {
                LvCore.Logger.Error("Failed tesselating pizza mesh: {0}", e);
                LvCore.Logger.Error("Shape: {0}", shape);
                LvCore.Logger.Error("Selective elements: {0}", string.Join(", ", selectiveElements));
                return null;
            }
        }
        
        public static bool ContentsRotten(ItemStack[] contentStacks)
        {
            for (var i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i]?.Collectible.Code.Path == "rot") return true;
            }
            return false;
        }

        protected int GetMealHashCode(Block block, ItemStack[] contentStacks, Vec3f translate = null, string extraKey = null)
        {
            var shapestring = block.Shape.ToString() + block.Code.ToShortString();
            if (translate != null) shapestring += translate.X + "/" + translate.Y + "/" + translate.Z;

            var contentstring = "";
            for (var i = 0; i < contentStacks.Length; i++)
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