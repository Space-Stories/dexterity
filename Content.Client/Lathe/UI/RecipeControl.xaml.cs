using Content.Shared.Research.Prototypes;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Graphics;

namespace Content.Client.Lathe.UI;

[GenerateTypedNameReferences]
public sealed partial class RecipeControl : Control
{
    public Action<string>? OnButtonPressed;

    public string TooltipText;

    public RecipeControl(LatheRecipePrototype recipe, string tooltip, bool canProduce, Texture? texture = null)
    {
        RobustXamlLoader.Load(this);

        RecipeName.Text = recipe.Name;
        RecipeTexture.Texture = texture;
        TooltipText = tooltip;
        Button.Disabled = !canProduce;
        Button.ToolTip = tooltip;
        Button.TooltipSupplier = SupplyTooltip;

        Button.OnPressed += (_) =>
        {
            OnButtonPressed?.Invoke(recipe.ID);
        };
    }

    private Control? SupplyTooltip(Control sender)
    {
        return new RecipeTooltip(TooltipText);
    }
}
