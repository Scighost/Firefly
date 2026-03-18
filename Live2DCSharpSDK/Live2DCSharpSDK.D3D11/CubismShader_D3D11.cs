using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using System.Text;

namespace Live2DCSharpSDK.D3D11;

public unsafe class CubismShader_D3D11 : IDisposable
{
    public enum ShaderNames
    {
        ShaderNames_SetupMask,
        ShaderNames_Normal,
        ShaderNames_NormalMasked,
        ShaderNames_NormalMaskedInverted,
        ShaderNames_NormalPremultipliedAlpha,
        ShaderNames_NormalMaskedPremultipliedAlpha,
        ShaderNames_NormalMaskedInvertedPremultipliedAlpha,
        ShaderNames_Add,
        ShaderNames_AddMasked,
        ShaderNames_AddMaskedInverted,
        ShaderNames_AddPremultipliedAlpha,
        ShaderNames_AddMaskedPremultipliedAlpha,
        ShaderNames_AddMaskedInvertedPremultipliedAlpha,
        ShaderNames_Mult,
        ShaderNames_MultMasked,
        ShaderNames_MultMaskedInverted,
        ShaderNames_MultPremultipliedAlpha,
        ShaderNames_MultMaskedPremultipliedAlpha,
        ShaderNames_MultMaskedInvertedPremultipliedAlpha,
        ShaderNames_Max,
    }

    private List<ComPtr<ID3D11VertexShader>> _shaderSetsVS = new();
    private List<ComPtr<ID3D11PixelShader>> _shaderSetsPS = new();
    private ComPtr<ID3D11InputLayout> _vertexFormat;

    private static D3DCompiler _compiler;

    private const string ShaderSrc = """
cbuffer ConstantBuffer {
    float4x4 projectMatrix;
    float4x4 clipMatrix;
    float4 baseColor;
    float4 multiplyColor;
    float4 screenColor;
    float4 channelFlag;
}

Texture2D mainTexture : register(t0);
SamplerState mainSampler : register(s0);
Texture2D maskTexture : register(t1);

struct VS_IN {
    float2 pos : POSITION;
    float2 uv : TEXCOORD0;
};

struct VS_OUT {
    float4 Position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 clipPosition : TEXCOORD1;
};

VS_OUT VertSetupMask(VS_IN In) {
    VS_OUT Out = (VS_OUT)0;
    Out.Position = mul(float4(In.pos, 0.0f, 1.0f), projectMatrix);
    Out.clipPosition = mul(float4(In.pos, 0.0f, 1.0f), projectMatrix);
    Out.uv.x = In.uv.x;
    Out.uv.y = 1.0f - +In.uv.y;
    return Out;
}

float4 PixelSetupMask(VS_OUT In) : SV_Target{
    float isInside =
    step(baseColor.x, In.clipPosition.x / In.clipPosition.w)
    * step(baseColor.y, In.clipPosition.y / In.clipPosition.w)
    * step(In.clipPosition.x / In.clipPosition.w, baseColor.z)
    * step(In.clipPosition.y / In.clipPosition.w, baseColor.w);
    return channelFlag * mainTexture.Sample(mainSampler, In.uv).a * isInside;
}

VS_OUT VertNormal(VS_IN In) {
    VS_OUT Out = (VS_OUT)0;
    Out.Position = mul(float4(In.pos, 0.0f, 1.0f), projectMatrix);
    Out.uv.x = In.uv.x;
    Out.uv.y = 1.0f - +In.uv.y;
    return Out;
}

VS_OUT VertMasked(VS_IN In) {
    VS_OUT Out = (VS_OUT)0;
    Out.Position = mul(float4(In.pos, 0.0f, 1.0f), projectMatrix);
    Out.clipPosition = mul(float4(In.pos, 0.0f, 1.0f), clipMatrix);
    Out.uv.x = In.uv.x;
    Out.uv.y = 1.0f - In.uv.y;
    return Out;
}

float4 PixelNormal(VS_OUT In) : SV_Target{
    float4 texColor = mainTexture.Sample(mainSampler, In.uv);
    texColor.rgb = texColor.rgb * multiplyColor.rgb;
    texColor.rgb = (texColor.rgb + screenColor.rgb) - (texColor.rgb * screenColor.rgb);
    float4 color = texColor * baseColor;
    color.xyz *= color.w;
    return color;
}

float4 PixelNormalPremult(VS_OUT In) : SV_Target{
    float4 texColor = mainTexture.Sample(mainSampler, In.uv);
    texColor.rgb = texColor.rgb * multiplyColor.rgb;
    texColor.rgb = (texColor.rgb + screenColor.rgb * texColor.a) - (texColor.rgb * screenColor.rgb);
    float4 color = texColor * baseColor;
    return color;
}

float4 PixelMasked(VS_OUT In) : SV_Target{
    float4 texColor = mainTexture.Sample(mainSampler, In.uv);
    texColor.rgb = texColor.rgb * multiplyColor.rgb;
    texColor.rgb = (texColor.rgb + screenColor.rgb) - (texColor.rgb * screenColor.rgb);
    float4 color = texColor * baseColor;
    color.xyz *= color.w;
    float4 clipMask = (1.0f - maskTexture.Sample(mainSampler, In.clipPosition.xy / In.clipPosition.w)) * channelFlag;
    float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
    color = color * maskVal;
    return color;
}

float4 PixelMaskedInverted(VS_OUT In) : SV_Target{
    float4 texColor = mainTexture.Sample(mainSampler, In.uv);
    texColor.rgb = texColor.rgb * multiplyColor.rgb;
    texColor.rgb = (texColor.rgb + screenColor.rgb) - (texColor.rgb * screenColor.rgb);
    float4 color = texColor * baseColor;
    color.xyz *= color.w;
    float4 clipMask = (1.0f - maskTexture.Sample(mainSampler, In.clipPosition.xy / In.clipPosition.w)) * channelFlag;
    float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
    color = color * (1.0f - maskVal);
    return color;
}

float4 PixelMaskedPremult(VS_OUT In) : SV_Target{
    float4 texColor = mainTexture.Sample(mainSampler, In.uv);
    texColor.rgb = texColor.rgb * multiplyColor.rgb;
    texColor.rgb = (texColor.rgb + screenColor.rgb * texColor.a) - (texColor.rgb * screenColor.rgb);
    float4 color = texColor * baseColor;
    float4 clipMask = (1.0f - maskTexture.Sample(mainSampler, In.clipPosition.xy / In.clipPosition.w)) * channelFlag;
    float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
    color = color * maskVal;
    return color;
}

float4 PixelMaskedInvertedPremult(VS_OUT In) : SV_Target{
    float4 texColor = mainTexture.Sample(mainSampler, In.uv);
    texColor.rgb = texColor.rgb * multiplyColor.rgb;
    texColor.rgb = (texColor.rgb + screenColor.rgb * texColor.a) - (texColor.rgb * screenColor.rgb);
    float4 color = texColor * baseColor;
    float4 clipMask = (1.0f - maskTexture.Sample(mainSampler, In.clipPosition.xy / In.clipPosition.w)) * channelFlag;
    float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
    color = color * (1.0f - maskVal);
    return color;
}
""";

    public CubismShader_D3D11()
    {
        if (_compiler == null)
        {
            _compiler = D3DCompiler.GetApi();
        }
        for (int i = 0; i < (int)ShaderNames.ShaderNames_Max; i++)
        {
            _shaderSetsVS.Add(default);
            _shaderSetsPS.Add(default);
        }
    }

    public void Dispose()
    {
        ReleaseShaderProgram();
    }

    public void ReleaseShaderProgram()
    {
        if (_vertexFormat.Handle is not null)
        {
            _vertexFormat.Release();
            _vertexFormat = default;
        }

        for (int i = 0; i < _shaderSetsVS.Count; i++)
        {
            if (_shaderSetsVS[i].Handle is not null)
            {
                _shaderSetsVS[i].Release();
                _shaderSetsVS[i] = default;
            }
            if (_shaderSetsPS[i].Handle is not null)
            {
                _shaderSetsPS[i].Release();
                _shaderSetsPS[i] = default;
            }
        }
    }

    public void GenerateShaders(ComPtr<ID3D11Device> device)
    {
        if (_vertexFormat.Handle is not null) return;

        ReleaseShaderProgram();

        // SetupMask
        LoadShaderProgram(device, false, ShaderNames.ShaderNames_SetupMask, "VertSetupMask");
        LoadShaderProgram(device, true, ShaderNames.ShaderNames_SetupMask, "PixelSetupMask");

        // Normal
        LoadShaderProgram(device, false, ShaderNames.ShaderNames_Normal, "VertNormal");
        LoadShaderProgram(device, false, ShaderNames.ShaderNames_NormalMasked, "VertMasked");

        // Inverted/PremultipliedAlpha variants share the same VS bytecode; AddRef so each slot owns its reference
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMaskedInverted] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_Normal].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalPremultipliedAlpha] = _shaderSetsVS[(int)ShaderNames.ShaderNames_Normal];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMaskedPremultipliedAlpha] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMaskedInvertedPremultipliedAlpha] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];

        LoadShaderProgram(device, true, ShaderNames.ShaderNames_Normal, "PixelNormal");
        LoadShaderProgram(device, true, ShaderNames.ShaderNames_NormalMasked, "PixelMasked");
        LoadShaderProgram(device, true, ShaderNames.ShaderNames_NormalMaskedInverted, "PixelMaskedInverted");
        LoadShaderProgram(device, true, ShaderNames.ShaderNames_NormalPremultipliedAlpha, "PixelNormalPremult");
        LoadShaderProgram(device, true, ShaderNames.ShaderNames_NormalMaskedPremultipliedAlpha, "PixelMaskedPremult");
        LoadShaderProgram(device, true, ShaderNames.ShaderNames_NormalMaskedInvertedPremultipliedAlpha, "PixelMaskedInvertedPremult");

        // Add — each slot owns its reference via AddRef
        _shaderSetsVS[(int)ShaderNames.ShaderNames_Normal].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_Add] = _shaderSetsVS[(int)ShaderNames.ShaderNames_Normal];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_Normal].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_Add] = _shaderSetsPS[(int)ShaderNames.ShaderNames_Normal];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_AddMasked] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_AddMasked] = _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_AddMaskedInverted] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedInverted].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_AddMaskedInverted] = _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedInverted];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_Normal].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_AddPremultipliedAlpha] = _shaderSetsVS[(int)ShaderNames.ShaderNames_Normal];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalPremultipliedAlpha].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_AddPremultipliedAlpha] = _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalPremultipliedAlpha];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_AddMaskedPremultipliedAlpha] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedPremultipliedAlpha].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_AddMaskedPremultipliedAlpha] = _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedPremultipliedAlpha];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_AddMaskedInvertedPremultipliedAlpha] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedInvertedPremultipliedAlpha].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_AddMaskedInvertedPremultipliedAlpha] = _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedInvertedPremultipliedAlpha];

        // Mult — each slot owns its reference via AddRef
        _shaderSetsVS[(int)ShaderNames.ShaderNames_Normal].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_Mult] = _shaderSetsVS[(int)ShaderNames.ShaderNames_Normal];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_Normal].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_Mult] = _shaderSetsPS[(int)ShaderNames.ShaderNames_Normal];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_MultMasked] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_MultMasked] = _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_MultMaskedInverted] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedInverted].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_MultMaskedInverted] = _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedInverted];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_Normal].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_MultPremultipliedAlpha] = _shaderSetsVS[(int)ShaderNames.ShaderNames_Normal];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalPremultipliedAlpha].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_MultPremultipliedAlpha] = _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalPremultipliedAlpha];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_MultMaskedPremultipliedAlpha] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedPremultipliedAlpha].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_MultMaskedPremultipliedAlpha] = _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedPremultipliedAlpha];
        _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked].AddRef();
        _shaderSetsVS[(int)ShaderNames.ShaderNames_MultMaskedInvertedPremultipliedAlpha] = _shaderSetsVS[(int)ShaderNames.ShaderNames_NormalMasked];
        _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedInvertedPremultipliedAlpha].AddRef();
        _shaderSetsPS[(int)ShaderNames.ShaderNames_MultMaskedInvertedPremultipliedAlpha] = _shaderSetsPS[(int)ShaderNames.ShaderNames_NormalMaskedInvertedPremultipliedAlpha];

        // Create Input Layout
        ComPtr<ID3D10Blob> blob = new();
        ComPtr<ID3D10Blob> error = new();

        var shaderBytes = Encoding.UTF8.GetBytes(ShaderSrc);
        fixed (byte* ptr = shaderBytes)
        {
            int hr = _compiler.Compile(ptr, (nuint)shaderBytes.Length, (byte*)null, null, null, "VertNormal", "vs_4_0", 0, 0, blob.GetAddressOf(), error.GetAddressOf());
            if (hr < 0)
            {
                string message = error.Handle is not null
                    ? SilkMarshal.PtrToString((nint)error.GetBufferPointer()) ?? "Failed to compile VertNormal"
                    : "Failed to compile VertNormal";
                throw new Exception(message);
            }
        }

        InputElementDesc* layout = stackalloc InputElementDesc[2];
        layout[0] = new InputElementDesc
        {
            SemanticName = (byte*)SilkMarshal.StringToPtr("POSITION"),
            SemanticIndex = 0,
            Format = Format.FormatR32G32Float,
            InputSlot = 0,
            AlignedByteOffset = 0,
            InputSlotClass = InputClassification.PerVertexData,
            InstanceDataStepRate = 0
        };
        layout[1] = new InputElementDesc
        {
            SemanticName = (byte*)SilkMarshal.StringToPtr("TEXCOORD"),
            SemanticIndex = 0,
            Format = Format.FormatR32G32Float,
            InputSlot = 0,
            AlignedByteOffset = 8, // 2 floats * 4 bytes
            InputSlotClass = InputClassification.PerVertexData,
            InstanceDataStepRate = 0
        };

        ComPtr<ID3D11InputLayout> inputLayout = new();
        device.CreateInputLayout(layout, 2, blob.GetBufferPointer(), blob.GetBufferSize(), inputLayout.GetAddressOf());
        _vertexFormat = inputLayout;

        SilkMarshal.Free((nint)layout[0].SemanticName);
        SilkMarshal.Free((nint)layout[1].SemanticName);
    }

    private bool LoadShaderProgram(ComPtr<ID3D11Device> device, bool isPixelShader, ShaderNames shaderName, string entryPoint)
    {
        ComPtr<ID3D10Blob> blob = new();
        ComPtr<ID3D10Blob> error = new();

        var shaderBytes = Encoding.UTF8.GetBytes(ShaderSrc);
        fixed (byte* ptr = shaderBytes)
        {
            int hr = _compiler.Compile(ptr, (nuint)shaderBytes.Length, (byte*)null, null, null, entryPoint, isPixelShader ? "ps_4_0" : "vs_4_0", 0, 0, blob.GetAddressOf(), error.GetAddressOf());
            if (hr < 0)
            {
                string message = error.Handle is not null
                    ? SilkMarshal.PtrToString((nint)error.GetBufferPointer()) ?? $"Failed to compile {entryPoint}"
                    : $"Failed to compile {entryPoint}";
                throw new Exception(message);
            }
        }

        if (isPixelShader)
        {
            ComPtr<ID3D11PixelShader> ps = new();
            device.CreatePixelShader(blob.GetBufferPointer(), blob.GetBufferSize(), (ID3D11ClassLinkage*)null, ps.GetAddressOf());
            _shaderSetsPS[(int)shaderName] = ps;
        }
        else
        {
            ComPtr<ID3D11VertexShader> vs = new();
            device.CreateVertexShader(blob.GetBufferPointer(), blob.GetBufferSize(), (ID3D11ClassLinkage*)null, vs.GetAddressOf());
            _shaderSetsVS[(int)shaderName] = vs;
        }

        return true;
    }

    public void SetupShader(ComPtr<ID3D11Device> device, ComPtr<ID3D11DeviceContext> renderContext)
    {
        if (_vertexFormat.Handle is null) GenerateShaders(device);

        renderContext.IASetInputLayout(_vertexFormat);
    }

    public ComPtr<ID3D11VertexShader> GetVertexShader(ShaderNames shaderId)
    {
        return _shaderSetsVS[(int)shaderId];
    }

    public ComPtr<ID3D11PixelShader> GetPixelShader(ShaderNames shaderId)
    {
        return _shaderSetsPS[(int)shaderId];
    }
}
