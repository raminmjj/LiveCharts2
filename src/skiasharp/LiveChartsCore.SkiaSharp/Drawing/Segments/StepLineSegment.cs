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

using LiveChartsCore.Drawing;
using LiveChartsCore.Motion;
using SkiaSharp;

namespace LiveChartsCore.SkiaSharpView.Drawing.Segments;

/// <inheritdoc cref="IStepLineSegment{TPathContext}" />
public class StepLineSegment : PathCommand, IStepLineSegment<SKPath>
{
    private readonly FloatMotionProperty _x0Property;
    private readonly FloatMotionProperty _y0Property;
    private readonly FloatMotionProperty _x1Property;
    private readonly FloatMotionProperty _y1Property;

    /// <summary>
    /// Initializes a new instance of the <see cref="StepLineSegment"/> class.
    /// </summary>
    public StepLineSegment()
    {
        _x0Property = RegisterMotionProperty(new FloatMotionProperty(nameof(X0), 0f));
        _y0Property = RegisterMotionProperty(new FloatMotionProperty(nameof(Y0), 0f));
        _x1Property = RegisterMotionProperty(new FloatMotionProperty(nameof(X1), 0f));
        _y1Property = RegisterMotionProperty(new FloatMotionProperty(nameof(Y1), 0f));
    }

    /// <inheritdoc cref="IAnimatableBezierSegment.X0" />
    public float X0 { get => _x0Property.GetMovement(this); set => _x0Property.SetMovement(value, this); }

    /// <inheritdoc cref="IAnimatableBezierSegment.Y0" />
    public float Y0 { get => _y0Property.GetMovement(this); set => _y0Property.SetMovement(value, this); }

    /// <inheritdoc cref="IAnimatableBezierSegment.X1" />
    public float X1 { get => _x1Property.GetMovement(this); set => _x1Property.SetMovement(value, this); }

    /// <inheritdoc cref="IAnimatableBezierSegment.Y1" />
    public float Y1 { get => _y1Property.GetMovement(this); set => _y1Property.SetMovement(value, this); }

    /// <inheritdoc cref="IPathCommand{TPathContext}.Execute(TPathContext, long, Animatable)" />
    public override void Execute(SKPath path, long currentTime, Animatable pathGeometry)
    {
        CurrentTime = currentTime;
        path.LineTo(X0, Y0);
        path.LineTo(X1, Y1);
    }
}
