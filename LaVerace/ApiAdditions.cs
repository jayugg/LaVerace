using System.Collections.Generic;
using Vintagestory.API.Common;

namespace LaVerace;

public static class ApiAdditions
{
    public static List<TableRecipe> GetTableRecipes(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<TableRecipeRegistry>().TableRecipes;
    }
}