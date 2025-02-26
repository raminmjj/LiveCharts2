﻿// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Events;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.Drawing;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Essentials;
using Microsoft.Maui.Graphics;
using SkiaSharp.Views.Maui;

namespace LiveChartsCore.SkiaSharpView.Maui;

/// <inheritdoc cref="IPieChartView{TDrawingContext}"/>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class PieChart : ContentView, IPieChartView<SkiaSharpDrawingContext>, IMauiChart
{
    #region fields

    /// <summary>
    /// The core
    /// </summary>
    protected Chart<SkiaSharpDrawingContext>? core;
    private CollectionDeepObserver<ISeries> _seriesObserver;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="PieChart"/> class.
    /// </summary>
    /// <exception cref="Exception">Default colors are not valid</exception>
    public PieChart()
    {
        InitializeComponent();

        if (!LiveCharts.IsConfigured) LiveCharts.Configure(LiveChartsSkiaSharp.DefaultPlatformBuilder);

        var stylesBuilder = LiveCharts.CurrentSettings.GetTheme<SkiaSharpDrawingContext>();
        var initializer = stylesBuilder.GetVisualsInitializer();
        if (stylesBuilder.CurrentColors is null || stylesBuilder.CurrentColors.Length == 0)
            throw new Exception("Default colors are not valid");
        initializer.ApplyStyleToChart(this);

        InitializeCore();
        SizeChanged += OnSizeChanged;

        _seriesObserver = new CollectionDeepObserver<ISeries>(
           (object? sender, NotifyCollectionChangedEventArgs e) =>
           {
               if (core is null || (sender is IStopNPC stop && !stop.IsNotifyingChanges)) return;
               core.Update();
           },
           (object? sender, PropertyChangedEventArgs e) =>
           {
               if (core is null || (sender is IStopNPC stop && !stop.IsNotifyingChanges)) return;
               core.Update();
           });

        Series = new ObservableCollection<ISeries>();

        canvas.SkCanvasView.EnableTouchEvents = true;
        canvas.SkCanvasView.Touch += OnSkCanvasTouched;

        if (core is null) throw new Exception("Core not found!");
        core.Measuring += OnCoreMeasuring;
        core.UpdateStarted += OnCoreUpdateStarted;
        core.UpdateFinished += OnCoreUpdateFinished;
    }

    #region bindable properties

    /// <summary>
    /// The sync context property.
    /// </summary>
    public static readonly BindableProperty SyncContextProperty =
        BindableProperty.Create(
            nameof(SyncContext), typeof(object), typeof(PieChart), new ObservableCollection<ISeries>(), BindingMode.Default, null,
            (BindableObject o, object oldValue, object newValue) =>
            {
                var chart = (PieChart)o;
                chart.CoreCanvas.Sync = newValue;
                if (chart.core is null) return;
                chart.core.Update();
            });

    /// <summary>
    /// The series property
    /// </summary>
    public static readonly BindableProperty SeriesProperty =
          BindableProperty.Create(
              nameof(Series), typeof(IEnumerable<ISeries>), typeof(PieChart), new ObservableCollection<ISeries>(), BindingMode.Default, null,
              (BindableObject o, object oldValue, object newValue) =>
              {
                  var chart = (PieChart)o;
                  var seriesObserver = chart._seriesObserver;
                  seriesObserver?.Dispose((IEnumerable<ISeries>)oldValue);
                  seriesObserver.Initialize((IEnumerable<ISeries>)newValue);
                  if (chart.core is null) return;
                  chart.core.Update();
              });

    /// <summary>
    /// The initial rotation property
    /// </summary>
    public static readonly BindableProperty InitialRotationProperty =
        BindableProperty.Create(
            nameof(InitialRotation), typeof(double), typeof(CartesianChart), 0d, BindingMode.Default, null, OnBindablePropertyChanged);

    /// <summary>
    /// The maximum angle property
    /// </summary>
    public static readonly BindableProperty MaxAngleProperty =
        BindableProperty.Create(
            nameof(MaxAngle), typeof(double), typeof(CartesianChart), 360d, BindingMode.Default, null, OnBindablePropertyChanged);

    /// <summary>
    /// The total property
    /// </summary>
    public static readonly BindableProperty TotalProperty =
        BindableProperty.Create(
            nameof(Total), typeof(double?), typeof(CartesianChart), null, BindingMode.Default, null, OnBindablePropertyChanged);

    /// <summary>
    /// The draw margin property
    /// </summary>
    public static readonly BindableProperty DrawMarginProperty =
        BindableProperty.Create(
            nameof(DrawMargin), typeof(Margin), typeof(CartesianChart), null, BindingMode.Default, null, OnBindablePropertyChanged);

    /// <summary>
    /// The animations speed property
    /// </summary>
    public static readonly BindableProperty AnimationsSpeedProperty =
      BindableProperty.Create(
          nameof(AnimationsSpeed), typeof(TimeSpan), typeof(PieChart), LiveCharts.CurrentSettings.DefaultAnimationsSpeed);

    /// <summary>
    /// The easing function property
    /// </summary>
    public static readonly BindableProperty EasingFunctionProperty =
        BindableProperty.Create(
            nameof(EasingFunction), typeof(Func<float, float>), typeof(PieChart), LiveCharts.CurrentSettings.DefaultEasingFunction);

    /// <summary>
    /// The legend position property
    /// </summary>
    public static readonly BindableProperty LegendPositionProperty =
        BindableProperty.Create(
            nameof(LegendPosition), typeof(LegendPosition), typeof(CartesianChart),
            LiveCharts.CurrentSettings.DefaultLegendPosition, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The legend orientation property
    /// </summary>
    public static readonly BindableProperty LegendOrientationProperty =
        BindableProperty.Create(
            nameof(LegendOrientation), typeof(LegendOrientation), typeof(CartesianChart),
            LiveCharts.CurrentSettings.DefaultLegendOrientation, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The legend template property
    /// </summary>
    public static readonly BindableProperty LegendTemplateProperty =
        BindableProperty.Create(
            nameof(LegendTemplate), typeof(DataTemplate), typeof(CartesianChart), null, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The legend font family property
    /// </summary>
    public static readonly BindableProperty LegendFontFamilyProperty =
        BindableProperty.Create(
            nameof(LegendFontFamily), typeof(string), typeof(CartesianChart), null, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The legend font size property
    /// </summary>
    public static readonly BindableProperty LegendFontSizeProperty =
        BindableProperty.Create(
            nameof(LegendFontSize), typeof(double), typeof(CartesianChart), 13d, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The legend text color property
    /// </summary>
    public static readonly BindableProperty LegendTextBrushProperty =
        BindableProperty.Create(
            nameof(LegendTextBrush), typeof(Color), typeof(CartesianChart),
            Color.FromRgb(35 / 255d, 35 / 255d, 35 / 255d), propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The legend background property
    /// </summary>
    public static readonly BindableProperty LegendBackgroundProperty =
        BindableProperty.Create(
            nameof(LegendBackground), typeof(Color), typeof(CartesianChart),
            Color.FromRgb(250 / 255d, 250 / 255d, 250 / 255d), propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The legend font attributes property
    /// </summary>
    public static readonly BindableProperty LegendFontAttributesProperty =
        BindableProperty.Create(
            nameof(LegendFontAttributes), typeof(FontAttributes), typeof(CartesianChart),
            FontAttributes.None, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The tool tip position property;
    /// </summary>
    public static readonly BindableProperty TooltipPositionProperty =
       BindableProperty.Create(
           nameof(TooltipPosition), typeof(TooltipPosition), typeof(CartesianChart),
           LiveCharts.CurrentSettings.DefaultTooltipPosition, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The tool tip template property
    /// </summary>
    public static readonly BindableProperty TooltipTemplateProperty =
        BindableProperty.Create(
            nameof(TooltipTemplate), typeof(DataTemplate), typeof(CartesianChart), null, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The tool tip font family property
    /// </summary>
    public static readonly BindableProperty TooltipFontFamilyProperty =
        BindableProperty.Create(
            nameof(TooltipFontFamily), typeof(string), typeof(CartesianChart), null, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The tool tip font size property
    /// </summary>
    public static readonly BindableProperty TooltipFontSizeProperty =
        BindableProperty.Create(
            nameof(TooltipFontSize), typeof(double), typeof(CartesianChart), 13d, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The tool tip text color property
    /// </summary>
    public static readonly BindableProperty TooltipTextColorProperty =
        BindableProperty.Create(
            nameof(TooltipTextBrush), typeof(Color), typeof(CartesianChart),
            Color.FromRgb(35 / 255d, 35 / 255d, 35 / 255d), propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The tool tip background property
    /// </summary>
    public static readonly BindableProperty TooltipBackgroundProperty =
        BindableProperty.Create(
            nameof(TooltipBackground), typeof(Color), typeof(CartesianChart),
            Color.FromRgb(250 / 255d, 250 / 255d, 250 / 255d), propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The tool tip font attributes property
    /// </summary>
    public static readonly BindableProperty TooltipFontAttributesProperty =
        BindableProperty.Create(
            nameof(TooltipFontAttributes), typeof(FontAttributes), typeof(CartesianChart),
            FontAttributes.None, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The data pointer down command property
    /// </summary>
    public static readonly BindableProperty DataPointerDownCommandProperty =
        BindableProperty.Create(
            nameof(DataPointerDownCommand), typeof(ICommand), typeof(PieChart),
            null, propertyChanged: OnBindablePropertyChanged);

    /// <summary>
    /// The chart point pointer down command property
    /// </summary>
    public static readonly BindableProperty ChartPointPointerDownCommandProperty =
        BindableProperty.Create(
            nameof(ChartPointPointerDownCommand), typeof(ICommand), typeof(PieChart),
            null, propertyChanged: OnBindablePropertyChanged);

    #endregion

    #region events

    /// <inheritdoc cref="IChartView{TDrawingContext}.Measuring" />
    public event ChartEventHandler<SkiaSharpDrawingContext>? Measuring;

    /// <inheritdoc cref="IChartView{TDrawingContext}.UpdateStarted" />
    public event ChartEventHandler<SkiaSharpDrawingContext>? UpdateStarted;

    /// <inheritdoc cref="IChartView{TDrawingContext}.UpdateFinished" />
    public event ChartEventHandler<SkiaSharpDrawingContext>? UpdateFinished;

    /// <inheritdoc cref="IChartView.DataPointerDown" />
    public event ChartPointsHandler? DataPointerDown;

    /// <inheritdoc cref="IChartView.ChartPointPointerDown" />
    public event ChartPointHandler? ChartPointPointerDown;

    /// <summary>
    /// Called when the chart is touched.
    /// </summary>
    public event EventHandler<SKTouchEventArgs>? Touched;

    #endregion

    #region properties

    Grid IMauiChart.LayoutGrid => grid;
    BindableObject IMauiChart.Canvas => canvas;
    BindableObject IMauiChart.Legend => legend;

    /// <inheritdoc cref="IChartView.DesignerMode" />
    bool IChartView.DesignerMode => DesignMode.IsDesignModeEnabled;

    /// <inheritdoc cref="IChartView.CoreChart" />
    public IChart CoreChart => core ?? throw new Exception("Core not set yet.");

    LvcColor IChartView.BackColor
    {
        get => Background is not SolidColorBrush b
            ? new LvcColor()
            : LvcColor.FromArgb(
                (byte)(b.Color.Alpha * 255), (byte)(b.Color.Red * 255), (byte)(b.Color.Green * 255), (byte)(b.Color.Blue * 255));
        set => Background = new SolidColorBrush(Color.FromRgba(value.R / 255, value.G / 255, value.B / 255, value.A / 255));
    }

    PieChart<SkiaSharpDrawingContext> IPieChartView<SkiaSharpDrawingContext>.Core =>
        core is null ? throw new Exception("core not found") : (PieChart<SkiaSharpDrawingContext>)core;

    /// <inheritdoc cref="IChartView.SyncContext" />
    public object SyncContext
    {
        get => GetValue(SyncContextProperty);
        set => SetValue(SyncContextProperty, value);
    }

    LvcSize IChartView.ControlSize => new()
    {
        Width = (float)(canvas.Width * DeviceDisplay.MainDisplayInfo.Density),
        Height = (float)(canvas.Height * DeviceDisplay.MainDisplayInfo.Density)
    };

    /// <inheritdoc cref="IChartView{TDrawingContext}.CoreCanvas" />
    public MotionCanvas<SkiaSharpDrawingContext> CoreCanvas => canvas.CanvasCore;

    /// <inheritdoc cref="IChartView.DrawMargin" />
    public Margin? DrawMargin
    {
        get => (Margin)GetValue(DrawMarginProperty);
        set => SetValue(DrawMarginProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.Series" />
    public IEnumerable<ISeries> Series
    {
        get => (IEnumerable<ISeries>)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.InitialRotation" />
    public double InitialRotation
    {
        get => (double)GetValue(InitialRotationProperty);
        set => SetValue(InitialRotationProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.MaxAngle" />
    public double MaxAngle
    {
        get => (double)GetValue(MaxAngleProperty);
        set => SetValue(MaxAngleProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.Total" />
    public double? Total
    {
        get => (double?)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

    /// <inheritdoc cref="IChartView.AnimationsSpeed" />
    public TimeSpan AnimationsSpeed
    {
        get => (TimeSpan)GetValue(AnimationsSpeedProperty);
        set => SetValue(AnimationsSpeedProperty, value);
    }

    /// <inheritdoc cref="IChartView.EasingFunction" />
    public Func<float, float>? EasingFunction
    {
        get => (Func<float, float>)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    /// <inheritdoc cref="IChartView.LegendPosition" />
    public LegendPosition LegendPosition
    {
        get => (LegendPosition)GetValue(LegendPositionProperty);
        set => SetValue(LegendPositionProperty, value);
    }

    /// <inheritdoc cref="IChartView.LegendOrientation" />
    public LegendOrientation LegendOrientation
    {
        get => (LegendOrientation)GetValue(LegendOrientationProperty);
        set => SetValue(LegendOrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the legend template.
    /// </summary>
    /// <value>
    /// The legend template.
    /// </value>
    public DataTemplate LegendTemplate
    {
        get => (DataTemplate)GetValue(LegendTemplateProperty);
        set => SetValue(LegendTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the legend font family.
    /// </summary>
    /// <value>
    /// The legend font family.
    /// </value>
    public string LegendFontFamily
    {
        get => (string)GetValue(LegendFontFamilyProperty);
        set => SetValue(LegendFontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the size of the legend font.
    /// </summary>
    /// <value>
    /// The size of the legend font.
    /// </value>
    public double LegendFontSize
    {
        get => (double)GetValue(LegendFontSizeProperty);
        set => SetValue(LegendFontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the legend text.
    /// </summary>
    /// <value>
    /// The color of the legend text.
    /// </value>
    public Color LegendTextBrush
    {
        get => (Color)GetValue(LegendTextBrushProperty);
        set => SetValue(LegendTextBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the legend background.
    /// </summary>
    /// <value>
    /// The color of the legend background.
    /// </value>
    public Color LegendBackground
    {
        get => (Color)GetValue(LegendBackgroundProperty);
        set => SetValue(LegendBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the legend font attributes.
    /// </summary>
    /// <value>
    /// The legend font attributes.
    /// </value>
    public FontAttributes LegendFontAttributes
    {
        get => (FontAttributes)GetValue(LegendFontAttributesProperty);
        set => SetValue(LegendFontAttributesProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.Legend" />
    public IChartLegend<SkiaSharpDrawingContext>? Legend => legend;

    /// <inheritdoc cref="IChartView.TooltipPosition" />
    public TooltipPosition TooltipPosition
    {
        get => (TooltipPosition)GetValue(TooltipPositionProperty);
        set => SetValue(TooltipPositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the tool tip template.
    /// </summary>
    /// <value>
    /// The tool tip template.
    /// </value>
    public DataTemplate TooltipTemplate
    {
        get => (DataTemplate)GetValue(TooltipTemplateProperty);
        set => SetValue(TooltipTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the tool tip font family.
    /// </summary>
    /// <value>
    /// The tool tip font family.
    /// </value>
    public string TooltipFontFamily
    {
        get => (string)GetValue(TooltipFontFamilyProperty);
        set => SetValue(TooltipFontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the size of the tool tip font.
    /// </summary>
    /// <value>
    /// The size of the tool tip font.
    /// </value>
    public double TooltipFontSize
    {
        get => (double)GetValue(TooltipFontSizeProperty);
        set => SetValue(TooltipFontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the tool tip text.
    /// </summary>
    /// <value>
    /// The color of the tool tip text.
    /// </value>
    public Color TooltipTextBrush
    {
        get => (Color)GetValue(TooltipTextColorProperty);
        set => SetValue(TooltipTextColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the tool tip background.
    /// </summary>
    /// <value>
    /// The color of the tool tip background.
    /// </value>
    public Color TooltipBackground
    {
        get => (Color)GetValue(TooltipBackgroundProperty);
        set => SetValue(TooltipBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the tool tip font attributes.
    /// </summary>
    /// <value>
    /// The tool tip font attributes.
    /// </value>
    public FontAttributes TooltipFontAttributes
    {
        get => (FontAttributes)GetValue(TooltipFontAttributesProperty);
        set => SetValue(TooltipFontAttributesProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.Tooltip" />
    public IChartTooltip<SkiaSharpDrawingContext>? Tooltip => tooltip;

    /// <inheritdoc cref="IChartView{TDrawingContext}.AutoUpdateEnabled" />
    public bool AutoUpdateEnabled { get; set; } = true;

    /// <inheritdoc cref="IChartView.UpdaterThrottler" />
    public TimeSpan UpdaterThrottler
    {
        get => core?.UpdaterThrottler ?? throw new Exception("core not set yet.");
        set
        {
            if (core is null) throw new Exception("core not set yet.");
            core.UpdaterThrottler = value;
        }
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer goes down on a data or data points.
    /// </summary>
    public ICommand? DataPointerDownCommand
    {
        get => (ICommand?)GetValue(DataPointerDownCommandProperty);
        set => SetValue(DataPointerDownCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer goes down on a chart point.
    /// </summary>
    public ICommand? ChartPointPointerDownCommand
    {
        get => (ICommand?)GetValue(ChartPointPointerDownCommandProperty);
        set => SetValue(ChartPointPointerDownCommandProperty, value);
    }

    #endregion

    /// <inheritdoc cref="IChartView{TDrawingContext}.ShowTooltip(IEnumerable{ChartPoint})"/>
    public void ShowTooltip(IEnumerable<ChartPoint> points)
    {
        if (tooltip is null || core is null) return;

        ((IChartTooltip<SkiaSharpDrawingContext>)tooltip).Show(points, core);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.HideTooltip"/>
    public void HideTooltip()
    {
        if (tooltip is null || core is null) return;

        core.ClearTooltipData();
        ((IChartTooltip<SkiaSharpDrawingContext>)tooltip).Hide();
    }

    /// <inheritdoc cref="IChartView.SetTooltipStyle(LvcColor, LvcColor)"/>
    public void SetTooltipStyle(LvcColor background, LvcColor textColor)
    {
        TooltipBackground = Color.FromRgba(background.R, background.G, background.B, background.A);
        TooltipTextBrush = Color.FromRgba(textColor.R, textColor.G, textColor.B, textColor.A);
    }

    void IChartView.InvokeOnUIThread(Action action)
    {
        MainThread.BeginInvokeOnMainThread(action);
    }

    /// <inheritdoc cref="IChartView.SyncAction(Action)"/>
    public void SyncAction(Action action)
    {
        lock (CoreCanvas.Sync)
        {
            action();
        }
    }

    /// <summary>
    /// Initializes the core.
    /// </summary>
    /// <returns></returns>
    protected void InitializeCore()
    {
        core = new PieChart<SkiaSharpDrawingContext>(this, LiveChartsSkiaSharp.DefaultPlatformBuilder, canvas.CanvasCore);
        core.Update();
    }

    /// <summary>
    /// Called when a bindable property changes.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <param name="oldValue">The old value.</param>
    /// <param name="newValue">The new value.</param>
    /// <returns></returns>
    protected static void OnBindablePropertyChanged(BindableObject o, object oldValue, object newValue)
    {
        var chart = (PieChart)o;
        if (chart.core is null) return;
        chart.core.Update();
    }

    /// <inheritdoc cref="NavigableElement.OnParentSet"/>
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent == null)
        {
            core?.Unload();

            Series = Array.Empty<ISeries>();
            _seriesObserver = null!;

            return;
        }

        core?.Load();
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (core is null) return;
        core.Update();
    }

    private void OnSkCanvasTouched(object? sender, SKTouchEventArgs e)
    {
        if (core is null) return;

        if (TooltipPosition != TooltipPosition.Hidden)
        {
            var location = new LvcPoint(e.Location.X, e.Location.Y);
            core.InvokePointerDown(location);
            ((IChartTooltip<SkiaSharpDrawingContext>)tooltip).Show(core.FindHoveredPointsBy(location), core);
        }

        Touched?.Invoke(this, e);
    }

    private void OnCoreUpdateFinished(IChartView<SkiaSharpDrawingContext> chart)
    {
        UpdateFinished?.Invoke(this);
    }

    private void OnCoreUpdateStarted(IChartView<SkiaSharpDrawingContext> chart)
    {
        UpdateStarted?.Invoke(this);
    }

    private void OnCoreMeasuring(IChartView<SkiaSharpDrawingContext> chart)
    {
        Measuring?.Invoke(this);
    }

    void IChartView.OnDataPointerDown(IEnumerable<ChartPoint> points, LvcPoint pointer)
    {
        DataPointerDown?.Invoke(this, points);
        if (DataPointerDownCommand is not null && DataPointerDownCommand.CanExecute(points)) DataPointerDownCommand.Execute(points);

        var closest = points.FindClosestTo(pointer);
        ChartPointPointerDown?.Invoke(this, closest);
        if (ChartPointPointerDownCommand is not null && ChartPointPointerDownCommand.CanExecute(closest)) ChartPointPointerDownCommand.Execute(closest);
    }
}
