using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Slither.Client;

public sealed class VirtualControlsRenderer : IDisposable
{
    private const int CircleSegments = 48;
    private const float DigitHeightRatio = 0.055f;
    private const float ScoreBottomMarginRatio = 0.04f;
    private const float DigitWidthRatio = 0.56f;
    private const float DigitSpacingRatio = 0.24f;
    private const float SegmentThicknessRatio = 0.13f;

    private static readonly byte[] DigitSegments =
    [
        0b011_1111, // 0
        0b000_0110, // 1
        0b101_1011, // 2
        0b100_1111, // 3
        0b110_0110, // 4
        0b110_1101, // 5
        0b111_1101, // 6
        0b000_0111, // 7
        0b111_1111, // 8
        0b110_1111  // 9
    ];

    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _effect;
    private readonly VertexBuffer _circleBuffer;
    private readonly RasterizerState _rasterizerState = new() { CullMode = CullMode.None };

    public VirtualControlsRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _effect = new BasicEffect(graphicsDevice)
        {
            VertexColorEnabled = true,
            View = Matrix.Identity,
            LightingEnabled = false
        };

        var vertices = new VertexPositionColor[CircleSegments * 3];
        for (var segment = 0; segment < CircleSegments; segment++)
        {
            var startAngle = MathHelper.TwoPi * segment / CircleSegments;
            var endAngle = MathHelper.TwoPi * (segment + 1) / CircleSegments;
            var vertexIndex = segment * 3;
            vertices[vertexIndex] = new VertexPositionColor(Vector3.Zero, Color.White);
            vertices[vertexIndex + 1] = new VertexPositionColor(
                new Vector3(MathF.Cos(startAngle), MathF.Sin(startAngle), 0),
                Color.White);
            vertices[vertexIndex + 2] = new VertexPositionColor(
                new Vector3(MathF.Cos(endAngle), MathF.Sin(endAngle), 0),
                Color.White);
        }

        _circleBuffer = new VertexBuffer(
            graphicsDevice,
            VertexPositionColor.VertexDeclaration,
            vertices.Length,
            BufferUsage.WriteOnly);
        _circleBuffer.SetData(vertices);
    }

    public void Render(ScreenLayout layout, VirtualControls controls, int slitherSize)
    {
        var settings = layout.Settings;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(
            0,
            layout.ViewportWidth,
            layout.ViewportHeight,
            0,
            0,
            1);

        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = _rasterizerState;
        _graphicsDevice.SetVertexBuffer(_circleBuffer);

        RenderCircle(
            layout.JoystickBase,
            new Color(65, 155, 220),
            controls.JoystickActive ? settings.JoystickActiveAlpha : settings.JoystickInactiveAlpha);
        RenderCircle(
            new CircleRegion(controls.KnobPosition, layout.JoystickKnobRadius),
            new Color(180, 225, 255),
            controls.JoystickActive ? 0.80f : 0.48f);
        RenderCircle(
            layout.BoostButton,
            new Color(255, 150, 50),
            controls.Boost ? settings.BoostPressedAlpha : settings.BoostInactiveAlpha);

        RenderNumber(layout, slitherSize);
    }

    public void Dispose()
    {
        _circleBuffer.Dispose();
        _effect.Dispose();
        _rasterizerState.Dispose();
    }

    private void RenderCircle(CircleRegion circle, Color color, float alpha)
    {
        _effect.World = Matrix.CreateScale(circle.Radius, circle.Radius, 1) *
                        Matrix.CreateTranslation(circle.Center.X, circle.Center.Y, 0);
        _effect.DiffuseColor = color.ToVector3();
        _effect.Alpha = alpha;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, CircleSegments);
        }
    }

    private void RenderNumber(ScreenLayout layout, int value)
    {
        var text = Math.Max(0, value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var shortSide = Math.Min(layout.ViewportWidth, layout.ViewportHeight);
        var digitHeight = shortSide * DigitHeightRatio;
        var digitWidth = digitHeight * DigitWidthRatio;
        var spacing = digitWidth * DigitSpacingRatio;
        var thickness = digitHeight * SegmentThicknessRatio;
        var totalWidth = (text.Length * digitWidth) + ((text.Length - 1) * spacing);
        var origin = new Vector2(
            (layout.ViewportWidth - totalWidth) * 0.5f,
            layout.ViewportHeight - layout.SafeArea.Bottom -
            (shortSide * ScoreBottomMarginRatio) - digitHeight);
        var vertices = new List<VertexPositionColor>(text.Length * 7 * 6);
        var yellow = new Color(255, 220, 35, 235);

        for (var digitIndex = 0; digitIndex < text.Length; digitIndex++)
        {
            var digit = text[digitIndex] - '0';
            var mask = DigitSegments[digit];
            var x = origin.X + digitIndex * (digitWidth + spacing);
            AddDigit(vertices, mask, x, origin.Y, digitWidth, digitHeight, thickness, yellow);
        }

        _graphicsDevice.SetVertexBuffer(null);
        _effect.World = Matrix.Identity;
        _effect.DiffuseColor = Vector3.One;
        _effect.Alpha = 1;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                vertices.ToArray(),
                0,
                vertices.Count / 3);
        }
    }

    private static void AddDigit(
        List<VertexPositionColor> vertices,
        byte mask,
        float x,
        float y,
        float width,
        float height,
        float thickness,
        Color color)
    {
        var middleY = y + (height - thickness) * 0.5f;
        var rightX = x + width - thickness;
        var lowerY = middleY + thickness;
        var verticalHeight = (height - (3 * thickness)) * 0.5f;

        AddSegment(vertices, mask, 0, x + thickness, y, width - (2 * thickness), thickness, color);
        AddSegment(vertices, mask, 1, rightX, y + thickness, thickness, verticalHeight, color);
        AddSegment(vertices, mask, 2, rightX, lowerY, thickness, verticalHeight, color);
        AddSegment(vertices, mask, 3, x + thickness, y + height - thickness, width - (2 * thickness), thickness, color);
        AddSegment(vertices, mask, 4, x, lowerY, thickness, verticalHeight, color);
        AddSegment(vertices, mask, 5, x, y + thickness, thickness, verticalHeight, color);
        AddSegment(vertices, mask, 6, x + thickness, middleY, width - (2 * thickness), thickness, color);
    }

    private static void AddSegment(
        List<VertexPositionColor> vertices,
        byte mask,
        int bit,
        float x,
        float y,
        float width,
        float height,
        Color color)
    {
        if ((mask & (1 << bit)) == 0)
        {
            return;
        }

        Vector2 start;
        Vector2 end;
        float radius;
        if (width >= height)
        {
            radius = height * 0.5f;
            start = new Vector2(x + radius, y + radius);
            end = new Vector2(x + width - radius, y + radius);
        }
        else
        {
            radius = width * 0.5f;
            start = new Vector2(x + radius, y + radius);
            end = new Vector2(x + radius, y + height - radius);
        }

        AddCapsule(vertices, start, end, radius, color);
    }

    private static void AddCapsule(
        List<VertexPositionColor> vertices,
        Vector2 start,
        Vector2 end,
        float radius,
        Color color)
    {
        var direction = end - start;
        if (direction.LengthSquared() > float.Epsilon)
        {
            direction.Normalize();
            var perpendicular = new Vector2(-direction.Y, direction.X) * radius;
            var startLeft = start + perpendicular;
            var startRight = start - perpendicular;
            var endLeft = end + perpendicular;
            var endRight = end - perpendicular;
            AddTriangle(vertices, startLeft, endLeft, endRight, color);
            AddTriangle(vertices, startLeft, endRight, startRight, color);
        }

        AddFilledCircle(vertices, start, radius, color);
        AddFilledCircle(vertices, end, radius, color);
    }

    private static void AddFilledCircle(
        List<VertexPositionColor> vertices,
        Vector2 center,
        float radius,
        Color color)
    {
        const int segments = 16;
        for (var segment = 0; segment < segments; segment++)
        {
            var startAngle = MathHelper.TwoPi * segment / segments;
            var endAngle = MathHelper.TwoPi * (segment + 1) / segments;
            AddTriangle(
                vertices,
                center,
                center + new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * radius,
                center + new Vector2(MathF.Cos(endAngle), MathF.Sin(endAngle)) * radius,
                color);
        }
    }

    private static void AddTriangle(
        List<VertexPositionColor> vertices,
        Vector2 first,
        Vector2 second,
        Vector2 third,
        Color color)
    {
        vertices.Add(new VertexPositionColor(new Vector3(first, 0), color));
        vertices.Add(new VertexPositionColor(new Vector3(second, 0), color));
        vertices.Add(new VertexPositionColor(new Vector3(third, 0), color));
    }
}
