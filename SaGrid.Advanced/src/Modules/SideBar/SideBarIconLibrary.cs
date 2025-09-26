using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace SaGrid.Advanced.Modules.SideBar;

internal static class SideBarIconLibrary
{
    private static readonly IReadOnlyDictionary<string, Func<Control>> IconFactories =
        new Dictionary<string, Func<Control>>(StringComparer.OrdinalIgnoreCase)
        {
            [SideBarIcons.Columns] = () => CreateGeometryIcon(StreamGeometry.Parse("M6 4 H10 V28 H6 Z M14 4 H18 V28 H14 Z M22 4 H26 V28 H22 Z")),
            [SideBarIcons.Info] = () => CreateGeometryIcon(StreamGeometry.Parse("M16 4 A12 12 0 1 1 15.99 4 Z M15 12 H17 V24 H15 Z M15 8 H17 V10 H15 Z")),
            [SideBarIcons.Filters] = () => CreateGeometryIcon(StreamGeometry.Parse("M6 4 H26 L18 16 V28 H14 V16 Z")),
            [SideBarIcons.Charts] = () => CreateGeometryIcon(StreamGeometry.Parse("M6 22 H10 V28 H6 Z M12 16 H16 V28 H12 Z M18 10 H22 V28 H18 Z M24 6 H28 V28 H24 Z"))
        };

    public static bool TryCreate(string key, out Control icon)
    {
        icon = null!;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (IconFactories.TryGetValue(key, out var factory))
        {
            icon = factory();
            return true;
        }

        if (TryCreateFromGeometry(key, out icon))
        {
            return true;
        }

        return false;
    }

    private static Control CreateGeometryIcon(Geometry geometry)
    {
        var path = new Path
        {
            Data = geometry,
            Stretch = Stretch.Uniform,
            Width = 20,
            Height = 20,
            Fill = Brushes.Gray
        };

        path.Bind(Shape.FillProperty, new Binding
        {
            Path = "Foreground",
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(ToggleButton)
            },
            FallbackValue = Brushes.Gray
        });

        return new Viewbox
        {
            Width = 24,
            Height = 24,
            Child = path
        };
    }

    private static bool TryCreateFromGeometry(string data, out Control icon)
    {
        icon = null!;

        try
        {
            var geometry = StreamGeometry.Parse(data);
            icon = CreateGeometryIcon(geometry);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public static class SideBarIcons
{
    public const string Columns = "columns";
    public const string Info = "info";
    public const string Filters = "filters";
    public const string Charts = "charts";
}
