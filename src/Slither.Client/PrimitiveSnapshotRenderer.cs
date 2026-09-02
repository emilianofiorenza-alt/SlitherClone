using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Slither.Protocol;

namespace Slither.Client;

public sealed class PrimitiveSnapshotRenderer : ISnapshotRenderer, IDisposable
{
    private const int CircleSegments = 192;
    private const int DotTextureSize = 64;
    private const double TextureWorldWidth = 14.0;

    private static readonly Color ArenaColor = new(12, 31, 43);
    private static readonly Color OutsideColor = new(92, 13, 22);
    private static readonly Color BoundaryColor = new(205, 43, 52);
    private static readonly Color[] DotPalette =
    [
        new(45, 225, 255),
        new(65, 255, 175),
        new(115, 245, 75),
        new(255, 235, 45),
        new(255, 165, 45),
        new(255, 95, 95),
        new(255, 80, 175),
        new(230, 85, 255),
        new(145, 105, 255),
        new(75, 175, 255)
    ];

    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _effect;
    private readonly VertexBuffer _circleBuffer;
    private readonly VertexPositionColor[] _shadedCircleVertices = new VertexPositionColor[CircleSegments * 3];
    private readonly Texture2D _backgroundTexture;
    private readonly Texture2D _dotDiscTexture;
    private readonly Texture2D _dotLightAtlas;
    private readonly SamplerState _backgroundSampler = new()
    {
        Filter = TextureFilter.Linear,
        AddressU = TextureAddressMode.Mirror,
        AddressV = TextureAddressMode.Mirror
    };
    private readonly RasterizerState _rasterizerState = new() { CullMode = CullMode.None };
    private readonly CameraTransform _camera = new();
    private VertexPositionColorTexture[] _dotOutlineVertices = [];
    private VertexPositionColorTexture[] _dotBaseVertices = [];
    private VertexPositionColorTexture[] _dotLightVertices = [];
    private ScreenLayout? _layout;

    public PrimitiveSnapshotRenderer(GraphicsDevice graphicsDevice)
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
                new Vector3(MathF.Cos(startAngle), MathF.Sin(startAngle), 0), Color.White);
            vertices[vertexIndex + 2] = new VertexPositionColor(
                new Vector3(MathF.Cos(endAngle), MathF.Sin(endAngle), 0), Color.White);
        }

        _circleBuffer = new VertexBuffer(
            graphicsDevice,
            VertexPositionColor.VertexDeclaration,
            vertices.Length,
            BufferUsage.WriteOnly);
        _circleBuffer.SetData(vertices);

        using var textureStream = typeof(PrimitiveSnapshotRenderer).Assembly
            .GetManifestResourceStream("Slither.Client.Assets.background.jpg")
            ?? throw new InvalidOperationException("Embedded arena background was not found.");
        _backgroundTexture = Texture2D.FromStream(graphicsDevice, textureStream);
        _dotDiscTexture = CreateDotDiscTexture(graphicsDevice);
        _dotLightAtlas = CreateDotLightAtlas(graphicsDevice);
    }

    public void SetScreenLayout(ScreenLayout layout) => _layout = layout;

    public void Render(
        WorldSnapshot previousSnapshot,
        WorldSnapshot snapshot,
        float interpolationAlpha)
    {
        var layout = _layout ?? throw new InvalidOperationException("Screen layout has not been set.");
        var cameraX = Lerp(previousSnapshot.Snake.HeadX, snapshot.Snake.HeadX, interpolationAlpha);
        var cameraY = Lerp(previousSnapshot.Snake.HeadY, snapshot.Snake.HeadY, interpolationAlpha);
        var viewBounds = ConfigureProjection(layout, cameraX, cameraY);

        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = _rasterizerState;

        RenderArena(snapshot.Arena, cameraX, cameraY, viewBounds);
        RenderDots(snapshot.VisibleDots, viewBounds);
        RenderSnake(previousSnapshot.Snake, snapshot.Snake, interpolationAlpha);
    }

    public void Dispose()
    {
        _circleBuffer.Dispose();
        _backgroundTexture.Dispose();
        _dotDiscTexture.Dispose();
        _dotLightAtlas.Dispose();
        _backgroundSampler.Dispose();
        _effect.Dispose();
        _rasterizerState.Dispose();
    }

    private ViewBounds ConfigureProjection(ScreenLayout layout, double cameraX, double cameraY)
    {
        var usableHeight = layout.ViewportHeight - layout.SafeArea.Top - layout.SafeArea.Bottom;
        var pixelsPerWorldUnit = usableHeight / _camera.VisibleWorldHeight;
        var center = layout.GameplayCenter;
        var left = cameraX - (center.X / pixelsPerWorldUnit);
        var right = cameraX + ((layout.ViewportWidth - center.X) / pixelsPerWorldUnit);
        var top = cameraY + (center.Y / pixelsPerWorldUnit);
        var bottom = cameraY - ((layout.ViewportHeight - center.Y) / pixelsPerWorldUnit);
        _effect.View = Matrix.Identity;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(
            (float)left,
            (float)right,
            (float)bottom,
            (float)top,
            0,
            1);
        return new ViewBounds(left, right, bottom, top);
    }

    private void RenderArena(ArenaSnapshot arena, double cameraX, double cameraY, ViewBounds view)
    {
        var halfWidth = (view.Right - view.Left) * 0.5;
        var halfHeight = (view.Top - view.Bottom) * 0.5;
        var halfDiagonal = Math.Sqrt((halfWidth * halfWidth) + (halfHeight * halfHeight));
        var dx = cameraX - arena.CenterX;
        var dy = cameraY - arena.CenterY;
        var boundaryVisible = Math.Sqrt((dx * dx) + (dy * dy)) + halfDiagonal >= arena.PlayableRadius;

        if (!boundaryVisible)
        {
            _graphicsDevice.Clear(ArenaColor);
            RenderTexturedQuad(view.Left, view.Right, view.Bottom, view.Top);
            return;
        }

        _graphicsDevice.Clear(OutsideColor);
        RenderCircle(arena.CenterX, arena.CenterY, arena.Radius, BoundaryColor);
        RenderTexturedCircle(arena.CenterX, arena.CenterY, arena.PlayableRadius);
    }

    private void RenderTexturedQuad(double left, double right, double bottom, double top)
    {
        var vertices = new[]
        {
            TexturedVertex(left, bottom), TexturedVertex(left, top), TexturedVertex(right, top),
            TexturedVertex(left, bottom), TexturedVertex(right, top), TexturedVertex(right, bottom)
        };
        RenderTexturedTriangles(vertices, 2);
    }

    private void RenderTexturedCircle(double centerX, double centerY, double radius)
    {
        var vertices = new VertexPositionColorTexture[CircleSegments * 3];
        for (var segment = 0; segment < CircleSegments; segment++)
        {
            var startAngle = MathHelper.TwoPi * segment / CircleSegments;
            var endAngle = MathHelper.TwoPi * (segment + 1) / CircleSegments;
            var index = segment * 3;
            vertices[index] = TexturedVertex(centerX, centerY);
            vertices[index + 1] = TexturedVertex(
                centerX + (Math.Cos(startAngle) * radius),
                centerY + (Math.Sin(startAngle) * radius));
            vertices[index + 2] = TexturedVertex(
                centerX + (Math.Cos(endAngle) * radius),
                centerY + (Math.Sin(endAngle) * radius));
        }

        RenderTexturedTriangles(vertices, CircleSegments);
    }

    private void RenderTexturedTriangles(VertexPositionColorTexture[] vertices, int primitiveCount)
    {
        _effect.World = Matrix.Identity;
        _effect.TextureEnabled = true;
        _effect.Texture = _backgroundTexture;
        _effect.DiffuseColor = Vector3.One;
        _effect.Alpha = 1;
        _graphicsDevice.SamplerStates[0] = _backgroundSampler;
        _graphicsDevice.SetVertexBuffer(null);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, primitiveCount);
        }

        _effect.TextureEnabled = false;
    }

    private void RenderSnake(SnakeSnapshot previous, SnakeSnapshot current, float alpha)
    {
        var bodyCount = Math.Min(previous.Body.Count, current.Body.Count);
        var headX = Lerp(previous.HeadX, current.HeadX, alpha);
        var headY = Lerp(previous.HeadY, current.HeadY, alpha);

        // A small common shadow around the silhouette helps the overlapping discs
        // read as one continuous body instead of unrelated circles.
        for (var index = bodyCount - 1; index >= 0; index--)
        {
            var oldNode = previous.Body[index];
            var node = current.Body[index];
            RenderBodyShadow(
                Lerp(oldNode.X, node.X, alpha),
                Lerp(oldNode.Y, node.Y, alpha),
                node.Radius);
        }
        RenderBodyShadow(headX, headY, current.HeadRadius);

        for (var index = bodyCount - 1; index >= 0; index--)
        {
            var oldNode = previous.Body[index];
            var node = current.Body[index];
            RenderShadedCircle(
                Lerp(oldNode.X, node.X, alpha),
                Lerp(oldNode.Y, node.Y, alpha),
                node.Radius,
                new Color(70, 205, 225),
                new Color(65, 137, 151));
        }

        var headingX = Lerp(previous.HeadingX, current.HeadingX, alpha);
        var headingY = Lerp(previous.HeadingY, current.HeadingY, alpha);
        var headColor = current.IsBoosting
            ? new Color(255, 190, 65)
            : new Color(75, 225, 235);
        var headRimColor = current.IsBoosting
            ? new Color(151, 105, 54)
            : new Color(69, 145, 155);
        RenderShadedCircle(headX, headY, current.HeadRadius, headColor, headRimColor);
        RenderEyes(headX, headY, headingX, headingY, current.HeadRadius);
    }

    private void RenderDots(IReadOnlyList<DotSnapshot> dots, ViewBounds view)
    {
        var requiredVertices = dots.Count * 6;
        EnsureDotCapacity(requiredVertices);
        var outlineCount = 0;
        var baseCount = 0;
        var lightCount = 0;
        foreach (var dot in dots)
        {
            var renderRadius = dot.Radius * 1.07;
            if (dot.X + renderRadius < view.Left ||
                dot.X - renderRadius > view.Right ||
                dot.Y + renderRadius < view.Bottom ||
                dot.Y - renderRadius > view.Top)
            {
                continue;
            }

            var paletteIndex = (int)(unchecked((ulong)dot.Id) % (ulong)DotPalette.Length);
            var atlasLeft = ((paletteIndex * DotTextureSize) + 0.5f) / _dotLightAtlas.Width;
            var atlasRight = (((paletteIndex + 1) * DotTextureSize) - 0.5f) / _dotLightAtlas.Width;
            AddDotQuad(
                _dotOutlineVertices,
                ref outlineCount,
                dot.X,
                dot.Y,
                renderRadius,
                Color.White,
                0,
                1);
            AddDotQuad(
                _dotBaseVertices,
                ref baseCount,
                dot.X,
                dot.Y,
                dot.Radius,
                new Color(18, 22, 27),
                0,
                1);
            AddDotQuad(
                _dotLightVertices,
                ref lightCount,
                dot.X,
                dot.Y,
                dot.Radius,
                Color.White,
                atlasLeft,
                atlasRight);
        }

        if (baseCount == 0)
        {
            return;
        }

        _effect.TextureEnabled = false;
        _effect.World = Matrix.Identity;
        _effect.DiffuseColor = Vector3.One;
        _effect.Alpha = 1;
        DrawDotLayer(
            _dotOutlineVertices,
            outlineCount,
            _dotDiscTexture,
            BlendState.AlphaBlend,
            new Color(3, 4, 6));
        DrawDotLayer(
            _dotBaseVertices,
            baseCount,
            _dotDiscTexture,
            BlendState.AlphaBlend,
            Color.White);
        DrawDotLayer(
            _dotLightVertices,
            lightCount,
            _dotLightAtlas,
            BlendState.Additive,
            Color.White);
        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _effect.TextureEnabled = false;
    }

    private void DrawDotLayer(
        VertexPositionColorTexture[] vertices,
        int vertexCount,
        Texture2D texture,
        BlendState blendState,
        Color tint)
    {
        _graphicsDevice.BlendState = blendState;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
        _graphicsDevice.SetVertexBuffer(null);
        _effect.TextureEnabled = true;
        _effect.Texture = texture;
        _effect.DiffuseColor = tint.ToVector3();
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                vertices,
                0,
                vertexCount / 3);
        }
    }

    private static void AddDotQuad(
        VertexPositionColorTexture[] vertices,
        ref int vertexCount,
        double x,
        double y,
        double radius,
        Color color,
        float textureLeft,
        float textureRight)
    {
        var left = (float)(x - radius);
        var right = (float)(x + radius);
        var bottom = (float)(y - radius);
        var top = (float)(y + radius);
        var topLeft = new VertexPositionColorTexture(
            new Vector3(left, top, 0), color, new Vector2(textureLeft, 0));
        var topRight = new VertexPositionColorTexture(
            new Vector3(right, top, 0), color, new Vector2(textureRight, 0));
        var bottomLeft = new VertexPositionColorTexture(
            new Vector3(left, bottom, 0), color, new Vector2(textureLeft, 1));
        var bottomRight = new VertexPositionColorTexture(
            new Vector3(right, bottom, 0), color, new Vector2(textureRight, 1));
        vertices[vertexCount++] = topLeft;
        vertices[vertexCount++] = bottomLeft;
        vertices[vertexCount++] = bottomRight;
        vertices[vertexCount++] = topLeft;
        vertices[vertexCount++] = bottomRight;
        vertices[vertexCount++] = topRight;
    }

    private void EnsureDotCapacity(int requiredVertices)
    {
        if (_dotOutlineVertices.Length >= requiredVertices)
        {
            return;
        }

        var capacity = Math.Max(requiredVertices, Math.Max(256, _dotOutlineVertices.Length * 2));
        Array.Resize(ref _dotOutlineVertices, capacity);
        Array.Resize(ref _dotBaseVertices, capacity);
        Array.Resize(ref _dotLightVertices, capacity);
    }

    private void RenderEyes(double headX, double headY, double headingX, double headingY, double radius)
    {
        var headingLength = Math.Sqrt((headingX * headingX) + (headingY * headingY));
        headingX /= headingLength;
        headingY /= headingLength;
        var perpendicularX = -headingY;
        var perpendicularY = headingX;
        // At the 1280x720 reference resolution the 32 px head carries
        // 12 px sclerae and 7 px pupils.
        var eyeForward = radius * 0.50;
        var eyeLateral = radius * 0.38;
        var eyeRadius = radius * 0.375;
        var eyeOutlineRadius = radius * 0.395;
        var leftEyeX = headX + (headingX * eyeForward) + (perpendicularX * eyeLateral);
        var leftEyeY = headY + (headingY * eyeForward) + (perpendicularY * eyeLateral);
        var rightEyeX = headX + (headingX * eyeForward) - (perpendicularX * eyeLateral);
        var rightEyeY = headY + (headingY * eyeForward) - (perpendicularY * eyeLateral);

        RenderCircle(leftEyeX, leftEyeY, eyeOutlineRadius, new Color(42, 48, 52));
        RenderCircle(rightEyeX, rightEyeY, eyeOutlineRadius, new Color(42, 48, 52));
        RenderCircle(leftEyeX, leftEyeY, eyeRadius, new Color(248, 248, 242));
        RenderCircle(rightEyeX, rightEyeY, eyeRadius, new Color(248, 248, 242));

        var pupilOffset = radius * 0.11;
        var pupilRadius = radius * 0.21875;
        RenderCircle(
            leftEyeX + (headingX * pupilOffset) - (perpendicularX * pupilOffset),
            leftEyeY + (headingY * pupilOffset) - (perpendicularY * pupilOffset),
            pupilRadius,
            new Color(12, 15, 17));
        RenderCircle(
            rightEyeX + (headingX * pupilOffset) + (perpendicularX * pupilOffset),
            rightEyeY + (headingY * pupilOffset) + (perpendicularY * pupilOffset),
            pupilRadius,
            new Color(12, 15, 17));
    }

    private void RenderBodyShadow(double x, double y, double radius)
    {
        RenderCircle(
            x + (radius * 0.035),
            y - (radius * 0.045),
            radius * 1.055,
            new Color(58, 61, 65));
    }

    private void RenderCircle(double x, double y, double radius, Color color)
    {
        _effect.TextureEnabled = false;
        _effect.World = Matrix.CreateScale((float)radius, (float)radius, 1) *
                        Matrix.CreateTranslation((float)x, (float)y, 0);
        _effect.DiffuseColor = color.ToVector3();
        _effect.Alpha = 1;
        _graphicsDevice.SetVertexBuffer(_circleBuffer);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, CircleSegments);
        }
    }

    private void RenderShadedCircle(
        double x,
        double y,
        double radius,
        Color centerColor,
        Color rimColor)
    {
        RenderCircle(x, y, radius, Color.Lerp(rimColor, new Color(160, 164, 168), 0.58f));

        var innerRadius = radius * 0.90;
        var highlightX = x - (radius * 0.18);
        var highlightY = y + (radius * 0.22);
        var highlightColor = Color.Lerp(centerColor, Color.White, 0.32f);
        for (var segment = 0; segment < CircleSegments; segment++)
        {
            var startAngle = MathHelper.TwoPi * segment / CircleSegments;
            var endAngle = MathHelper.TwoPi * (segment + 1) / CircleSegments;
            var vertexIndex = segment * 3;
            _shadedCircleVertices[vertexIndex] = new VertexPositionColor(
                new Vector3((float)highlightX, (float)highlightY, 0),
                highlightColor);
            _shadedCircleVertices[vertexIndex + 1] = new VertexPositionColor(
                new Vector3(
                    (float)(x + (Math.Cos(startAngle) * innerRadius)),
                    (float)(y + (Math.Sin(startAngle) * innerRadius)),
                    0),
                rimColor);
            _shadedCircleVertices[vertexIndex + 2] = new VertexPositionColor(
                new Vector3(
                    (float)(x + (Math.Cos(endAngle) * innerRadius)),
                    (float)(y + (Math.Sin(endAngle) * innerRadius)),
                    0),
                rimColor);
        }

        _effect.TextureEnabled = false;
        _effect.World = Matrix.Identity;
        _effect.DiffuseColor = Vector3.One;
        _effect.Alpha = 1;
        _graphicsDevice.SetVertexBuffer(null);
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _shadedCircleVertices,
                0,
                CircleSegments);
        }
    }

    private VertexPositionColorTexture TexturedVertex(double x, double y)
    {
        var textureWorldHeight = TextureWorldWidth * _backgroundTexture.Height / _backgroundTexture.Width;
        return new VertexPositionColorTexture(
            new Vector3((float)x, (float)y, 0),
            Color.White,
            new Vector2((float)(x / TextureWorldWidth), (float)(y / textureWorldHeight)));
    }

    private static Texture2D CreateDotDiscTexture(GraphicsDevice graphicsDevice)
    {
        var texture = new Texture2D(graphicsDevice, DotTextureSize, DotTextureSize);
        var pixels = new Color[DotTextureSize * DotTextureSize];
        for (var y = 0; y < DotTextureSize; y++)
        {
            for (var x = 0; x < DotTextureSize; x++)
            {
                var normalizedX = (((x + 0.5f) / DotTextureSize) * 2) - 1;
                var normalizedY = (((y + 0.5f) / DotTextureSize) * 2) - 1;
                var distance = MathF.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
                var coverage = 1 - SmoothStep(0.91f, 1.0f, distance);
                var channel = (byte)Math.Clamp((int)MathF.Round(coverage * 255), 0, 255);
                pixels[(y * DotTextureSize) + x] = new Color(channel, channel, channel, channel);
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static Texture2D CreateDotLightAtlas(GraphicsDevice graphicsDevice)
    {
        var width = DotTextureSize * DotPalette.Length;
        var texture = new Texture2D(graphicsDevice, width, DotTextureSize);
        var pixels = new Color[width * DotTextureSize];
        for (var paletteIndex = 0; paletteIndex < DotPalette.Length; paletteIndex++)
        {
            var baseColor = DotPalette[paletteIndex];
            for (var y = 0; y < DotTextureSize; y++)
            {
                for (var x = 0; x < DotTextureSize; x++)
                {
                    var normalizedX = (((x + 0.5f) / DotTextureSize) * 2) - 1;
                    var normalizedY = (((y + 0.5f) / DotTextureSize) * 2) - 1;
                    var distance = MathF.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
                    var fade = 1 - SmoothStep(0.18f, 1.0f, distance);
                    var whiteMix = 0.84f * MathF.Pow(Math.Max(0, 1 - distance), 2);
                    var lightColor = Color.Lerp(baseColor, Color.White, whiteMix);
                    var alpha = (byte)Math.Clamp((int)MathF.Round(fade * 235), 0, 235);
                    var pixelX = (paletteIndex * DotTextureSize) + x;
                    pixels[(y * width) + pixelX] = new Color(
                        lightColor.R,
                        lightColor.G,
                        lightColor.B,
                        alpha);
                }
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static float SmoothStep(float from, float to, float value)
    {
        var amount = Math.Clamp((value - from) / (to - from), 0, 1);
        return amount * amount * (3 - (2 * amount));
    }

    private static double Lerp(double from, double to, float amount) =>
        from + ((to - from) * amount);

    private readonly record struct ViewBounds(double Left, double Right, double Bottom, double Top);
}
