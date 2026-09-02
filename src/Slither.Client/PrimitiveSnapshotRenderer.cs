using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Slither.Protocol;

namespace Slither.Client;

public sealed class PrimitiveSnapshotRenderer : ISnapshotRenderer, IDisposable
{
    private const int CircleSegments = 192;
    private const double GridSpacing = 2.0 / 1.5;

    private static readonly Color ArenaColor = new(12, 31, 43);
    private static readonly Color GridColor = new(40, 78, 94, 150);
    private static readonly Color OutsideColor = new(92, 13, 22);
    private static readonly Color BoundaryColor = new(205, 43, 52);

    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _effect;
    private readonly VertexBuffer _circleBuffer;
    private readonly RasterizerState _rasterizerState = new() { CullMode = CullMode.None };
    private readonly CameraTransform _camera = new();
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
        RenderGrid(snapshot.Arena, viewBounds);
        RenderSnake(previousSnapshot.Snake, snapshot.Snake, interpolationAlpha);
    }

    public void Dispose()
    {
        _circleBuffer.Dispose();
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
            return;
        }

        _graphicsDevice.Clear(OutsideColor);
        RenderCircle(arena.CenterX, arena.CenterY, arena.Radius, BoundaryColor);
        RenderCircle(arena.CenterX, arena.CenterY, arena.PlayableRadius, ArenaColor);
    }

    private void RenderGrid(ArenaSnapshot arena, ViewBounds view)
    {
        var vertices = new List<VertexPositionColor>();
        var firstX = Math.Ceiling(view.Left / GridSpacing) * GridSpacing;
        for (var x = firstX; x <= view.Right; x += GridSpacing)
        {
            var relativeX = x - arena.CenterX;
            var remaining = (arena.PlayableRadius * arena.PlayableRadius) - (relativeX * relativeX);
            if (remaining <= 0)
            {
                continue;
            }

            var extent = Math.Sqrt(remaining);
            AddLine(vertices, x, Math.Max(view.Bottom, arena.CenterY - extent),
                x, Math.Min(view.Top, arena.CenterY + extent));
        }

        var firstY = Math.Ceiling(view.Bottom / GridSpacing) * GridSpacing;
        for (var y = firstY; y <= view.Top; y += GridSpacing)
        {
            var relativeY = y - arena.CenterY;
            var remaining = (arena.PlayableRadius * arena.PlayableRadius) - (relativeY * relativeY);
            if (remaining <= 0)
            {
                continue;
            }

            var extent = Math.Sqrt(remaining);
            AddLine(vertices, Math.Max(view.Left, arena.CenterX - extent), y,
                Math.Min(view.Right, arena.CenterX + extent), y);
        }

        if (vertices.Count == 0)
        {
            return;
        }

        _effect.World = Matrix.Identity;
        _effect.DiffuseColor = Vector3.One;
        _effect.Alpha = 1;
        _graphicsDevice.SetVertexBuffer(null);
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.LineList,
                vertices.ToArray(),
                0,
                vertices.Count / 2);
        }
    }

    private void RenderSnake(SnakeSnapshot previous, SnakeSnapshot current, float alpha)
    {
        var bodyCount = Math.Min(previous.Body.Count, current.Body.Count);
        for (var index = bodyCount - 1; index >= 0; index--)
        {
            var oldNode = previous.Body[index];
            var node = current.Body[index];
            RenderCircle(
                Lerp(oldNode.X, node.X, alpha),
                Lerp(oldNode.Y, node.Y, alpha),
                node.Radius,
                new Color(55, 190, 215));
        }

        var headX = Lerp(previous.HeadX, current.HeadX, alpha);
        var headY = Lerp(previous.HeadY, current.HeadY, alpha);
        var headingX = Lerp(previous.HeadingX, current.HeadingX, alpha);
        var headingY = Lerp(previous.HeadingY, current.HeadingY, alpha);
        var headColor = current.IsBoosting
            ? new Color(255, 190, 65)
            : new Color(75, 225, 235);
        RenderCircle(headX, headY, current.HeadRadius, headColor);
        RenderEyes(headX, headY, headingX, headingY, current.HeadRadius);
    }

    private void RenderEyes(double headX, double headY, double headingX, double headingY, double radius)
    {
        var headingLength = Math.Sqrt((headingX * headingX) + (headingY * headingY));
        headingX /= headingLength;
        headingY /= headingLength;
        var perpendicularX = -headingY;
        var perpendicularY = headingX;
        var forward = radius * 0.52;
        var lateral = radius * 0.34;
        var eyeRadius = radius * 0.13;

        RenderCircle(headX + (headingX * forward) + (perpendicularX * lateral),
            headY + (headingY * forward) + (perpendicularY * lateral), eyeRadius, Color.White);
        RenderCircle(headX + (headingX * forward) - (perpendicularX * lateral),
            headY + (headingY * forward) - (perpendicularY * lateral), eyeRadius, Color.White);
    }

    private void RenderCircle(double x, double y, double radius, Color color)
    {
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

    private static void AddLine(
        List<VertexPositionColor> vertices,
        double x1,
        double y1,
        double x2,
        double y2)
    {
        if (x2 <= x1 && y2 <= y1)
        {
            return;
        }

        vertices.Add(new VertexPositionColor(new Vector3((float)x1, (float)y1, 0), GridColor));
        vertices.Add(new VertexPositionColor(new Vector3((float)x2, (float)y2, 0), GridColor));
    }

    private static double Lerp(double from, double to, float amount) =>
        from + ((to - from) * amount);

    private readonly record struct ViewBounds(double Left, double Right, double Bottom, double Top);
}
