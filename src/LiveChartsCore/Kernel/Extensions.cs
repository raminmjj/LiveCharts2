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
using System.Linq;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;

namespace LiveChartsCore.Kernel;

/// <summary>
/// LiveCharts kerner extensions.
/// </summary>
public static class Extensions
{
    private const double Cf = 3d;

    /// <summary>
    /// Returns the left, top coordinate of the tooltip based on the found points, the position and the tooltip size.
    /// </summary>
    /// <param name="foundPoints"></param>
    /// <param name="position"></param>
    /// <param name="tooltipSize"></param>
    /// <param name="chartSize"></param>
    /// <returns></returns>
    public static LvcPoint? GetCartesianTooltipLocation(
        this IEnumerable<ChartPoint> foundPoints, TooltipPosition position, LvcSize tooltipSize, LvcSize chartSize)
    {
        var count = 0f;

        var placementContext = new TooltipPlacementContext();

        foreach (var point in foundPoints)
        {
            if (point.Context.HoverArea is null) continue;
            point.Context.HoverArea.SuggestTooltipPlacement(placementContext);
            count++;
        }

        if (count == 0) return null;

        if (placementContext.MostBottom > chartSize.Height - tooltipSize.Height)
            placementContext.MostBottom = chartSize.Height - tooltipSize.Height;
        if (placementContext.MostTop < 0) placementContext.MostTop = 0;

        var avrgX = (placementContext.MostRight + placementContext.MostLeft) / 2f - tooltipSize.Width * 0.5f;
        var avrgY = (placementContext.MostTop + placementContext.MostBottom) / 2f - tooltipSize.Height * 0.5f;

        return position switch
        {
            TooltipPosition.Top => new LvcPoint(avrgX, placementContext.MostTop - tooltipSize.Height),
            TooltipPosition.Bottom => new LvcPoint(avrgX, placementContext.MostBottom),
            TooltipPosition.Left => new LvcPoint(placementContext.MostLeft - tooltipSize.Width, avrgY),
            TooltipPosition.Right => new LvcPoint(placementContext.MostRight, avrgY),
            TooltipPosition.Center => new LvcPoint(avrgX, avrgY),
            TooltipPosition.Hidden => new LvcPoint(),
            _ => new LvcPoint(),
        };
    }

    /// <summary>
    ///  Returns the left, top coordinate of the tooltip based on the found points, the position and the tooltip size.
    /// </summary>
    /// <param name="foundPoints">The found points.</param>
    /// <param name="position">The position.</param>
    /// <param name="tooltipSize">Size of the tooltip.</param>
    /// <returns></returns>
    public static LvcPoint? GetPieTooltipLocation(
        this IEnumerable<ChartPoint> foundPoints, TooltipPosition position, LvcSize tooltipSize)
    {
        var placementContext = new TooltipPlacementContext();
        var found = false;

        foreach (var foundPoint in foundPoints)
        {
            if (foundPoint.Context.HoverArea is null) continue;
            foundPoint.Context.HoverArea.SuggestTooltipPlacement(placementContext);
            found = true;
            break; // we only care about the first one.
        }

        return found
            ? new LvcPoint(placementContext.PieX - tooltipSize.Width * 0.5f, placementContext.PieY - tooltipSize.Height * 0.5f)
            : null;
    }

    /// <summary>
    /// Gets the tick.
    /// </summary>
    /// <param name="axis">The axis.</param>
    /// <param name="controlSize">Size of the control.</param>
    /// <returns></returns>
    public static AxisTick GetTick(this ICartesianAxis axis, LvcSize controlSize)
    {
        return GetTick(axis, controlSize, axis.VisibleDataBounds);
    }

    /// <summary>
    /// Gets the tick.
    /// </summary>
    /// <param name="axis">The axis.</param>
    /// <param name="chart">The chart.</param>
    /// <returns></returns>
    public static AxisTick GetTick<TDrawingContext>(this IPolarAxis axis, PolarChart<TDrawingContext> chart)
        where TDrawingContext : DrawingContext
    {
        return GetTick(axis, chart, axis.VisibleDataBounds);
    }

    /// <summary>
    /// Gets the tick.
    /// </summary>
    /// <param name="axis">The axis.</param>
    /// <param name="controlSize">Size of the control.</param>
    /// <param name="bounds">The bounds.</param>
    /// <returns></returns>
    public static AxisTick GetTick(this ICartesianAxis axis, LvcSize controlSize, Bounds bounds)
    {
        var max = axis.MaxLimit is null ? bounds.Max : axis.MaxLimit.Value;
        var min = axis.MinLimit is null ? bounds.Min : axis.MinLimit.Value;

        var range = max - min;
        var separations = axis.Orientation == AxisOrientation.Y
            ? Math.Round(controlSize.Height / (12 * Cf), 0)
            : Math.Round(controlSize.Width / (20 * Cf), 0);
        var minimum = range / separations;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log(minimum) / Math.Log(10)));

        var residual = minimum / magnitude;
        var tick = residual > 5 ? 10 * magnitude : residual > 2 ? 5 * magnitude : residual > 1 ? 2 * magnitude : magnitude;
        return new AxisTick { Value = tick, Magnitude = magnitude };
    }

    /// <summary>
    /// Gets the tick.
    /// </summary>
    /// <param name="axis">The axis.</param>
    /// <param name="chart">The chart.</param>
    /// <param name="bounds">The bounds.</param>
    /// <returns></returns> 
    public static AxisTick GetTick<TDrawingContext>(this IPolarAxis axis, PolarChart<TDrawingContext> chart, Bounds bounds)
        where TDrawingContext : DrawingContext
    {
        var max = axis.MaxLimit is null ? bounds.Max : axis.MaxLimit.Value;
        var min = axis.MinLimit is null ? bounds.Min : axis.MinLimit.Value;

        var controlSize = chart.ControlSize;
        var minD = controlSize.Width < controlSize.Height ? controlSize.Width : controlSize.Height;
        var radius = minD - chart.InnerRadius;
        var c = minD * chart.TotalAnge / 360;

        var range = max - min;
        var separations = axis.Orientation == PolarAxisOrientation.Angle
            ? Math.Round(c / (10 * Cf), 0)
            : Math.Round(radius / (30 * Cf), 0);
        var minimum = range / separations;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log(minimum) / Math.Log(10)));

        var residual = minimum / magnitude;
        var tick = residual > 5 ? 10 * magnitude : residual > 2 ? 5 * magnitude : residual > 1 ? 2 * magnitude : magnitude;
        return new AxisTick { Value = tick, Magnitude = magnitude };
    }

    /// <summary>
    /// Creates a transition builder for the specified properties.
    /// </summary>
    /// <param name="animatable">The animatable.</param>
    /// <param name="properties">The properties, use null to apply the transition to all the properties.</param>
    /// <returns>The builder</returns>
    public static TransitionBuilder TransitionateProperties(this IAnimatable animatable, params string[]? properties)
    {
        return new TransitionBuilder(animatable, properties);
    }

    /// <summary>
    /// Determines whether is bar series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>
    ///   <c>true</c> if [is bar series] [the specified series]; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsBarSeries(this ISeries series)
    {
        return (series.SeriesProperties & SeriesProperties.Bar) != 0;
    }

    /// <summary>
    /// Determines whether is column series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>
    ///   <c>true</c> if [is column series] [the specified series]; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsColumnSeries(this ISeries series)
    {
        return (series.SeriesProperties & (SeriesProperties.Bar | SeriesProperties.PrimaryAxisVerticalOrientation)) != 0;
    }

    /// <summary>
    /// Determines whether is row series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>
    ///   <c>true</c> if [is row series] [the specified series]; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsRowSeries(this ISeries series)
    {
        return (series.SeriesProperties & (SeriesProperties.Bar | SeriesProperties.PrimaryAxisHorizontalOrientation)) != 0;
    }

    /// <summary>
    /// Determines whether is stacked series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>
    ///   <c>true</c> if [is stacked series] [the specified series]; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsStackedSeries(this ISeries series)
    {
        return (series.SeriesProperties & (SeriesProperties.Stacked)) != 0;
    }

    /// <summary>
    /// Determines whether is vertical series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>
    ///   <c>true</c> if [is vertical series] [the specified series]; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsVerticalSeries(this ISeries series)
    {
        return (series.SeriesProperties & (SeriesProperties.PrimaryAxisVerticalOrientation)) != 0;
    }

    /// <summary>
    /// Determines whether is horizontal series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>
    ///   <c>true</c> if [is horizontal series] [the specified series]; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsHorizontalSeries(this ISeries series)
    {
        return (series.SeriesProperties & (SeriesProperties.PrimaryAxisHorizontalOrientation)) != 0;
    }

    /// <summary>
    /// Determines whether is a financial series.
    /// </summary>
    /// <param name="series"></param>
    /// <returns></returns>
    public static bool IsFinancialSeries(this ISeries series)
    {
        return (series.SeriesProperties & SeriesProperties.Financial) != 0;
    }

    /// <summary>
    /// Calculates the tooltips finding strategy based on the series properties.
    /// </summary>
    /// <param name="seriesCollection">The series collection</param>
    /// <returns></returns>
    public static TooltipFindingStrategy GetTooltipFindingStrategy(this IEnumerable<ISeries> seriesCollection)
    {
        var areAllX = true;
        var areAllY = true;

        foreach (var series in seriesCollection)
        {
            areAllX = areAllX && (series.SeriesProperties & SeriesProperties.PrefersXStrategyTooltips) != 0;
            areAllY = areAllY && (series.SeriesProperties & SeriesProperties.PrefersYStrategyTooltips) != 0;
        }

        return areAllX
            ? TooltipFindingStrategy.CompareOnlyX
            : (areAllY ? TooltipFindingStrategy.CompareOnlyY : TooltipFindingStrategy.CompareAll);
    }

    /// <summary>
    /// Finds the closest point to the specified location in UI coordinates.
    /// </summary>
    /// <param name="points">The points to look in to.</param>bcv 
    /// <param name="point">The location.</param>
    /// <returns></returns>
    public static ChartPoint FindClosestTo(this IEnumerable<ChartPoint> points, LvcPoint point)
    {
        return _findClosestTo(points, point);
    }

    /// <summary>
    /// Finds the closest point to the specified location in UI coordinates.
    /// </summary>
    /// <param name="points">The points to look in to.</param>bcv 
    /// <param name="point">The location.</param>
    /// <returns></returns>
    public static ChartPoint<TModel, TVisual, TLabel> FindClosestTo<TModel, TVisual, TLabel>(
        this IEnumerable<ChartPoint> points, LvcPoint point)
    {
        return new ChartPoint<TModel, TVisual, TLabel>(_findClosestTo(points, point));
    }

    /// <summary>
    /// Gets a scaler for the given axis with the measured bounds (the target, the final dimension of the chart).
    /// </summary>
    /// <typeparam name="TDrawingContext"></typeparam>
    /// <param name="axis"></param>
    /// <param name="chart"></param>
    /// <returns></returns>
    public static Scaler GetNextScaler<TDrawingContext>(this ICartesianAxis axis, CartesianChart<TDrawingContext> chart)
        where TDrawingContext : DrawingContext
    {
        return new Scaler(chart.DrawMarginLocation, chart.DrawMarginSize, axis);
    }

    /// <summary>
    /// Gets a scaler that is built based on the dimensions of the chart at a given time, the scaler is built based on the
    /// animations that are happening in the chart at the moment this method is called.
    /// </summary>
    /// <typeparam name="TDrawingContext"></typeparam>
    /// <param name="axis"></param>
    /// <param name="chart"></param>
    /// <returns></returns>
    public static Scaler? GetActualScalerScaler<TDrawingContext>(this ICartesianAxis axis, CartesianChart<TDrawingContext> chart)
        where TDrawingContext : DrawingContext
    {
        return !axis.ActualBounds.HasPreviousState
            ? null
            : new Scaler(
                chart.ActualBounds.Location,
                chart.ActualBounds.Size,
                axis,
                new Bounds
                {
                    Max = axis.ActualBounds.MaxVisibleBound,
                    Min = axis.ActualBounds.MinVisibleBound
                });
    }

    private static ChartPoint _findClosestTo(this IEnumerable<ChartPoint> points, LvcPoint point)
    {
        var o = points.Select(p => new { distance = p.DistanceTo(point), point = p }).OrderBy(p => p.distance).ToArray();

        return o.First().point; //points.OrderBy(p => p.DistanceTo(point)).ToArray().First();
    }
}
