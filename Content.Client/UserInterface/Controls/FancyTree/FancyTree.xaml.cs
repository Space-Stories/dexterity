using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Resources;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Controls.FancyTree;

/// <summary>
///     Functionally similar to <see cref="Tree"/>, but with collapsible sections,
/// </summary>
[GenerateTypedNameReferences]
public sealed partial class FancyTree : Control
{
    [Dependency] private readonly IResourceCache _resCache = default!;

    public const string StylePropertyLineWidth = "LineWidth";
    public const string StylePropertyLineColor = "LineColor";
    public const string StylePropertyIconColor = "IconColor";
    public const string StylePropertyIconExpanded = "IconExpanded";
    public const string StylePropertyIconCollapsed = "IconCollapsed";
    public const string StylePropertyIconNoChildren = "IconNoChildren";

    public readonly List<TreeItem> Items = new();

    public event Action<TreeItem?>? OnSelectedItemChanged;

    public int? SelectedIndex { get; private set; }

    private bool _rowStyleUpdateQueued = true;

    /// <summary>
    ///     Whether or not to draw the lines connecting parents & children.
    /// </summary>
    public bool DrawLines = true;

    /// <summary>
    ///     Colour of the lines connecting parents & their child entries.
    /// </summary>
    public Color LineColor = Color.White;

    /// <summary>
    ///     Color used to modulate the icon textures.
    /// </summary>
    public Color IconColor = Color.White;

    /// <summary>
    ///     Width of the lines connecting parents & their child entries.
    /// </summary>
    public int LineWidth = 2;

    // If people ever want to customize this, this should be a style parameter/
    public const int Indentation = 16;

    public const string DefaultIconExpanded = "/Textures/Interface/Nano/inverted_triangle.svg.png";
    public const string DefaultIconCollapsed = "/Textures/Interface/Nano/triangle_right.png";
    public const string DefaultIconNoChildren = "/Textures/Interface/Nano/triangle_right_hollow.svg.png";

    public Texture? IconExpanded;
    public Texture? IconCollapsed;
    public Texture? IconNoChildren;

    /// <summary>
    ///     If true, tree entries will hide their icon if the texture is set to null. If the icon is hidden then the
    ///     text of that entry will no longer be aligned with sibling entries that do have an icon.
    /// </summary>
    public bool HideEmptyIcon
    {
        get => _hideEmptyIcon;
        set => SetHideEmptyIcon(value);
    }
    private bool _hideEmptyIcon;

    public TreeItem? SelectedItem => SelectedIndex == null ? null : Items[SelectedIndex.Value];

    /// <summary>
    ///     If true, a collapsed item will automatically expand when first selected. If false, it has to be manually expanded by
    ///     clicking on it a second time.
    /// </summary>
    public bool AutoExpand = true;

    public FancyTree()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        LoadIcons();
    }

    private void LoadIcons()
    {
        IconColor = TryGetStyleProperty(StylePropertyIconColor, out Color color) ? color : Color.White;

        if (!TryGetStyleProperty(StylePropertyIconExpanded, out IconExpanded))
            IconExpanded = _resCache.GetTexture(DefaultIconExpanded);

        if (!TryGetStyleProperty(StylePropertyIconCollapsed, out IconCollapsed))
            IconCollapsed = _resCache.GetTexture(DefaultIconCollapsed);

        if (!TryGetStyleProperty(StylePropertyIconNoChildren, out IconNoChildren))
            IconNoChildren = _resCache.GetTexture(DefaultIconNoChildren);

        foreach (var item in Body.Children)
        {
            RecursiveUpdateIcon((TreeItem) item);
        }
    }

    public TreeItem AddItem(TreeItem? parent = null)
    {
        if (parent != null)
        {
            if (parent.Tree != this)
                throw new ArgumentException("Parent must be owned by this tree.", nameof(parent));

            DebugTools.Assert(Items[parent.Index] == parent);
        }

        var item = new TreeItem()
        {
            Tree = this,
            Index = Items.Count,
        };

        Items.Add(item);
        item.Icon.SetSize = new Vector2(Indentation, Indentation);
        item.Button.OnPressed += (_) => OnPressed(item);

        if (parent == null)
            Body.AddChild(item);
        else
        {
            item.Padding.MinWidth = parent.Padding.MinWidth + Indentation;
            parent.Body.AddChild(item);
        }

        item.UpdateIcon();
        QueueRowStyleUpdate();
        return item;
    }

    private void OnPressed(TreeItem item)
    {
        if (SelectedIndex == item.Index)
        {
            item.SetExpanded(!item.Expanded);
            return;
        }

        SetSelectedIndex(item.Index);
    }

    public void SetSelectedIndex(int? value)
    {
        if (value == null || value < 0 || value >= Items.Count)
            value = null;

        if (SelectedIndex == value)
            return;

        SelectedItem?.SetSelected(false);
        SelectedIndex = value;

        var newSelection = SelectedItem;
        if (newSelection != null)
        {
            newSelection.SetSelected(true);
            if (AutoExpand && !newSelection.Expanded)
                newSelection.SetExpanded(true);
        }

        OnSelectedItemChanged?.Invoke(newSelection);
    }

    /// <summary>
    ///     Recursively expands or collapse all entries, optionally up to some depth.
    /// </summary>
    /// <param name="value">Whether to expand or collapse the entries</param>
    /// <param name="depth">The recursion depth. If negative, implies no limit. Zero will expand only the top-level entries.</param>
    public void SetAllExpanded(bool value, int depth = -1)
    {
        foreach (var item in Body.Children)
        {
            RecursiveSetExpanded((TreeItem) item, value, depth);
        }
    }

    public void RecursiveSetExpanded(TreeItem item, bool value, int depth)
    {
        item.SetExpanded(value);

        if (depth == 0)
            return;
        depth--;

        foreach (var child in item.Body.Children)
        {
            RecursiveSetExpanded((TreeItem) child, value, depth);
        }
    }

    public bool TryGetIndexFromMetadata(object metadata, [NotNullWhen(true)] out int? index)
    {
        index = null;
        foreach (var item in Items)
        {
            if (item.Metadata?.Equals(metadata) ?? false)
            {
                index = item.Index;
                break;
            }
        }
        return index != null;
    }

    public void ExpandParentEntries(int index)
    {
        Control? current = Items[index];
        while (current != null)
        {
            if (current is TreeItem item)
                item.SetExpanded(true);
            current = current.Parent;
        }
    }

    public void Clear()
    {
        foreach (var item in Items)
        {
            item.Dispose();
        }

        Items.Clear();
        Body.Children.Clear();
        SelectedIndex = null;
    }

    public void QueueRowStyleUpdate()
    {
        _rowStyleUpdateQueued = true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (!_rowStyleUpdateQueued)
            return;

        _rowStyleUpdateQueued = false;

        int index = 0;

        foreach (var item in Body.Children)
        {
            RecursivelyUpdateRowStyle((TreeItem) item, ref index);
        }
    }

    private void RecursivelyUpdateRowStyle(TreeItem item, ref int index)
    {
        if (int.IsOddInteger(index))
        {
            item.Button.RemoveStyleClass(TreeItem.StyleClassEvenRow);
            item.Button.AddStyleClass(TreeItem.StyleClassOddRow);
        }
        else
        {
            item.Button.AddStyleClass(TreeItem.StyleClassEvenRow);
            item.Button.RemoveStyleClass(TreeItem.StyleClassOddRow);
        }

        index++;

        if (!item.Expanded)
            return;

        foreach (var child in item.Body.Children)
        {
            RecursivelyUpdateRowStyle((TreeItem) child, ref index);
        }
    }

    private void SetHideEmptyIcon(bool value)
    {
        if (value == _hideEmptyIcon)
            return;

        _hideEmptyIcon = value;

        foreach (var item in Body.Children)
        {
            RecursiveUpdateIcon((TreeItem) item);
        }
    }

    private void RecursiveUpdateIcon(TreeItem item)
    {
        item.UpdateIcon();

        foreach (var child in item.Body.Children)
        {
            RecursiveUpdateIcon((TreeItem) child);
        }
    }

    protected override void StylePropertiesChanged()
    {
        LoadIcons();
        LineColor = TryGetStyleProperty(StylePropertyLineColor, out Color color) ? color: Color.White;
        LineWidth = TryGetStyleProperty(StylePropertyLineWidth, out int width) ? width : 2;
        base.StylePropertiesChanged();
    }
}
