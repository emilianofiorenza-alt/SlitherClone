using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Slither.Core;
using Slither.Protocol;

namespace Slither.Client;

public sealed class PrimitiveSnapshotRenderer : ISnapshotRenderer, IDisposable
{
    private const int CircleSegments = 96;
    private const int DotTextureSize = 64;
    private const int SnakeTextureSize = 96;
    private const double SnakeTextureWorldRadius = 1.12;
    private const double TextureWorldWidth = 14.0;
    private static readonly Vector2[] UnitCircle = CreateUnitCircle();

    private static readonly Color ArenaColor = new(12, 31, 43);
    private static readonly Color OutsideColor = new(92, 13, 22);
    private static readonly Color SnakeShadowColor = new(0, 0, 0, 145);
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
    private static readonly (Color Body, Color Rim, Color Head)[] SnakePalette =
    [
        (new(70, 205, 225), new(65, 137, 151), new(75, 225, 235)),
        (new(238, 92, 92), new(148, 72, 72), new(255, 112, 104)),
        (new(114, 222, 103), new(75, 145, 72), new(132, 242, 116)),
        (new(183, 111, 242), new(116, 76, 151), new(205, 132, 255)),
        (new(250, 187, 67), new(151, 112, 54), new(255, 205, 82)),
        (new(245, 104, 184), new(151, 73, 118), new(255, 125, 202)),
        (new(91, 137, 244), new(67, 91, 151), new(112, 158, 255)),
        (new(226, 224, 100), new(143, 138, 70), new(246, 242, 118))
    ];

    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _effect;
    private readonly VertexBuffer _circleBuffer;
    private readonly VertexPositionColor[] _shadedCircleVertices = new VertexPositionColor[CircleSegments * 3];
    private VertexPositionColorTexture[] _snakeBodyVertices = [];
    private VertexPositionColor[] _radarLineVertices = new VertexPositionColor[256];
    private readonly Texture2D _backgroundTexture;
    private readonly Texture2D _dotDiscTexture;
    private readonly Texture2D _dotLightAtlas;
    private readonly Texture2D _snakeBodyAtlas;
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
            var vertexIndex = segment * 3;
            vertices[vertexIndex] = new VertexPositionColor(Vector3.Zero, Color.White);
            vertices[vertexIndex + 1] = new VertexPositionColor(
                new Vector3(UnitCircle[segment], 0), Color.White);
            vertices[vertexIndex + 2] = new VertexPositionColor(
                new Vector3(UnitCircle[segment + 1], 0), Color.White);
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
        _snakeBodyAtlas = CreateSnakeBodyAtlas(graphicsDevice);
    }

    public void SetScreenLayout(ScreenLayout layout) => _layout = layout;

    public void UpdateCameraScale(int score, double elapsedSeconds) =>
        _camera.ApproachVisibleWorldHeight(
            CameraTransform.DefaultVisibleWorldHeight * SnakeGrowthCurve.CameraScaleForScore(score),
            elapsedSeconds);

    public void Render(
        WorldSnapshot previousSnapshot,
        WorldSnapshot snapshot,
        float interpolationAlpha)
    {
        var layout = _layout ?? throw new InvalidOperationException("Screen layout has not been set.");
        var previousLocal = previousSnapshot.Snake.Id == snapshot.Snake.Id &&
                            previousSnapshot.Snake.Generation == snapshot.Snake.Generation
            ? previousSnapshot.Snake
            : snapshot.Snake;
        var cameraX = Lerp(previousLocal.HeadX, snapshot.Snake.HeadX, interpolationAlpha);
        var cameraY = Lerp(previousLocal.HeadY, snapshot.Snake.HeadY, interpolationAlpha);
        var viewBounds = ConfigureProjection(layout, cameraX, cameraY);

        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = _rasterizerState;

        using (new ProfileScope("Slither.Arena"))
            RenderArena(snapshot.Arena, cameraX, cameraY, viewBounds);
        using (new ProfileScope("Slither.Dots"))
            RenderDots(snapshot.VisibleDots, viewBounds);
        var currentSnakes = snapshot.VisibleSnakes ?? [snapshot.Snake];
        var previousSnakes = previousSnapshot.VisibleSnakes ?? [previousSnapshot.Snake];
        foreach (var currentSnake in currentSnakes)
        {
            if (!currentSnake.IsAlive) continue;
            var previousSnake = currentSnake;
            foreach (var candidate in previousSnakes)
            {
                if (candidate.Id == currentSnake.Id && candidate.Generation == currentSnake.Generation)
                {
                    previousSnake = candidate;
                    break;
                }
            }
            using (new ProfileScope("Slither.Snake"))
                RenderSnake(previousSnake, currentSnake, interpolationAlpha, viewBounds);
        }
        using (new ProfileScope("Slither.Radar"))
            RenderRadar(snapshot, layout);
    }

    public void Dispose()
    {
        _circleBuffer.Dispose();
        _backgroundTexture.Dispose();
        _dotDiscTexture.Dispose();
        _dotLightAtlas.Dispose();
        _snakeBodyAtlas.Dispose();
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
            var index = segment * 3;
            vertices[index] = TexturedVertex(centerX, centerY);
            vertices[index + 1] = TexturedVertex(
                centerX + (UnitCircle[segment].X * radius),
                centerY + (UnitCircle[segment].Y * radius));
            vertices[index + 2] = TexturedVertex(
                centerX + (UnitCircle[segment + 1].X * radius),
                centerY + (UnitCircle[segment + 1].Y * radius));
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

    private void RenderSnake(SnakeSnapshot previous, SnakeSnapshot current, float alpha, ViewBounds view)
    {
        var style = SnakePalette[Math.Abs(current.StyleId) % SnakePalette.Length];
        var bodyCount = Math.Min(previous.Body.Count, current.Body.Count);
        var headX = Lerp(previous.HeadX, current.HeadX, alpha);
        var headY = Lerp(previous.HeadY, current.HeadY, alpha);
        var headRadius = Lerp(previous.HeadRadius, current.HeadRadius, alpha);

        EnsureSnakeCapacity(bodyCount);
        var bodyVertexCount = 0;
        var headVisible = CircleIntersectsView(headX, headY, headRadius * 1.1, view);

        for (var index = bodyCount - 1; index >= 0; index--)
        {
            var oldNode = previous.Body[index];
            var node = current.Body[index];
            var x = Lerp(oldNode.X, node.X, alpha);
            var y = Lerp(oldNode.Y, node.Y, alpha);
            var radius = Lerp(oldNode.Radius, node.Radius, alpha);
            if (!CircleIntersectsView(x, y, radius * 1.1, view)) continue;
            AddSnakeBodyQuad(
                _snakeBodyVertices,
                ref bodyVertexCount,
                x,
                y,
                radius,
                Math.Abs(current.StyleId) % SnakePalette.Length);
        }
        DrawSnakeBodyQuads(_snakeBodyVertices, bodyVertexCount);

        if (!headVisible) return;

        var headingX = Lerp(previous.HeadingX, current.HeadingX, alpha);
        var headingY = Lerp(previous.HeadingY, current.HeadingY, alpha);
        var headColor = current.IsBoosting
            ? new Color(255, 190, 65)
            : style.Head;
        var headRimColor = current.IsBoosting
            ? new Color(151, 105, 54)
            : style.Rim;
        RenderBodyShadow(headX, headY, headRadius);
        RenderShadedCircle(headX, headY, headRadius, headColor, headRimColor);
        RenderEyes(headX, headY, headingX, headingY, headRadius);
    }

    private static bool CircleIntersectsView(double x, double y, double radius, ViewBounds view) =>
        x + radius >= view.Left && x - radius <= view.Right &&
        y + radius >= view.Bottom && y - radius <= view.Top;

    private void EnsureSnakeCapacity(int bodyCount)
    {
        var bodyVertices = bodyCount * 6;
        if (_snakeBodyVertices.Length < bodyVertices)
        {
            Array.Resize(ref _snakeBodyVertices, Math.Max(bodyVertices, Math.Max(2048, _snakeBodyVertices.Length * 2)));
        }
    }

    private static void AddSnakeBodyQuad(
        VertexPositionColorTexture[] vertices,
        ref int vertexCount,
        double x,
        double y,
        double radius,
        int paletteIndex)
    {
        var extent = radius * SnakeTextureWorldRadius;
        var left = (float)(x - extent);
        var right = (float)(x + extent);
        var bottom = (float)(y - extent);
        var top = (float)(y + extent);
        var atlasWidth = SnakeTextureSize * SnakePalette.Length;
        var u0 = ((paletteIndex * SnakeTextureSize) + 0.5f) / atlasWidth;
        var u1 = (((paletteIndex + 1) * SnakeTextureSize) - 0.5f) / atlasWidth;
        var v0 = 0.5f / SnakeTextureSize;
        var v1 = (SnakeTextureSize - 0.5f) / SnakeTextureSize;

        var topLeft = new VertexPositionColorTexture(new Vector3(left, top, 0), Color.White, new Vector2(u0, v0));
        var topRight = new VertexPositionColorTexture(new Vector3(right, top, 0), Color.White, new Vector2(u1, v0));
        var bottomLeft = new VertexPositionColorTexture(new Vector3(left, bottom, 0), Color.White, new Vector2(u0, v1));
        var bottomRight = new VertexPositionColorTexture(new Vector3(right, bottom, 0), Color.White, new Vector2(u1, v1));
        vertices[vertexCount++] = topLeft;
        vertices[vertexCount++] = topRight;
        vertices[vertexCount++] = bottomLeft;
        vertices[vertexCount++] = bottomLeft;
        vertices[vertexCount++] = topRight;
        vertices[vertexCount++] = bottomRight;
    }

    private void DrawSnakeBodyQuads(VertexPositionColorTexture[] vertices, int vertexCount)
    {
        if (vertexCount == 0) return;
        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
        _graphicsDevice.SetVertexBuffer(null);
        _effect.TextureEnabled = true;
        _effect.Texture = _snakeBodyAtlas;
        _effect.World = Matrix.Identity;
        _effect.DiffuseColor = Vector3.One;
        _effect.Alpha = 1;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertexCount / 3);
        }
        _effect.TextureEnabled = false;
    }

    private static void AddSolidCircle(
        VertexPositionColor[] vertices,
        ref int vertexCount,
        double x,
        double y,
        double radius,
        Color color)
    {
        for (var segment = 0; segment < CircleSegments; segment++)
        {
            vertices[vertexCount++] = new VertexPositionColor(new Vector3((float)x, (float)y, 0), color);
            vertices[vertexCount++] = new VertexPositionColor(
                new Vector3((float)(x + (UnitCircle[segment].X * radius)), (float)(y + (UnitCircle[segment].Y * radius)), 0),
                color);
            vertices[vertexCount++] = new VertexPositionColor(
                new Vector3((float)(x + (UnitCircle[segment + 1].X * radius)), (float)(y + (UnitCircle[segment + 1].Y * radius)), 0),
                color);
        }
    }

    private static void AddShadedCircle(
        VertexPositionColor[] vertices,
        ref int vertexCount,
        double x,
        double y,
        double radius,
        Color centerColor,
        Color rimColor)
    {
        AddSolidCircle(vertices, ref vertexCount, x, y, radius, Color.Lerp(rimColor, new Color(160, 164, 168), 0.58f));

        var innerRadius = radius * 0.90;
        var highlightX = x - (radius * 0.18);
        var highlightY = y + (radius * 0.22);
        var highlightColor = Color.Lerp(centerColor, Color.White, 0.32f);
        for (var segment = 0; segment < CircleSegments; segment++)
        {
            vertices[vertexCount++] = new VertexPositionColor(
                new Vector3((float)highlightX, (float)highlightY, 0), highlightColor);
            vertices[vertexCount++] = new VertexPositionColor(
                new Vector3((float)(x + (UnitCircle[segment].X * innerRadius)), (float)(y + (UnitCircle[segment].Y * innerRadius)), 0),
                rimColor);
            vertices[vertexCount++] = new VertexPositionColor(
                new Vector3((float)(x + (UnitCircle[segment + 1].X * innerRadius)), (float)(y + (UnitCircle[segment + 1].Y * innerRadius)), 0),
                rimColor);
        }
    }

    private void DrawColoredTriangles(VertexPositionColor[] vertices, int vertexCount)
    {
        if (vertexCount == 0) return;
        _effect.TextureEnabled = false;
        _effect.World = Matrix.Identity;
        _effect.DiffuseColor = Vector3.One;
        _effect.Alpha = 1;
        _graphicsDevice.SetVertexBuffer(null);
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertexCount / 3);
        }
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
            var opacity = (float)Math.Clamp(dot.Opacity, 0, 1);
            var atlasLeft = ((paletteIndex * DotTextureSize) + 0.5f) / _dotLightAtlas.Width;
            var atlasRight = (((paletteIndex + 1) * DotTextureSize) - 0.5f) / _dotLightAtlas.Width;
            AddDotQuad(
                _dotOutlineVertices,
                ref outlineCount,
                dot.X,
                dot.Y,
                renderRadius,
                Color.White * opacity,
                0,
                1);
            AddDotQuad(
                _dotBaseVertices,
                ref baseCount,
                dot.X,
                dot.Y,
                dot.Radius,
                new Color(18, 22, 27) * opacity,
                0,
                1);
            AddDotQuad(
                _dotLightVertices,
                ref lightCount,
                dot.X,
                dot.Y,
                dot.Radius,
                Color.White * opacity,
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
            x + (radius * 0.025),
            y - (radius * 0.030),
            radius * 1.075,
            SnakeShadowColor);
    }

    private void RenderRadar(WorldSnapshot snapshot, ScreenLayout layout)
    {
        var scale = Math.Min(layout.ViewportWidth, layout.ViewportHeight) / 720.0;
        var outerRadius = 58.0 * scale;
        var innerRadius = outerRadius - Math.Max(2.0, 3.0 * scale);
        var margin = 16.0 * scale;
        var centerX = layout.SafeArea.Left + margin + outerRadius;
        var centerY = layout.SafeArea.Top + margin + outerRadius;

        _effect.View = Matrix.Identity;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(
            0,
            layout.ViewportWidth,
            layout.ViewportHeight,
            0,
            0,
            1);

        RenderCircle(centerX, centerY, outerRadius, new Color(184, 190, 194));
        RenderCircle(centerX, centerY, innerRadius, new Color(11, 18, 23));

        var axisColor = new Color(72, 82, 88);
        var radarLineCount = 0;
        AddRadarLine(ref radarLineCount, centerX - innerRadius, centerY, centerX + innerRadius, centerY, axisColor);
        AddRadarLine(ref radarLineCount, centerX, centerY - innerRadius, centerX, centerY + innerRadius, axisColor);

        var radarSnakes = snapshot.RadarSnakes;
        if (radarSnakes is null || snapshot.Arena.PlayableRadius <= 0)
        {
            DrawRadarLines(radarLineCount);
            return;
        }

        var mapRadius = innerRadius - (4.0 * scale);
        var mapScale = mapRadius / snapshot.Arena.PlayableRadius;
        var bodyRadius = Math.Max(1.1, 1.25 * scale);
        foreach (var snake in radarSnakes)
        {
            if (snake.Body.Count > 0)
            {
                var previousX = centerX + ((snake.HeadX - snapshot.Arena.CenterX) * mapScale);
                var previousY = centerY - ((snake.HeadY - snapshot.Arena.CenterY) * mapScale);
                foreach (var node in snake.Body)
                {
                    var nodeX = centerX + ((node.X - snapshot.Arena.CenterX) * mapScale);
                    var nodeY = centerY - ((node.Y - snapshot.Arena.CenterY) * mapScale);
                    AddRadarLine(ref radarLineCount, previousX, previousY, nodeX, nodeY, new Color(145, 151, 156));
                    previousX = nodeX;
                    previousY = nodeY;
                }
            }

            if (snake.IsHuman)
            {
                continue;
            }
            RenderCircle(
                centerX + ((snake.HeadX - snapshot.Arena.CenterX) * mapScale),
                centerY - ((snake.HeadY - snapshot.Arena.CenterY) * mapScale),
                bodyRadius * 1.25,
                new Color(178, 182, 185));
        }

        DrawRadarLines(radarLineCount);

        var player = radarSnakes.FirstOrDefault(static snake => snake.IsHuman);
        if (!player.IsHuman)
        {
            return;
        }
        var playerX = centerX + ((player.HeadX - snapshot.Arena.CenterX) * mapScale);
        var playerY = centerY - ((player.HeadY - snapshot.Arena.CenterY) * mapScale);
        RenderCircle(playerX, playerY, Math.Max(3.4, 3.8 * scale), new Color(255, 220, 62));
        RenderCircle(playerX, playerY, Math.Max(2.2, 2.5 * scale), new Color(55, 235, 245));
    }

    private void AddRadarLine(ref int vertexCount, double startX, double startY, double endX, double endY, Color color)
    {
        if (vertexCount + 2 > _radarLineVertices.Length)
        {
            Array.Resize(ref _radarLineVertices, _radarLineVertices.Length * 2);
        }
        _radarLineVertices[vertexCount++] = new VertexPositionColor(new Vector3((float)startX, (float)startY, 0), color);
        _radarLineVertices[vertexCount++] = new VertexPositionColor(new Vector3((float)endX, (float)endY, 0), color);
    }

    private void DrawRadarLines(int vertexCount)
    {
        if (vertexCount == 0) return;
        _effect.TextureEnabled = false;
        _effect.World = Matrix.Identity;
        _effect.DiffuseColor = Vector3.One;
        _effect.Alpha = 1;
        _graphicsDevice.SetVertexBuffer(null);
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _radarLineVertices, 0, vertexCount / 2);
        }
    }

    private void RenderCircle(double x, double y, double radius, Color color)
    {
        _effect.TextureEnabled = false;
        _effect.World = Matrix.CreateScale((float)radius, (float)radius, 1) *
                        Matrix.CreateTranslation((float)x, (float)y, 0);
        _effect.DiffuseColor = color.ToVector3();
        _effect.Alpha = color.A / 255f;
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
            var vertexIndex = segment * 3;
            _shadedCircleVertices[vertexIndex] = new VertexPositionColor(
                new Vector3((float)highlightX, (float)highlightY, 0),
                highlightColor);
            _shadedCircleVertices[vertexIndex + 1] = new VertexPositionColor(
                new Vector3(
                    (float)(x + (UnitCircle[segment].X * innerRadius)),
                    (float)(y + (UnitCircle[segment].Y * innerRadius)),
                    0),
                rimColor);
            _shadedCircleVertices[vertexIndex + 2] = new VertexPositionColor(
                new Vector3(
                    (float)(x + (UnitCircle[segment + 1].X * innerRadius)),
                    (float)(y + (UnitCircle[segment + 1].Y * innerRadius)),
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

    private static Texture2D CreateSnakeBodyAtlas(GraphicsDevice graphicsDevice)
    {
        var width = SnakeTextureSize * SnakePalette.Length;
        var texture = new Texture2D(graphicsDevice, width, SnakeTextureSize);
        var pixels = new Color[width * SnakeTextureSize];
        for (var paletteIndex = 0; paletteIndex < SnakePalette.Length; paletteIndex++)
        {
            var style = SnakePalette[paletteIndex];
            var outerColor = Color.Lerp(style.Rim, new Color(160, 164, 168), 0.58f);
            var highlightColor = Color.Lerp(style.Body, Color.White, 0.32f);
            for (var y = 0; y < SnakeTextureSize; y++)
            {
                for (var x = 0; x < SnakeTextureSize; x++)
                {
                    var nx = ((((x + 0.5f) / SnakeTextureSize) * 2) - 1) * (float)SnakeTextureWorldRadius;
                    var ny = ((1 - (((y + 0.5f) / SnakeTextureSize) * 2))) * (float)SnakeTextureWorldRadius;
                    var color = Color.Transparent;

                    var shadowDx = nx - 0.025f;
                    var shadowDy = ny + 0.030f;
                    var shadowDistance = MathF.Sqrt((shadowDx * shadowDx) + (shadowDy * shadowDy));
                    var shadowCoverage = 1 - SmoothStep(1.045f, 1.075f, shadowDistance);
                    if (shadowCoverage > 0)
                        color = Premultiplied(SnakeShadowColor, shadowCoverage);

                    var distance = MathF.Sqrt((nx * nx) + (ny * ny));
                    var bodyCoverage = 1 - SmoothStep(0.985f, 1.0f, distance);
                    if (bodyCoverage > 0)
                    {
                        Color bodyColor;
                        if (distance >= 0.90f)
                        {
                            bodyColor = outerColor;
                        }
                        else
                        {
                            var hx = nx + 0.18f;
                            var hy = ny - 0.22f;
                            var highlightDistance = MathF.Sqrt((hx * hx) + (hy * hy));
                            var shade = Math.Clamp(highlightDistance / 1.05f, 0, 1);
                            bodyColor = Color.Lerp(highlightColor, style.Rim, shade);
                        }
                        color = CompositeOver(Premultiplied(bodyColor, bodyCoverage), color);
                    }

                    pixels[(y * width) + (paletteIndex * SnakeTextureSize) + x] = color;
                }
            }
        }
        texture.SetData(pixels);
        return texture;
    }

    private static Color Premultiplied(Color color, float coverage)
    {
        var alpha = Math.Clamp((color.A / 255f) * coverage, 0, 1);
        return new Color(
            (byte)Math.Clamp((int)MathF.Round(color.R * alpha), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(color.G * alpha), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(color.B * alpha), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(255 * alpha), 0, 255));
    }

    private static Color CompositeOver(Color foreground, Color background)
    {
        var inverseAlpha = 1 - (foreground.A / 255f);
        return new Color(
            (byte)Math.Clamp(foreground.R + MathF.Round(background.R * inverseAlpha), 0, 255),
            (byte)Math.Clamp(foreground.G + MathF.Round(background.G * inverseAlpha), 0, 255),
            (byte)Math.Clamp(foreground.B + MathF.Round(background.B * inverseAlpha), 0, 255),
            (byte)Math.Clamp(foreground.A + MathF.Round(background.A * inverseAlpha), 0, 255));
    }

    private static float SmoothStep(float from, float to, float value)
    {
        var amount = Math.Clamp((value - from) / (to - from), 0, 1);
        return amount * amount * (3 - (2 * amount));
    }

    private static Vector2[] CreateUnitCircle()
    {
        var points = new Vector2[CircleSegments + 1];
        for (var segment = 0; segment <= CircleSegments; segment++)
        {
            var angle = MathHelper.TwoPi * segment / CircleSegments;
            points[segment] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        return points;
    }

    private static double Lerp(double from, double to, float amount) =>
        from + ((to - from) * amount);

    private readonly record struct ViewBounds(double Left, double Right, double Bottom, double Top);
}
