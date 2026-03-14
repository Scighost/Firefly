using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Live2DCSharpSDK.D3D11;

public unsafe class CubismRenderer_D3D11 : CubismRenderer, IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CubismConstantBufferD3D11
    {
        public Matrix4x4 ProjectMatrix;
        public Matrix4x4 ClipMatrix;
        public Vector4 BaseColor;
        public Vector4 MultiplyColor;
        public Vector4 ScreenColor;
        public Vector4 ChannelFlag;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CubismVertexD3D11
    {
        public float X, Y;
        public float U, V;
    }

    private ComPtr<ID3D11Device> _device;
    private ComPtr<ID3D11DeviceContext> _context;
    private CubismRenderState_D3D11 _renderState;
    private CubismShader_D3D11 _shaderManager;
    private CubismClippingManager_D3D11 _clippingManager;

    private List<List<ComPtr<ID3D11Buffer>>> _vertexBuffers = new();
    private List<List<ComPtr<ID3D11Buffer>>> _indexBuffers = new();
    private List<List<ComPtr<ID3D11Buffer>>> _constantBuffers = new();
    private List<List<CubismOffscreenSurface_D3D11>> _offscreenSurfaces = new();

    private int _commandBufferCurrent = 0;
    private int _commandBufferNum = 1;

    private CubismClippingContext_D3D11 _clippingContextBufferForMask;
    private CubismClippingContext_D3D11 _clippingContextBufferForDraw;

    private Dictionary<int, ComPtr<ID3D11ShaderResourceView>> _textures = new();
    private List<int> _sortedDrawableIndexList = new();

    public CubismRenderer_D3D11(ComPtr<ID3D11Device> device, ComPtr<ID3D11DeviceContext> context, CubismModel model, int maskBufferCount = 1) : base(model)
    {
        _device = device;
        _context = context;
        _renderState = new CubismRenderState_D3D11(device);
        _shaderManager = new CubismShader_D3D11();

        if (model.IsUsingMasking())
        {
            _clippingManager = new CubismClippingManager_D3D11();
            _clippingManager.Initialize(model, maskBufferCount);

            var bufferWidth = (uint)_clippingManager.ClippingMaskBufferSize.X;
            var bufferHeight = (uint)_clippingManager.ClippingMaskBufferSize.Y;

            for (int i = 0; i < _commandBufferNum; i++)
            {
                var surfaces = new List<CubismOffscreenSurface_D3D11>();
                for (int j = 0; j < maskBufferCount; j++)
                {
                    var surface = new CubismOffscreenSurface_D3D11();
                    surface.CreateOffscreenSurface(device, bufferWidth, bufferHeight);
                    surfaces.Add(surface);
                }
                _offscreenSurfaces.Add(surfaces);
            }
        }

        int drawableCount = model.GetDrawableCount();
        for (int i = 0; i < drawableCount; i++) _sortedDrawableIndexList.Add(0);

        for (int buffer = 0; buffer < _commandBufferNum; buffer++)
        {
            var vBuffers = new List<ComPtr<ID3D11Buffer>>();
            var iBuffers = new List<ComPtr<ID3D11Buffer>>();
            var cBuffers = new List<ComPtr<ID3D11Buffer>>();

            for (int drawAssign = 0; drawAssign < drawableCount; drawAssign++)
            {
                int vcount = model.GetDrawableVertexCount(drawAssign);
                if (vcount != 0)
                {
                    var bufferDesc = new BufferDesc
                    {
                        ByteWidth = (uint)(sizeof(CubismVertexD3D11) * vcount),
                        Usage = Usage.Dynamic,
                        BindFlags = (uint)BindFlag.VertexBuffer,
                        CPUAccessFlags = (uint)CpuAccessFlag.Write,
                        MiscFlags = 0,
                        StructureByteStride = 0
                    };

                    ComPtr<ID3D11Buffer> vBuffer = new();
                    device.CreateBuffer(ref bufferDesc, null, vBuffer.GetAddressOf());
                    vBuffers.Add(vBuffer);
                }
                else
                {
                    vBuffers.Add(default);
                }

                int icount = model.GetDrawableVertexIndexCount(drawAssign);
                if (icount != 0)
                {
                    var bufferDesc = new BufferDesc
                    {
                        ByteWidth = (uint)(sizeof(ushort) * icount),
                        Usage = Usage.Default,
                        BindFlags = (uint)BindFlag.IndexBuffer,
                        CPUAccessFlags = 0,
                        MiscFlags = 0,
                        StructureByteStride = 0
                    };

                    ushort* indices = model.GetDrawableVertexIndices(drawAssign);
                    var subResourceData = new SubresourceData
                    {
                        PSysMem = indices,
                        SysMemPitch = 0,
                        SysMemSlicePitch = 0
                    };

                    ComPtr<ID3D11Buffer> iBuffer = new();
                    device.CreateBuffer(ref bufferDesc, &subResourceData, iBuffer.GetAddressOf());
                    iBuffers.Add(iBuffer);
                }
                else
                {
                    iBuffers.Add(default);
                }

                {
                    var bufferDesc = new BufferDesc
                    {
                        ByteWidth = (uint)sizeof(CubismConstantBufferD3D11),
                        Usage = Usage.Default,
                        BindFlags = (uint)BindFlag.ConstantBuffer,
                        CPUAccessFlags = 0,
                        MiscFlags = 0,
                        StructureByteStride = 0
                    };

                    ComPtr<ID3D11Buffer> cBuffer = new();
                    device.CreateBuffer(ref bufferDesc, null, cBuffer.GetAddressOf());
                    cBuffers.Add(cBuffer);
                }
            }

            _vertexBuffers.Add(vBuffers);
            _indexBuffers.Add(iBuffers);
            _constantBuffers.Add(cBuffers);
        }
    }

    public override void Dispose()
    {
        _renderState.Dispose();
        _shaderManager.Dispose();

        foreach (var list in _vertexBuffers) foreach (var buf in list) if (buf.Handle is not null) buf.Release();
        foreach (var list in _indexBuffers) foreach (var buf in list) if (buf.Handle is not null) buf.Release();
        foreach (var list in _constantBuffers) foreach (var buf in list) if (buf.Handle is not null) buf.Release();
        foreach (var list in _offscreenSurfaces) foreach (var surf in list) surf.Dispose();
    }

    public void BindTexture(int modelTextureIndex, ComPtr<ID3D11ShaderResourceView> textureView)
    {
        _textures[modelTextureIndex] = textureView;
    }

    public CubismRenderState_D3D11 GetRenderStateManager() => _renderState;
    public CubismShader_D3D11 GetShaderManager() => _shaderManager;
    public CubismOffscreenSurface_D3D11 GetMaskBuffer(int backbufferNum, int offscreenIndex) => _offscreenSurfaces[backbufferNum][offscreenIndex];
    public void SetClippingContextBufferForMask(CubismClippingContext_D3D11 clip) => _clippingContextBufferForMask = clip;
    public void SetClippingContextBufferForDraw(CubismClippingContext_D3D11 clip) => _clippingContextBufferForDraw = clip;
    public void SetIsCulling(bool culling) => IsCulling = culling;

    protected override void SaveProfile()
    {
        _renderState.SaveCurrentNativeState(_device, _context);
    }

    protected override void RestoreProfile()
    {
        _renderState.RestoreNativeState(_device, _context);
    }

    protected override void DoDrawModel()
    {
        if (_clippingManager != null)
        {
            // Check size
            for (int i = 0; i < _clippingManager.RenderTextureCount; ++i)
            {
                if (_offscreenSurfaces[_commandBufferCurrent][i].BufferWidth != (uint)_clippingManager.ClippingMaskBufferSize.X ||
                    _offscreenSurfaces[_commandBufferCurrent][i].BufferHeight != (uint)_clippingManager.ClippingMaskBufferSize.Y)
                {
                    _offscreenSurfaces[_commandBufferCurrent][i].CreateOffscreenSurface(_device,
                        (uint)_clippingManager.ClippingMaskBufferSize.X, (uint)_clippingManager.ClippingMaskBufferSize.Y);
                }
            }

            // Save the current (full-size) viewport BEFORE mask drawing changes it to mask buffer size.
            uint numViewport = 1;
            Viewport savedViewPort;
            _context.RSGetViewports(&numViewport, &savedViewPort);

            _clippingManager.SetupClippingContext(_device, _context, Model, this, _commandBufferCurrent);

            // Restore the full-size viewport (SetupClippingContext sets it to mask buffer size e.g. 256x256).
            _renderState.SetViewport(_context, savedViewPort.TopLeftX, savedViewPort.TopLeftY, savedViewPort.Width, savedViewPort.Height, savedViewPort.MinDepth, savedViewPort.MaxDepth);
        }

        int drawableCount = Model.GetDrawableCount();
        int* renderOrder = Model.GetDrawableRenderOrders();

        for (int i = 0; i < drawableCount; ++i)
        {
            int order = renderOrder[i];
            _sortedDrawableIndexList[order] = i;
        }

        _renderState.StartFrame();
        _shaderManager.SetupShader(_device, _context);
        _renderState.SetSampler(_context, CubismRenderState_D3D11.Sampler.Sampler_Normal, Anisotropy, _device);

        for (int i = 0; i < drawableCount; ++i)
        {
            int drawableIndex = _sortedDrawableIndexList[i];

            if (!Model.GetDrawableDynamicFlagIsVisible(drawableIndex)) continue;

            var clipContext = (_clippingManager != null) ? (CubismClippingContext_D3D11)_clippingManager.ClippingContextListForDraw[drawableIndex] : null;

            if (clipContext != null && UseHighPrecisionMask && clipContext.IsUsing)
            {
                // High precision mask logic (omitted for now as it's complex and maybe not needed immediately)
            }

            SetClippingContextBufferForDraw(clipContext);

            IsCulling = Model.GetDrawableCulling(drawableIndex);

            DrawMesh(
                drawableIndex,
                Model.GetDrawableTextureIndices(drawableIndex),
                Model.GetDrawableVertexIndexCount(drawableIndex),
                Model.GetDrawableVertexCount(drawableIndex),
                (ushort*)Model.GetDrawableVertexIndices(drawableIndex),
                Model.GetDrawableVertices(drawableIndex),
                (float*)Model.GetDrawableVertexUvs(drawableIndex),
                Model.GetDrawableOpacity(drawableIndex),
                Model.GetDrawableBlendMode(drawableIndex),
                Model.GetDrawableInvertedMask(drawableIndex)
            );
        }

        // Post draw
        _commandBufferCurrent++;
        if (_commandBufferNum <= _commandBufferCurrent)
        {
            _commandBufferCurrent = 0;
        }
    }

    public void DrawMesh(int drawAssign, int textureIndex, int indexCount, int vertexCount, ushort* indexArray, float* vertexArray, float* uvArray, float opacity, CubismBlendMode colorBlendMode, bool invertedMask)
    {
        if (opacity <= 0.0f && _clippingContextBufferForMask == null) return;

        if (!_textures.TryGetValue(textureIndex, out var textureView) || textureView.Handle is null) return;

        // Sampler is chosen per draw so PS slot 0 always has a bound sampler
        if (Anisotropy >= 1.0f)
        {
            _renderState.SetSampler(_context, CubismRenderState_D3D11.Sampler.Sampler_Anisotropy, Anisotropy, _device);
        }
        else
        {
            _renderState.SetSampler(_context, CubismRenderState_D3D11.Sampler.Sampler_Normal, Anisotropy, _device);
        }

        _renderState.SetCullMode(_context, IsCulling ? CubismRenderState_D3D11.Cull.Cull_Ccw : CubismRenderState_D3D11.Cull.Cull_None);

        bool isMask = (_clippingContextBufferForMask != null);

        // Update Vertex Buffer
        var vBuffer = _vertexBuffers[_commandBufferCurrent][drawAssign];
        if (vBuffer.Handle is not null)
        {
            MappedSubresource mappedResource;
            _context.Map(vBuffer, 0, Map.WriteDiscard, 0, &mappedResource);
            var ptr = (CubismVertexD3D11*)mappedResource.PData;
            for (int i = 0; i < vertexCount; i++)
            {
                ptr[i].X = vertexArray[i * 2];
                ptr[i].Y = vertexArray[i * 2 + 1];
                ptr[i].U = uvArray[i * 2];
                ptr[i].V = uvArray[i * 2 + 1];
            }
            _context.Unmap(vBuffer, 0);
        }

        // Constant Buffer
        var cBuffer = _constantBuffers[_commandBufferCurrent][drawAssign];
        if (cBuffer.Handle is not null)
        {
            CubismConstantBufferD3D11 cb = new CubismConstantBufferD3D11();

            if (isMask)
            {
                var rect = _clippingContextBufferForMask.LayoutBounds;
                cb.ProjectMatrix = ConvertToMatrix4x4(_clippingContextBufferForMask.MatrixForMask);
                float right = rect.X + rect.Width;
                float bottom = rect.Y + rect.Height;
                cb.BaseColor = new Vector4(rect.X * 2.0f - 1.0f, rect.Y * 2.0f - 1.0f, right * 2.0f - 1.0f, bottom * 2.0f - 1.0f);
                cb.ChannelFlag = new Vector4(
                    _clippingContextBufferForMask.LayoutChannelIndex == 0 ? 1 : 0,
                    _clippingContextBufferForMask.LayoutChannelIndex == 1 ? 1 : 0,
                    _clippingContextBufferForMask.LayoutChannelIndex == 2 ? 1 : 0,
                    _clippingContextBufferForMask.LayoutChannelIndex == 3 ? 1 : 0
                );
                var multiply = Model.GetMultiplyColor(drawAssign);
                var screen = Model.GetScreenColor(drawAssign);
                cb.MultiplyColor = new Vector4(multiply.R, multiply.G, multiply.B, multiply.A);
                cb.ScreenColor = new Vector4(screen.R, screen.G, screen.B, screen.A);
            }
            else
            {
                cb.ProjectMatrix = ConvertToMatrix4x4(GetMvpMatrix());
                var color = GetModelColorWithOpacity(opacity);
                cb.BaseColor = new Vector4(color.R, color.G, color.B, color.A);

                if (_clippingContextBufferForDraw != null)
                {
                    cb.ClipMatrix = ConvertToMatrix4x4(_clippingContextBufferForDraw.MatrixForDraw);
                    cb.ChannelFlag = new Vector4(
                        _clippingContextBufferForDraw.LayoutChannelIndex == 0 ? 1 : 0,
                        _clippingContextBufferForDraw.LayoutChannelIndex == 1 ? 1 : 0,
                        _clippingContextBufferForDraw.LayoutChannelIndex == 2 ? 1 : 0,
                        _clippingContextBufferForDraw.LayoutChannelIndex == 3 ? 1 : 0
                    );
                }
                else
                {
                    cb.ClipMatrix = Matrix4x4.Identity;
                    cb.ChannelFlag = new Vector4(1, 0, 0, 0);
                }

                var multiply = Model.GetMultiplyColor(drawAssign);
                var screen = Model.GetScreenColor(drawAssign);
                cb.MultiplyColor = new Vector4(multiply.R, multiply.G, multiply.B, multiply.A);
                cb.ScreenColor = new Vector4(screen.R, screen.G, screen.B, screen.A);
            }

            _context.UpdateSubresource(cBuffer, 0, null, &cb, 0, 0);
        }

        // Set State
        if (isMask)
        {
            _shaderManager.SetupShader(_device, _context);
            var vs = _shaderManager.GetVertexShader(CubismShader_D3D11.ShaderNames.ShaderNames_SetupMask);
            var ps = _shaderManager.GetPixelShader(CubismShader_D3D11.ShaderNames.ShaderNames_SetupMask);
            if (vs.Handle is null || ps.Handle is null) return;
            _context.VSSetShader(vs, null, 0);
            _context.PSSetShader(ps, null, 0);

            _context.PSSetShaderResources(0, 1, textureView.GetAddressOf());

            ComPtr<ID3D11ShaderResourceView> nullView = default;
            _context.PSSetShaderResources(1, 1, nullView.GetAddressOf());

            _renderState.SetBlend(_context, CubismRenderState_D3D11.Blend.Blend_Mask, Vector4.Zero, 0xffffffff);
        }
        else
        {
            // Select shader based on blend mode and masking
            CubismShader_D3D11.ShaderNames shaderName = CubismShader_D3D11.ShaderNames.ShaderNames_Normal;

            if (_clippingContextBufferForDraw != null)
            {
                if (invertedMask)
                {
                    shaderName = IsPremultipliedAlpha ? CubismShader_D3D11.ShaderNames.ShaderNames_NormalMaskedInvertedPremultipliedAlpha : CubismShader_D3D11.ShaderNames.ShaderNames_NormalMaskedInverted;
                }
                else
                {
                    shaderName = IsPremultipliedAlpha ? CubismShader_D3D11.ShaderNames.ShaderNames_NormalMaskedPremultipliedAlpha : CubismShader_D3D11.ShaderNames.ShaderNames_NormalMasked;
                }
            }
            else
            {
                shaderName = IsPremultipliedAlpha ? CubismShader_D3D11.ShaderNames.ShaderNames_NormalPremultipliedAlpha : CubismShader_D3D11.ShaderNames.ShaderNames_Normal;
            }

            // Adjust for Add/Mult
            if (colorBlendMode == CubismBlendMode.Additive)
            {
                shaderName = (CubismShader_D3D11.ShaderNames)((int)shaderName + (int)CubismShader_D3D11.ShaderNames.ShaderNames_Add - (int)CubismShader_D3D11.ShaderNames.ShaderNames_Normal);
            }
            else if (colorBlendMode == CubismBlendMode.Multiplicative)
            {
                shaderName = (CubismShader_D3D11.ShaderNames)((int)shaderName + (int)CubismShader_D3D11.ShaderNames.ShaderNames_Mult - (int)CubismShader_D3D11.ShaderNames.ShaderNames_Normal);
            }

            var vs = _shaderManager.GetVertexShader(shaderName);
            var ps = _shaderManager.GetPixelShader(shaderName);
            if (vs.Handle is null || ps.Handle is null) return;
            _context.VSSetShader(vs, null, 0);
            _context.PSSetShader(ps, null, 0);

            // Set Texture
            _context.PSSetShaderResources(0, 1, textureView.GetAddressOf());

            // Set Mask Texture
            if (_clippingContextBufferForDraw != null)
            {
                var maskBuffer = _offscreenSurfaces[_commandBufferCurrent][_clippingContextBufferForDraw.BufferIndex];
                var maskView = maskBuffer.TextureView;
                _context.PSSetShaderResources(1, 1, maskView.GetAddressOf());
            }
            else
            {
                ComPtr<ID3D11ShaderResourceView> nullView = default;
                _context.PSSetShaderResources(1, 1, nullView.GetAddressOf());
            }

            // Set Blend State
            CubismRenderState_D3D11.Blend blend = CubismRenderState_D3D11.Blend.Blend_Normal;
            if (colorBlendMode == CubismBlendMode.Additive) blend = CubismRenderState_D3D11.Blend.Blend_Add;
            else if (colorBlendMode == CubismBlendMode.Multiplicative) blend = CubismRenderState_D3D11.Blend.Blend_Mult;

            _renderState.SetBlend(_context, blend, Vector4.Zero, 0xffffffff);
        }

        // Set Buffers
        uint stride = (uint)sizeof(CubismVertexD3D11);
        uint offset = 0;
        _context.IASetVertexBuffers(0, 1, vBuffer.GetAddressOf(), &stride, &offset);
        _context.IASetIndexBuffer(_indexBuffers[_commandBufferCurrent][drawAssign], Format.FormatR16Uint, 0);
        _context.IASetPrimitiveTopology(D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist);

        _context.VSSetConstantBuffers(0, 1, cBuffer.GetAddressOf());
        _context.PSSetConstantBuffers(0, 1, cBuffer.GetAddressOf());

        // Draw
        _context.DrawIndexed((uint)indexCount, 0, 0);

        // Reset per-draw clipping references to mirror native renderer behavior
        SetClippingContextBufferForDraw(null);
        SetClippingContextBufferForMask(null);
    }

    private Matrix4x4 ConvertToMatrix4x4(Live2DCSharpSDK.Framework.Math.CubismMatrix44 m)
    {
        // CubismMatrix44 uses OpenGL column-major convention (translation at _tr[12,13]).
        // HLSL cbuffer float4x4 is also read column-major, so mul(v, M) requires us to
        // transpose, matching the original C++ SDK's XMMatrixTranspose() call.
        return new Matrix4x4(
            m.Tr[0], m.Tr[4], m.Tr[8], m.Tr[12],
            m.Tr[1], m.Tr[5], m.Tr[9], m.Tr[13],
            m.Tr[2], m.Tr[6], m.Tr[10], m.Tr[14],
            m.Tr[3], m.Tr[7], m.Tr[11], m.Tr[15]
        );
    }
}
