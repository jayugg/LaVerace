using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LaVerace;

  public class TableRecipe : IByteSerializable
  {
    public string Code = "";
    public CraftingRecipeIngredient DoughType;
    public CraftingRecipeIngredient OffHandTool;

    public CraftingRecipeIngredient[] Ingredients
    {
      get => new [] {DoughType, OffHandTool};
      set
      {
        if (value.Length > 2)
          throw new ArgumentException("Table recipes must have 1 or 2 ingredients");
        DoughType = value[0];
        OffHandTool = value[1];
      }
    }
    public JsonItemStack Output;

    public AssetLocation Name { get; set; }

    public bool Enabled { get; set; } = true;

    public void TryPlaceNow(ICoreAPI api, ItemSlot[] inputslots)
    {
      List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = this.PairInput(inputslots);
      foreach (KeyValuePair<ItemSlot, CraftingRecipeIngredient> keyValuePair in matched)
      {
        keyValuePair.Key.TakeOut(keyValuePair.Value.Quantity);
        keyValuePair.Key.MarkDirty();
      }
    }

    public bool Matches(IWorldAccessor worldForResolve, ItemSlot[] inputSlots)
    {
      List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = this.PairInput(inputSlots);
      return matched != null && this.Output != null;
    }

    public int Match(List<ItemStack> inputs)
    {
      List<CraftingRecipeIngredient> recipeIngredientList = new List<CraftingRecipeIngredient>();
      int num1 = -1;
      foreach (ItemStack input in inputs)
      {
        CraftingRecipeIngredient recipeIngredient = null;
        foreach (CraftingRecipeIngredient ingredient in this.Ingredients)
        {
          if ((ingredient.ResolvedItemstack != null || ingredient.IsWildCard) && !recipeIngredientList.Contains(ingredient) && ingredient.SatisfiesAsIngredient(input))
          {
            recipeIngredient = ingredient;
            break;
          }
        }
        if (recipeIngredient == null || input.StackSize % recipeIngredient.Quantity != 0 )
          return 0;
        int num2 = input.StackSize / recipeIngredient.Quantity;
        if (num2 <= 0)
          return 0;
        if (num1 == -1)
          num1 = num2;
        if (num2 != num1)
          return 0;
        recipeIngredientList.Add(recipeIngredient);
      }
      return num1;
    }

    private List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> PairInput(ItemSlot[] inputSlots)
    {
      List<int> intList = new List<int>();
      Queue<ItemSlot> itemSlotQueue = new Queue<ItemSlot>();
      foreach (ItemSlot inputSlot in inputSlots)
      {
        if (!inputSlot.Empty)
          itemSlotQueue.Enqueue(inputSlot);
      }
      if (itemSlotQueue.Count != 2)
        return null;
      List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> keyValuePairList = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();
      while (itemSlotQueue.Count > 0)
      {
        ItemSlot key = itemSlotQueue.Dequeue();
        bool flag = false;
        for (int index = 0; index < 2; ++index)
        {
          if (this.DoughType.SatisfiesAsIngredient(key.Itemstack) && !intList.Contains(index))
          {
            keyValuePairList.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(key, this.DoughType));
            intList.Add(index);
            flag = true;
            break;
          }
          if (this.OffHandTool.SatisfiesAsIngredient(key.Itemstack) && !intList.Contains(index))
          {
            keyValuePairList.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(key, this.OffHandTool));
            intList.Add(index);
            flag = true;
            break;
          }
        }
        if (!flag)
          return null;
      }
      return keyValuePairList.Count != 2 ? null : keyValuePairList;
    }

    public string GetOutputName(IWorldAccessor world, BlockPos pos)
    {
      return Lang.Get($"{LvCore.Modid}:Will make {Output.ResolvedItemstack.Block.GetPlacedBlockName(world, pos)}");
    }

    public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
    {
      bool flag = true;
      flag &= this.DoughType.Resolve(world, sourceForErrorLogging);
      return flag & this.OffHandTool?.Resolve(world, sourceForErrorLogging) ?? true;
    }

    public void ToBytes(BinaryWriter writer)
    {
      LvCore.Logger.Warning("Writing code");
      writer.Write(this.Code);
      this.DoughType.ToBytes(writer);
      LvCore.Logger.Warning("Writing offhand tool");
      writer.Write(this.OffHandTool != null);
      this.OffHandTool?.ToBytes(writer);
      LvCore.Logger.Warning("Writing output block stack: {0}", this.Output.ResolvedItemstack?.Block?.Code);
      this.Output.ToBytes(writer);
    }

    public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
    {
      this.Code = reader.ReadString();
      this.DoughType = new CraftingRecipeIngredient();
      this.DoughType.FromBytes(reader, resolver);
      this.DoughType.Resolve(resolver, "Table Recipe (FromBytes)");
      if (reader.ReadBoolean())
      {
        this.OffHandTool = new CraftingRecipeIngredient();
        this.OffHandTool.FromBytes(reader, resolver);
        this.OffHandTool.Resolve(resolver, "Table Recipe (FromBytes)");
      }
      this.Output = new JsonItemStack();
      this.Output.FromBytes(reader, resolver.ClassRegistry);
      LvCore.Logger.Warning("Resolving output block stack to {0}", this.Output.ResolvedItemstack);
      this.Output.Resolve(resolver, "Simmer Recipe (FromBytes)");
    }

    public TableRecipe Clone()
    {
      return new TableRecipe()
      {
        Code = this.Code,
        Enabled = this.Enabled,
        Name = this.Name,
        DoughType = this.DoughType.Clone(),
        OffHandTool = this.OffHandTool.Clone(),
        Output = Output.Clone()
      };
    }
    
    public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
    {
      Dictionary<string, string[]> nameToCodeMapping = new Dictionary<string, string[]>();
      if (this.DoughType == null || this.Output == null)
        return nameToCodeMapping;
      foreach (CraftingRecipeIngredient ingredient in this.Ingredients)
      {
        if (this.Ingredients.Length != 0)
        {
          CraftingRecipeIngredient recipeIngredient = ingredient;
          if (recipeIngredient != null && recipeIngredient.Code.Path.Contains("*") && recipeIngredient.Name != null)
          {
            int startIndex = recipeIngredient.Code.Path.IndexOf("*", StringComparison.Ordinal);
            int num = recipeIngredient.Code.Path.Length - startIndex - 1;
            List<string> stringList = new List<string>();
            if (recipeIngredient.Type == EnumItemClass.Block)
            {
              for (int index = 0; index < world.Blocks.Count; ++index)
              {
                if (!(world.Blocks[index].Code == null) && !world.Blocks[index].IsMissing && WildcardUtil.Match(recipeIngredient.Code, world.Blocks[index].Code))
                {
                  string str1 = world.Blocks[index].Code.Path.Substring(startIndex);
                  string str2 = str1.Substring(0, str1.Length - num);
                  if (recipeIngredient.AllowedVariants == null || recipeIngredient.AllowedVariants.Contains(str2))
                    stringList.Add(str2);
                }
              }
            }
            else
            {
              for (int index = 0; index < world.Items.Count; ++index)
              {
                if (!(world.Items[index].Code == null) && !world.Items[index].IsMissing && WildcardUtil.Match(recipeIngredient.Code, world.Items[index].Code))
                {
                  string str3 = world.Items[index].Code.Path.Substring(startIndex);
                  string str4 = str3.Substring(0, str3.Length - num);
                  if (recipeIngredient.AllowedVariants == null || recipeIngredient.AllowedVariants.Contains(str4))
                    stringList.Add(str4);
                }
              }
            }
            nameToCodeMapping[recipeIngredient.Name] = stringList.ToArray();
          }
        }
      }
      return nameToCodeMapping;
    }
  }