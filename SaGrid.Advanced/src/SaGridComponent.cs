using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SolidAvalonia;
using SaGrid.Core;
using static SolidAvalonia.Solid;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace SaGrid;

public class SaGridComponent<TData> : Component
{
    private readonly SaGrid<TData> _saGrid;
    private (Func<SaGrid<TData>>, Action<SaGrid<TData>>)? _gridSignal;
    private (Func<int>, Action<int>)? _selectionSignal;
    private Grid? _rootGrid;
    private Border? _rootBorder;
    private ContentControl? _bodyHost;
    private ContentControl? _footerHost;
    private Control? _headerControl;
    private TextBox? _lastFocusedTextBox;
    private int _themeTick = 0;

    // Renderers
    private readonly SaGridHeaderRenderer<TData> _headerRenderer;
    private readonly SaGridBodyRenderer<TData> _bodyRenderer;
    private readonly SaGridFooterRenderer<TData> _footerRenderer;

    public SaGrid<TData> Grid => _gridSignal?.Item1() ?? throw new InvalidOperationException("Grid not initialized. Call Build() first.");

    public SaGridComponent(SaGrid<TData> saGrid) : base(true)
    {
        _saGrid = saGrid;
        
        // Initialize renderers
        _headerRenderer = new SaGridHeaderRenderer<TData>(tb => _lastFocusedTextBox = tb);
        _bodyRenderer = new SaGridBodyRenderer<TData>();
        _footerRenderer = new SaGridFooterRenderer<TData>();
        
        OnCreatedCore(); // 推送 reactive owner
        Initialize(); // 触发 Build()
    }

    protected override object Build()
    {
        Console.WriteLine("SaGridComponent.Build invoked");
        
        // Initialize reactive signals and wire SaGrid.Advanced to trigger them
        if (_gridSignal == null)
        {
            _gridSignal = CreateSignal(_saGrid);
            _selectionSignal = CreateSignal(0);

            var (gridGetter, gridSetter) = _gridSignal.Value;
            var (selectionGetter, selectionSetter) = _selectionSignal.Value;
            var counter = 0;

            _saGrid.SetUIUpdateCallback(() =>
            {
                counter++;
                gridSetter?.Invoke(_saGrid);
                selectionSetter?.Invoke(counter);
            });
        }

        // Build the root container once to avoid reparenting and keep header TextBoxes stable
        if (_rootGrid == null)
        {
            _rootGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto")
            };

            _headerControl = _headerRenderer.CreateHeader(_saGrid, () => Grid, _selectionSignal?.Item1);
            // React to theme changes by recreating header content
            _saGrid.SetUIUpdateCallback(() =>
            {
                // Update reactive signals as before
                var (gridGetter, gridSetter) = _gridSignal!.Value;
                var (selectionGetter, selectionSetter) = _selectionSignal!.Value;
                _themeTick++;
                gridSetter?.Invoke(_saGrid);
                selectionSetter?.Invoke(_themeTick);
            });
            if (_headerControl is Control hdrCtrl)
            {
                hdrCtrl.SetValue(Panel.ZIndexProperty, 1);
                if (hdrCtrl is Panel hdrPanel)
                {
                    hdrPanel.Background = Brushes.White;
                }
            }
            Avalonia.Controls.Grid.SetRow(_headerControl, 0);
            _rootGrid.Children.Add(_headerControl);

            _bodyHost = new ContentControl();
            Avalonia.Controls.Grid.SetRow(_bodyHost, 1);
            _rootGrid.Children.Add(_bodyHost);

            _footerHost = new ContentControl();
            Avalonia.Controls.Grid.SetRow(_footerHost, 2);
            _rootGrid.Children.Add(_footerHost);

            _rootBorder = new Border()
                .BorderThickness(1)
                .BorderBrush(Brushes.Gray)
                .Child(_rootGrid);

            // Initialize reactive body/footer content once so inner reactive cells have an owner
            _bodyHost.Content = Reactive(() =>
            {
                // Access signals to create reactive dependency
                var currentGrid = Grid; // uses _gridSignal
                var selTick = _selectionSignal?.Item1();
                return _bodyRenderer.CreateBody(currentGrid, () => Grid, _selectionSignal?.Item1);
            });

            _footerHost.Content = Reactive(() =>
            {
                var currentGrid = Grid;
                var selTick = _selectionSignal?.Item1();
                return _footerRenderer.CreateFooter(currentGrid);
            });
        }
        return _rootBorder!;
    }
}
