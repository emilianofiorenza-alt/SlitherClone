using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Slither.Protocol;

namespace Slither.Client;

public sealed class PrimitiveSnapshotRenderer : ISnapshotRenderer, IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _effect;
    private readonly VertexBuffer _vertexBuffer;
    private readonly RasterizerState _rasterizerState = new() { CullMode = CullMode.None };

    public PrimitiveSnapshotRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _effect = new BasicEffect(graphicsDevice)
        {
            VertexColorEnabled = true,
            View = Matrix.Identity
        };

        var vertices = new[]
        {
            new VertexPositionColor(new Vector3(0.16f, 0, 0), Color.Cyan),
            new VertexPositionColor(new Vector3(-0.10f, 0.09f, 0), Color.DeepSkyBlue),
            new VertexPositionColor(new Vector3(-0.10f, -0.09f, 0), Color.MediumPurple)
        };

        _vertexBuffer = new VertexBuffer(
            graphicsDevice,
            VertexPositionColor.VertexDeclaration,
            vertices.Length,
            BufferUsage.WriteOnly);
        _vertexBuffer.SetData(vertices);
    }

    public void Render(WorldSnapshot snapshot, float interpolationAlpha)
    {
        var viewport = _graphicsDevice.Viewport;
        var aspect = viewport.Width / (float)Math.Max(1, viewport.Height);

        _effect.World = Matrix.CreateRotationZ(snapshot.MarkerAngle) *
                        Matrix.CreateTranslation(snapshot.MarkerX, snapshot.MarkerY, 0);
        _effect.Projection = Matrix.CreateOrthographic(2 * aspect, 2, 0, 1);

        _graphicsDevice.RasterizerState = _rasterizerState;
        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 1);
        }
    }

    public void Dispose()
    {
        _vertexBuffer.Dispose();
        _effect.Dispose();
        _rasterizerState.Dispose();
    }
}
