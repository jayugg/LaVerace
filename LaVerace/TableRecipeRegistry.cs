using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace LaVerace;

 public class TableRecipeRegistry : ModSystem
  {
    public static bool CanRegister = true;
    public List<TableRecipe> TableRecipes = new List<TableRecipe>();

    public override double ExecuteOrder() => 1.0;

    public override void StartPre(ICoreAPI api) => CanRegister = true;

    public override void Start(ICoreAPI api)
    {
      this.TableRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<TableRecipe>>("tablerecipes").Recipes;
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
      if (!(api is ICoreServerAPI sapi))
        return;
      this.loadTableRecipes(sapi);
    }

    private void loadTableRecipes(ICoreServerAPI sapi)
    {
      Dictionary<AssetLocation, JToken> many = sapi.Assets.GetMany<JToken>(sapi.Server.Logger, "recipes/table");
      int quantityRegistered = 0;
      int quantityIgnored = 0;
      foreach (KeyValuePair<AssetLocation, JToken> keyValuePair in many)
      {
        if (keyValuePair.Value is JObject)
        {
          TableRecipe recipe = keyValuePair.Value.ToObject<TableRecipe>();
          LvCore.Logger.Warning("Table recipe {0} {1} {2} {3}", recipe.Code, recipe.DoughType?.Code, recipe.Output?.ResolvedItemstack?.Block?.Code, recipe.OffHandTool?.Code);
          if (recipe.Enabled)
            this.LoadTableRecipe(keyValuePair.Key, recipe, sapi, ref quantityRegistered, ref quantityIgnored);
          else
            continue;
        }
        if (keyValuePair.Value is JArray)
        {
          foreach (JToken jtoken in keyValuePair.Value as JArray)
          {
            TableRecipe recipe = jtoken.ToObject<TableRecipe>();
            if (recipe.Enabled)
              this.LoadTableRecipe(keyValuePair.Key, recipe, sapi, ref quantityRegistered, ref quantityIgnored);
          }
        }
      }
      sapi.World.Logger.Event("{0} table recipes loaded", (object) quantityRegistered);
      sapi.World.Logger.StoryEvent(Lang.Get($"{LvCore.Modid}:The red sauce and the white cheese..."));
    }
    
    private void LoadTableRecipe(
      AssetLocation path,
      TableRecipe recipe,
      ICoreServerAPI coreServerAPI,
      ref int quantityRegistered,
      ref int quantityIgnored)
    {
      if (!recipe.Enabled)
        return;
      if (recipe.Name == (AssetLocation) null)
        recipe.Name = path;
      string str = "table recipe";
      Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping((IWorldAccessor) coreServerAPI.World);
      if (nameToCodeMapping.Count > 0)
      {
        List<TableRecipe> tableRecipes = new List<TableRecipe>();
        int num = 0;
        bool flag1 = true;
        foreach (KeyValuePair<string, string[]> keyValuePair in nameToCodeMapping)
        {
          if (flag1)
            num = keyValuePair.Value.Length;
          else
            num *= keyValuePair.Value.Length;
          flag1 = false;
        }
        bool flag2 = true;
        foreach (KeyValuePair<string, string[]> keyValuePair in nameToCodeMapping)
        {
          string key = keyValuePair.Key;
          string[] strArray = keyValuePair.Value;
          for (int index = 0; index < num; ++index)
          {
            TableRecipe tableRecipe;
            if (flag2)
              tableRecipes.Add(tableRecipe = recipe.Clone());
            else
              tableRecipe = tableRecipes[index];
            if (tableRecipe.Ingredients != null)
            {
              foreach (CraftingRecipeIngredient ingredient in tableRecipe.Ingredients)
              {
                if (tableRecipe.Ingredients.Length != 0)
                {
                  CraftingRecipeIngredient recipeIngredient = ingredient;
                  if (recipeIngredient.Name == key)
                    recipeIngredient.Code = recipeIngredient.Code.CopyWithPath(recipeIngredient.Code.Path.Replace("*", strArray[index % strArray.Length]));
                }
              }
            }
            LvCore.Logger.Warning("Table recipe {0} {1} {2}", (object) tableRecipe.Code, (object) key, (object) strArray[index % strArray.Length]);
            LvCore.Logger.Warning("Fill placeholder arguments: {0} {1}", (object) keyValuePair.Key, (object) strArray[index % strArray.Length]);
            tableRecipe.Output.FillPlaceHolder(keyValuePair.Key, strArray[index % strArray.Length]);
            LvCore.Logger.Warning("Output: {0}", (object) tableRecipe.Output);
          }
          flag2 = false;
        }
        if (tableRecipes.Count == 0)
          coreServerAPI.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", (object) path, (object) str);
        foreach (TableRecipe tableRecipe in tableRecipes)
        {
          if (!tableRecipe.Resolve((IWorldAccessor) coreServerAPI.World, str + " " + path?.ToString()))
          {
            LvCore.Logger.Warning("Table recipe {0} ignored {1}, {2}", (object) tableRecipe.Code, tableRecipe.DoughType, tableRecipe.Output);
            ++quantityIgnored;
          }
          else
          {
            LvCore.Logger.Warning("Table recipe {0} registered {1}, {2}", (object) tableRecipe.Code, tableRecipe.DoughType, tableRecipe.Output);
            this.RegisterTableRecipe(tableRecipe);
            ++quantityRegistered;
          }
        }
      }
      else if (!recipe.Resolve((IWorldAccessor) coreServerAPI.World, str + " " + path?.ToString()))
      {
        LvCore.Logger.Warning("Table recipe {0} ignored {1}, {2}", (object) recipe.Code, recipe.DoughType?.Code, recipe.Output?.ResolvedItemstack?.Block?.Code);
        ++quantityIgnored;
      }
      else
      {
        LvCore.Logger.Warning("Table recipe {0} registered out {1}, {2}", (object) recipe.Code, recipe.DoughType?.Code, recipe.Output?.ResolvedItemstack?.Block?.Code);
        this.RegisterTableRecipe(recipe);
        ++quantityRegistered;
      }
    }
    
    public void RegisterTableRecipe(TableRecipe tableRecipe)
    {
      if (!CanRegister)
        throw new InvalidOperationException("Coding error: Can no long register table recipes. Register them during AssetsLoad/AssetsFinalize and with ExecuteOrder < 99999");
      this.TableRecipes.Add(tableRecipe);
    }
  }