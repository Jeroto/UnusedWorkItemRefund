using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using HarmonyLib;

namespace UnusedWorkItemRefund;

[HarmonyPatch(typeof(BlockEntityAnvil))]
public static class AnvilInteraction
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(BlockEntityAnvil), nameof(BlockEntityAnvil.ditchWorkItemStack))]
	public static void ReplaceWorkItemWithIngot(
		BlockEntityAnvil __instance,
		IPlayer? byPlayer = null)
	{
		if (__instance.WorkItemStack == null) return;
		if (__instance.SelectedRecipe == null) return;

		// both the ingot shape and plate shape are in the game as recipes, we'll just use those shapes
		SmithingRecipe? ingotRecipe = __instance.Api.GetSmithingRecipes().FirstOrDefault(r => r.Name == "game:Iron ingot");
		SmithingRecipe? plateRecipe = __instance.Api.GetSmithingRecipes().FirstOrDefault(r => r.Name == "game:plate");
		
		// if we have a work item and a recipe selected, we check if the work item is in the shape of an ingot
		// if it is, we do some stuff and clear the workspace to not allow any other items to be given
		ItemStack? ditchedStack = null;
		
		if (DoesWorkItemMatchRecipe(ref ingotRecipe.Voxels, ref __instance.Voxels, __instance.rotation))
		{
			// The GetBaseMaterial function returns an ingot as an item stack so we don't need to do any more work
			ditchedStack = __instance.WorkItemStack.Collectible.GetCollectibleInterface<IAnvilWorkable>()
				.GetBaseMaterial(__instance.WorkItemStack);
		}
		if (plateRecipe != null && DoesWorkItemMatchRecipe(ref plateRecipe.Voxels, ref __instance.Voxels, __instance.rotation))
		{
			// We need to construct a plate item, however
			String metalVariant = __instance.WorkItemStack.Collectible.Variant["metal"];
			String materialDomain = __instance.WorkItemStack.Collectible.Attributes?["baseMaterialDomain"].AsString("game");
			Item? plateItem = __instance.Api.World.GetItem(AssetLocation.Create("metalplate-" + metalVariant, materialDomain));

			if (plateItem == null)
			{
				throw new Exception(string.Format("Base material for {0} not found, there is no item with code 'metalplate-{1}'", __instance.WorkItemStack.Collectible.Code, metalVariant));
			}
			
			ditchedStack = new ItemStack(plateItem);
		}
		

		if (ditchedStack != null)
		{
			float temp =
				__instance.WorkItemStack.Collectible.GetTemperature(__instance.Api.World, __instance.WorkItemStack);
			ditchedStack.Collectible.SetTemperature(__instance.Api.World, ditchedStack, temp);
			
			if (byPlayer == null || !byPlayer.InventoryManager.TryGiveItemstack(ditchedStack))
			{
				__instance.Api.World.SpawnItemEntity(ditchedStack, __instance.Pos);
			}

			__instance.Api.World.Logger.Audit("{0} Took 1x{1} from Anvil at {2}.",
				byPlayer?.PlayerName,
				ditchedStack.Collectible.Code,
				__instance.Pos
			);

			// we need to use reflection to clear the workspace since the method is protected
			MethodInfo? clearWorkspaceMethod =
				typeof(BlockEntityAnvil).GetMethod("clearWorkSpace", BindingFlags.NonPublic | BindingFlags.Instance);
			
			clearWorkspaceMethod?.Invoke(__instance, null);
		}
	}

	private static bool[,,] RotateRecipeVoxels(ref bool[,,] recipeVoxels, int rotation)
	{
		if (rotation == 0) return recipeVoxels;
		
		bool[,,] prevInteration = (bool[,,])recipeVoxels.Clone();
		bool[,,] rotatedRecipeVoxels = new bool[recipeVoxels.GetLength(0), recipeVoxels.GetLength(1),
			recipeVoxels.GetLength(2)];
		
		// we rotate the recipe to match the work item rotation degrees
		for (int i = 0; i < rotation / 90; i++)
		{
			for (int x = 0; x < recipeVoxels.GetLength(0); x++)
			{
				for (int y = 0; y < recipeVoxels.GetLength(1); y++)
				{
					for (int z = 0; z < recipeVoxels.GetLength(2); z++)
					{
						rotatedRecipeVoxels[z, y, x] = prevInteration[16 - x - 1, y, z];
					}
				}
			}
			
			prevInteration = (bool[,,])rotatedRecipeVoxels.Clone();
		}

		return rotatedRecipeVoxels;
	}

	private static bool DoesWorkItemMatchRecipe(ref bool[,,] recipeVoxels, ref byte[,,] workItemVoxels, int rotation)
	{
		bool[,,] rotatedRecipeVoxels = RotateRecipeVoxels(ref recipeVoxels, rotation);
		
		for (int x = 0; x < 16; x++)
		{
			for (int y = 0; y < rotatedRecipeVoxels.GetLength(1); y++)
			{
				for (int z = 0; z < 16; z++)
				{
					byte desiredMat = (byte)(rotatedRecipeVoxels[x, y, z]
						? EnumVoxelMaterial.Metal
						: EnumVoxelMaterial.Empty);

					if (workItemVoxels[x, y, z] != desiredMat)
					{
						return false;
					}
				}
			}
		}
		return true;
	}
	
}