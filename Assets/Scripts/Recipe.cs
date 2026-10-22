using UnityEngine;

public enum RecipeType
{
    Shaped,
    Shapeless
}

[CreateAssetMenu(fileName = "New Recipe", menuName = "Crafting/Recipe")]
public class Recipe: ScriptableObject
{
    public int[] pattern;
    public int outputItemId;
    public RecipeType recipeType = RecipeType.Shaped;
}