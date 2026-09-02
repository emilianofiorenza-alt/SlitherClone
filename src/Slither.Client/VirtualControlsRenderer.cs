using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Slither.Client;

public sealed class VirtualControlsRenderer : IDisposable
{
    private const int CircleSegments = 48;

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

    public void Render(ScreenLayout layout, VirtualControls controls)
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
}
