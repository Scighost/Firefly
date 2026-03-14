using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using System.Numerics;

namespace Live2DCSharpSDK.D3D11;

public unsafe class CubismRenderState_D3D11 : IDisposable
{
    public enum Blend
    {
        Blend_Origin,
        Blend_Zero,
        Blend_Normal,
        Blend_Add,
        Blend_Mult,
        Blend_Mask,
        Blend_Max,
    }

    public enum Cull
    {
        Cull_Origin,
        Cull_None,
        Cull_Ccw,
        Cull_Max,
    }

    public enum Depth
    {
        Depth_Origin,
        Depth_Disable,
        Depth_Enable,
        Depth_Max,
    }

    public enum Sampler
    {
        Sampler_Origin,
        Sampler_Normal,
        Sampler_Anisotropy,
        Sampler_Max,
    }

    public struct Stored
    {
        public Blend _blendState;
        public Vector4 _blendFactor;
        public uint _blendMask;

        public Cull _cullMode;

        public Depth _depthEnable;
        public uint _depthRef;

        public float _viewportX;
        public float _viewportY;
        public float _viewportWidth;
        public float _viewportHeight;
        public float _viewportMinZ;
        public float _viewportMaxZ;

        public Sampler _sampler;

        public bool[] _valid;

        public Stored()
        {
            _blendState = Blend.Blend_Zero;
            _blendFactor = Vector4.Zero;
            _blendMask = 0xffffffff;

            _cullMode = Cull.Cull_None;

            _depthEnable = Depth.Depth_Disable;
            _depthRef = 0;

            _viewportX = 0;
            _viewportY = 0;
            _viewportWidth = 0;
            _viewportHeight = 0;
            _viewportMinZ = 0.0f;
            _viewportMaxZ = 0.0f;

            _sampler = Sampler.Sampler_Normal;

            _valid = new bool[(int)State.State_Max];
        }
    }

    public enum State
    {
        State_None,
        State_Blend,
        State_Viewport,
        State_ZEnable,
        State_CullMode,
        State_Sampler,
        State_Max,
    }

    private ComPtr<ID3D11Device> _device;
    private List<ComPtr<ID3D11BlendState>> _blendStateObjects = new();
    private List<ComPtr<ID3D11RasterizerState>> _rasterizeStateObjects = new();
    private List<ComPtr<ID3D11DepthStencilState>> _depthStencilState = new();
    private List<ComPtr<ID3D11SamplerState>> _samplerState = new();

    private List<Stored> _pushed = new();
    private Stored _stored = new();

    public CubismRenderState_D3D11(ComPtr<ID3D11Device> device)
    {
        _device = device;
        Create(device);
    }

    private void Create(ComPtr<ID3D11Device> device)
    {
        // Blend
        _blendStateObjects.Add(default); // Origin

        // Zero
        var blendDesc = new BlendDesc
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = false
        };
        blendDesc.RenderTarget[0].BlendEnable = true;
        blendDesc.RenderTarget[0].SrcBlend = Silk.NET.Direct3D11.Blend.Zero;
        blendDesc.RenderTarget[0].DestBlend = Silk.NET.Direct3D11.Blend.Zero;
        blendDesc.RenderTarget[0].BlendOp = Silk.NET.Direct3D11.BlendOp.Add;
        blendDesc.RenderTarget[0].SrcBlendAlpha = Silk.NET.Direct3D11.Blend.Zero;
        blendDesc.RenderTarget[0].DestBlendAlpha = Silk.NET.Direct3D11.Blend.Zero;
        blendDesc.RenderTarget[0].BlendOpAlpha = Silk.NET.Direct3D11.BlendOp.Add;
        blendDesc.RenderTarget[0].RenderTargetWriteMask = (byte)ColorWriteEnable.All;

        ComPtr<ID3D11BlendState> state = new();
        device.CreateBlendState(ref blendDesc, state.GetAddressOf());
        _blendStateObjects.Add(state);

        // Normal
        blendDesc.RenderTarget[0].SrcBlend = Silk.NET.Direct3D11.Blend.One;
        blendDesc.RenderTarget[0].DestBlend = Silk.NET.Direct3D11.Blend.InvSrcAlpha;
        blendDesc.RenderTarget[0].SrcBlendAlpha = Silk.NET.Direct3D11.Blend.One;
        blendDesc.RenderTarget[0].DestBlendAlpha = Silk.NET.Direct3D11.Blend.InvSrcAlpha;
        device.CreateBlendState(ref blendDesc, state.GetAddressOf());
        _blendStateObjects.Add(state);

        // Add
        blendDesc.RenderTarget[0].SrcBlend = Silk.NET.Direct3D11.Blend.One;
        blendDesc.RenderTarget[0].DestBlend = Silk.NET.Direct3D11.Blend.One;
        blendDesc.RenderTarget[0].SrcBlendAlpha = Silk.NET.Direct3D11.Blend.Zero;
        blendDesc.RenderTarget[0].DestBlendAlpha = Silk.NET.Direct3D11.Blend.One;
        device.CreateBlendState(ref blendDesc, state.GetAddressOf());
        _blendStateObjects.Add(state);

        // Mult
        blendDesc.RenderTarget[0].SrcBlend = Silk.NET.Direct3D11.Blend.DestColor;
        blendDesc.RenderTarget[0].DestBlend = Silk.NET.Direct3D11.Blend.InvSrcAlpha;
        blendDesc.RenderTarget[0].SrcBlendAlpha = Silk.NET.Direct3D11.Blend.Zero;
        blendDesc.RenderTarget[0].DestBlendAlpha = Silk.NET.Direct3D11.Blend.One;
        device.CreateBlendState(ref blendDesc, state.GetAddressOf());
        _blendStateObjects.Add(state);

        // Mask
        blendDesc.RenderTarget[0].SrcBlend = Silk.NET.Direct3D11.Blend.Zero;
        blendDesc.RenderTarget[0].DestBlend = Silk.NET.Direct3D11.Blend.InvSrcColor;
        blendDesc.RenderTarget[0].SrcBlendAlpha = Silk.NET.Direct3D11.Blend.Zero;
        blendDesc.RenderTarget[0].DestBlendAlpha = Silk.NET.Direct3D11.Blend.InvSrcAlpha;
        device.CreateBlendState(ref blendDesc, state.GetAddressOf());
        _blendStateObjects.Add(state);

        // Rasterizer
        _rasterizeStateObjects.Add(default); // Origin

        var rasterDesc = new RasterizerDesc
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None,
            FrontCounterClockwise = true,
            DepthClipEnable = false,
            MultisampleEnable = false,
            DepthBiasClamp = 0,
            SlopeScaledDepthBias = 0
        };

        ComPtr<ID3D11RasterizerState> rasterizer = new();
        device.CreateRasterizerState(ref rasterDesc, rasterizer.GetAddressOf());
        _rasterizeStateObjects.Add(rasterizer);

        // CCW
        rasterDesc.CullMode = CullMode.Back;
        device.CreateRasterizerState(ref rasterDesc, rasterizer.GetAddressOf());
        _rasterizeStateObjects.Add(rasterizer);

        // Depth
        _depthStencilState.Add(default); // Origin

        var depthDesc = new DepthStencilDesc
        {
            DepthEnable = false,
            DepthWriteMask = DepthWriteMask.All,
            DepthFunc = ComparisonFunc.Less,
            StencilEnable = false
        };

        ComPtr<ID3D11DepthStencilState> depth = new();
        device.CreateDepthStencilState(ref depthDesc, depth.GetAddressOf());
        _depthStencilState.Add(depth);

        // Enable
        depthDesc.DepthEnable = true;
        depthDesc.StencilEnable = false;
        device.CreateDepthStencilState(ref depthDesc, depth.GetAddressOf());
        _depthStencilState.Add(depth);

        // Sampler
        _samplerState.Add(default); // Origin

        var samplerDesc = new SamplerDesc
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            MaxAnisotropy = 0,
            ComparisonFunc = ComparisonFunc.Never,
            MinLOD = -float.MaxValue,
            MaxLOD = float.MaxValue
        };
        samplerDesc.BorderColor[0] = 1.0f;
        samplerDesc.BorderColor[1] = 1.0f;
        samplerDesc.BorderColor[2] = 1.0f;
        samplerDesc.BorderColor[3] = 1.0f;

        ComPtr<ID3D11SamplerState> sampler = new();
        device.CreateSamplerState(ref samplerDesc, sampler.GetAddressOf());
        _samplerState.Add(sampler);
    }

    public void Dispose()
    {
        _pushed.Clear();

        foreach (var sampler in _samplerState)
        {
            if (sampler.Handle is not null) sampler.Release();
        }
        _samplerState.Clear();

        foreach (var depth in _depthStencilState)
        {
            if (depth.Handle is not null) depth.Release();
        }
        _depthStencilState.Clear();

        foreach (var raster in _rasterizeStateObjects)
        {
            if (raster.Handle is not null) raster.Release();
        }
        _rasterizeStateObjects.Clear();

        foreach (var blend in _blendStateObjects)
        {
            if (blend.Handle is not null) blend.Release();
        }
        _blendStateObjects.Clear();
    }

    public void StartFrame()
    {
        _stored = new Stored();
        _pushed.Clear();
    }

    public void Save()
    {
        _pushed.Add(_stored);
    }

    public void Restore(ComPtr<ID3D11DeviceContext> renderContext)
    {
        if (_pushed.Count == 0) return;

        var size = _pushed.Count;
        bool[] isSet = new bool[(int)State.State_Max];

        for (int i = size - 1; i >= 0; i--)
        {
            var current = _pushed[i];

            if (current._valid[(int)State.State_Blend] && !isSet[(int)State.State_Blend])
            {
                SetBlend(renderContext, current._blendState, current._blendFactor, current._blendMask, true);
                isSet[(int)State.State_Blend] = true;
            }
            if (current._valid[(int)State.State_CullMode] && !isSet[(int)State.State_CullMode])
            {
                SetCullMode(renderContext, current._cullMode, true);
                isSet[(int)State.State_CullMode] = true;
            }
            if (current._valid[(int)State.State_Viewport] && !isSet[(int)State.State_Viewport])
            {
                SetViewport(renderContext, current._viewportX, current._viewportY, current._viewportWidth, current._viewportHeight, current._viewportMinZ, current._viewportMaxZ, true);
                isSet[(int)State.State_Viewport] = true;
            }
            if (current._valid[(int)State.State_ZEnable] && !isSet[(int)State.State_ZEnable])
            {
                SetZEnable(renderContext, current._depthEnable, current._depthRef, true);
                isSet[(int)State.State_ZEnable] = true;
            }
            if (current._valid[(int)State.State_Sampler] && !isSet[(int)State.State_Sampler])
            {
                SetSampler(renderContext, current._sampler, 0, null, true);
                isSet[(int)State.State_Sampler] = true;
            }
        }

        var store = _pushed[size - 1];
        _pushed.RemoveAt(size - 1);
        if (_pushed.Count == 0)
        {
            _pushed.Clear();
        }
        _stored = store;
    }

    public void SetBlend(ComPtr<ID3D11DeviceContext> renderContext, Blend blendState, Vector4 blendFactor, uint mask, bool force = false)
    {
        if (renderContext.Handle is null || blendState < 0 || blendState >= Blend.Blend_Max) return;

        if (!_stored._valid[(int)State.State_Blend] || force ||
            _stored._blendFactor != blendFactor ||
            _stored._blendMask != mask ||
            _stored._blendState != blendState)
        {
            float* factor = stackalloc float[4] { blendFactor.X, blendFactor.Y, blendFactor.Z, blendFactor.W };
            renderContext.OMSetBlendState(_blendStateObjects[(int)blendState], factor, mask);
        }

        _stored._blendState = blendState;
        _stored._blendFactor = blendFactor;
        _stored._blendMask = mask;
        _stored._valid[(int)State.State_Blend] = true;
    }

    public void SetCullMode(ComPtr<ID3D11DeviceContext> renderContext, Cull cullFace, bool force = false)
    {
        if (renderContext.Handle is null || cullFace < 0 || cullFace >= Cull.Cull_Max) return;

        if (!_stored._valid[(int)State.State_CullMode] || force || _stored._cullMode != cullFace)
        {
            renderContext.RSSetState(_rasterizeStateObjects[(int)cullFace]);
        }

        _stored._cullMode = cullFace;
        _stored._valid[(int)State.State_CullMode] = true;
    }

    public void SetViewport(ComPtr<ID3D11DeviceContext> renderContext, float left, float top, float width, float height, float zMin, float zMax, bool force = false)
    {
        if (!_stored._valid[(int)State.State_Viewport] || force ||
            _stored._viewportX != left || _stored._viewportY != top || _stored._viewportWidth != width || _stored._viewportHeight != height ||
            _stored._viewportMinZ != zMin || _stored._viewportMaxZ != zMax)
        {
            var viewport = new Viewport(left, top, width, height, zMin, zMax);
            renderContext.RSSetViewports(1, &viewport);
        }

        _stored._viewportX = left;
        _stored._viewportY = top;
        _stored._viewportWidth = width;
        _stored._viewportHeight = height;
        _stored._viewportMinZ = zMin;
        _stored._viewportMaxZ = zMax;
        _stored._valid[(int)State.State_Viewport] = true;
    }

    public void SetZEnable(ComPtr<ID3D11DeviceContext> renderContext, Depth enable, uint stencilRef, bool force = false)
    {
        if (renderContext.Handle is null || enable < 0 || enable >= Depth.Depth_Max) return;

        if (!_stored._valid[(int)State.State_ZEnable] || force || _stored._depthEnable != enable)
        {
            renderContext.OMSetDepthStencilState(_depthStencilState[(int)enable], stencilRef);
        }

        _stored._depthEnable = enable;
        _stored._depthRef = stencilRef;
        _stored._valid[(int)State.State_ZEnable] = true;
    }

    public void SetSampler(ComPtr<ID3D11DeviceContext> renderContext, Sampler sample, float anisotropy, ComPtr<ID3D11Device>? device, bool force = false)
    {
        if (renderContext.Handle is null || sample < 0 || sample >= Sampler.Sampler_Max) return;

        if (!_stored._valid[(int)State.State_Sampler] || force || _stored._sampler != sample)
        {
            if (anisotropy > 0.0f && sample == Sampler.Sampler_Anisotropy && device.HasValue)
            {
                ComPtr<ID3D11SamplerState> sampler = new();
                SamplerDesc samplerDesc = new SamplerDesc();
                ComPtr<ID3D11SamplerState> currentSampler = new();
                renderContext.PSGetSamplers(0, 1, currentSampler.GetAddressOf());
                currentSampler.GetDesc(&samplerDesc);
                samplerDesc.Filter = Filter.Anisotropic;
                samplerDesc.MaxAnisotropy = (uint)anisotropy;

                device.Value.CreateSamplerState(ref samplerDesc, sampler.GetAddressOf());
                _samplerState.Add(sampler);
            }

            var s = _samplerState[(int)sample];
            renderContext.PSSetSamplers(0, 1, s.GetAddressOf());
        }

        _stored._sampler = sample;
        _stored._valid[(int)State.State_Sampler] = true;
    }

    public void SaveCurrentNativeState(ComPtr<ID3D11Device> device, ComPtr<ID3D11DeviceContext> renderContext)
    {
        _pushed.Clear();
        _stored = new Stored();

        // Blend
        ComPtr<ID3D11BlendState> originBlend = new();
        float* originFactor = stackalloc float[4];
        uint originMask;
        renderContext.OMGetBlendState(originBlend.GetAddressOf(), originFactor, &originMask);
        if (_blendStateObjects[(int)Blend.Blend_Origin].Handle is not null)
        {
            _blendStateObjects[(int)Blend.Blend_Origin].Release();
        }
        _blendStateObjects[(int)Blend.Blend_Origin] = originBlend;
        SetBlend(renderContext, Blend.Blend_Origin, new Vector4(originFactor[0], originFactor[1], originFactor[2], originFactor[3]), originMask, true);

        // Cull
        ComPtr<ID3D11RasterizerState> originRaster = new();
        renderContext.RSGetState(originRaster.GetAddressOf());
        if (_rasterizeStateObjects[(int)Cull.Cull_Origin].Handle is not null)
        {
            _rasterizeStateObjects[(int)Cull.Cull_Origin].Release();
        }
        _rasterizeStateObjects[(int)Cull.Cull_Origin] = originRaster;
        SetCullMode(renderContext, Cull.Cull_Origin, true);

        // Depth
        ComPtr<ID3D11DepthStencilState> originDepth = new();
        uint stencilRef;
        renderContext.OMGetDepthStencilState(originDepth.GetAddressOf(), &stencilRef);
        if (_depthStencilState[(int)Depth.Depth_Origin].Handle is not null)
        {
            _depthStencilState[(int)Depth.Depth_Origin].Release();
        }
        _depthStencilState[(int)Depth.Depth_Origin] = originDepth;
        SetZEnable(renderContext, Depth.Depth_Origin, stencilRef, true);

        // Sampler
        ComPtr<ID3D11SamplerState> originSampler = new();
        renderContext.PSGetSamplers(0, 1, originSampler.GetAddressOf());
        if (_samplerState[(int)Sampler.Sampler_Origin].Handle is not null)
        {
            _samplerState[(int)Sampler.Sampler_Origin].Release();
        }
        _samplerState[(int)Sampler.Sampler_Origin] = originSampler;
        SetSampler(renderContext, Sampler.Sampler_Origin, 0, null, true);

        // Viewport
        uint numViewport = 1;
        Viewport viewPort;
        renderContext.RSGetViewports(&numViewport, &viewPort);
        SetViewport(renderContext, viewPort.TopLeftX, viewPort.TopLeftY, viewPort.Width, viewPort.Height, viewPort.MinDepth, viewPort.MaxDepth, true);

        Save();
    }

    public void RestoreNativeState(ComPtr<ID3D11Device> device, ComPtr<ID3D11DeviceContext> renderContext)
    {
        for (int i = _pushed.Count - 1; i >= 0; i--)
        {
            Restore(renderContext);
        }

        if (_samplerState[(int)Sampler.Sampler_Origin].Handle is not null)
        {
            _samplerState[(int)Sampler.Sampler_Origin].Release();
            _samplerState[(int)Sampler.Sampler_Origin] = default;
        }
        if (_depthStencilState[(int)Depth.Depth_Origin].Handle is not null)
        {
            _depthStencilState[(int)Depth.Depth_Origin].Release();
            _depthStencilState[(int)Depth.Depth_Origin] = default;
        }
        if (_rasterizeStateObjects[(int)Cull.Cull_Origin].Handle is not null)
        {
            _rasterizeStateObjects[(int)Cull.Cull_Origin].Release();
            _rasterizeStateObjects[(int)Cull.Cull_Origin] = default;
        }
        if (_blendStateObjects[(int)Blend.Blend_Origin].Handle is not null)
        {
            _blendStateObjects[(int)Blend.Blend_Origin].Release();
            _blendStateObjects[(int)Blend.Blend_Origin] = default;
        }
    }
}
