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

using System.Collections.Generic;
using LiveChartsCore.Drawing;
using LiveChartsCore.Geo;
using LiveChartsCore.Motion;
using SkiaSharp;

namespace LiveChartsCore.SkiaSharpView.Drawing.Geometries;

/// <summary>
/// Defines a path geometry with a specified color.
/// </summary>
/// <seealso cref="PathGeometry" />
public class HeatPathShape : PathGeometry, IHeatPathShape
{
    private readonly ColorMotionProperty _fillProperty;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeatPathShape"/> class.
    /// </summary>
    public HeatPathShape() : base()
    {
        _fillProperty = RegisterMotionProperty(new ColorMotionProperty(nameof(FillColor), LvcColor.Empty));
    }

    /// <summary>
    /// Gets or sets the color of the fill.
    /// </summary>
    /// <value>
    /// The color of the fill.
    /// </value>
    public LvcColor FillColor
    {
        get => _fillProperty.GetMovement(this);
        set => _fillProperty.SetMovement(value, this);
    }

    /// <inheritdoc cref="PathGeometry.Draw(SkiaSharpDrawingContext)"/>
    public override void Draw(SkiaSharpDrawingContext context)
    {
        if (_commands.Count == 0) return;

        var toRemoveSegments = new List<IPathCommand<SKPath>>();

        using var path = new SKPath();
        var isValid = true;

        foreach (var segment in _commands)
        {
            segment.IsValid = true;
            segment.Execute(path, CurrentTime, this);
            isValid = isValid && segment.IsValid;

            if (segment.IsValid && segment.RemoveOnCompleted) toRemoveSegments.Add(segment);
        }

        foreach (var segment in toRemoveSegments)
        {
            _ = _commands.Remove(segment);
            isValid = false;
        }

        if (IsClosed) path.Close();

        var originalColor = context.Paint.Color;
        var originalStyle = context.Paint.Style;

        var fill = FillColor;

        if (fill != LvcColor.Empty)
        {
            context.Paint.Color = fill.AsSKColor();
            context.Paint.Style = SKPaintStyle.Fill;
        }

        context.Canvas.DrawPath(path, context.Paint);

        if (fill != LvcColor.Empty)
        {
            context.Paint.Color = originalColor;
            context.Paint.Style = originalStyle;
        }

        if (!isValid) IsValid = false;
    }

    /// <inheritdoc cref="IAnimatable.CompleteTransition(string[])" />
    public override void CompleteTransition(params string[]? propertyName)
    {
        foreach (var item in _commands)
        {
            item.CompleteTransition(propertyName);
        }

        base.CompleteTransition(propertyName);
    }
}
