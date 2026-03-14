using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System.Numerics;

namespace Live2DCSharpSDK.D3D11;

public unsafe class CubismClippingManager_D3D11 : CubismClippingManager
{
    public override RenderType RenderType => RenderType.D3D11;

    public override CubismClippingContext CreateClippingContext(CubismClippingManager manager, CubismModel model, int* clippingDrawableIndices, int clipCount)
    {
        return new CubismClippingContext_D3D11(manager, clippingDrawableIndices, clipCount);
    }

    public void SetupClippingContext(ComPtr<ID3D11Device> device, ComPtr<ID3D11DeviceContext> renderContext, CubismModel model, CubismRenderer_D3D11 renderer, int offscreenCurrent)
    {
        int usingClipCount = 0;
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            var cc = (CubismClippingContext_D3D11)ClippingContextListForMask[clipIndex];
            CalcClippedDrawTotalBounds(model, cc);
            if (cc.IsUsing)
            {
                usingClipCount++;
            }
        }

        if (usingClipCount <= 0)
        {
            return;
        }

        renderer.GetRenderStateManager().SetViewport(renderContext,
            0,
            0,
            ClippingMaskBufferSize.X,
            ClippingMaskBufferSize.Y,
            0.0f, 1.0f);

        CurrentMaskBuffer = renderer.GetMaskBuffer(offscreenCurrent, 0);
        var currentMaskBuffer = (CubismOffscreenSurface_D3D11)CurrentMaskBuffer;

        currentMaskBuffer.BeginDraw(renderContext);

        SetupLayoutBounds(usingClipCount);

        if (ClearedMaskBufferFlags.Count != RenderTextureCount)
        {
            ClearedMaskBufferFlags.Clear();
            for (int i = 0; i < RenderTextureCount; ++i)
            {
                ClearedMaskBufferFlags.Add(false);
            }
        }
        else
        {
            for (int i = 0; i < RenderTextureCount; ++i)
            {
                ClearedMaskBufferFlags[i] = false;
            }
        }

        // Render masks
        var shaderManager = renderer.GetShaderManager();
        var vertexShader = shaderManager.GetVertexShader(CubismShader_D3D11.ShaderNames.ShaderNames_SetupMask);
        var pixelShader = shaderManager.GetPixelShader(CubismShader_D3D11.ShaderNames.ShaderNames_SetupMask);

        renderContext.VSSetShader(vertexShader, null, 0);
        renderContext.PSSetShader(pixelShader, null, 0);

        // Set texture to null to avoid hazard
        ID3D11ShaderResourceView** viewArray = stackalloc ID3D11ShaderResourceView*[2];
        viewArray[0] = null;
        viewArray[1] = null;
        renderContext.PSSetShaderResources(0, 2, viewArray);

        renderer.GetRenderStateManager().SetBlend(renderContext, CubismRenderState_D3D11.Blend.Blend_Mask, Vector4.Zero, 0xffffffff);
        renderer.GetRenderStateManager().SetZEnable(renderContext, CubismRenderState_D3D11.Depth.Depth_Disable, 0);
        renderer.GetRenderStateManager().SetCullMode(renderContext, CubismRenderState_D3D11.Cull.Cull_None);

        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            var cc = (CubismClippingContext_D3D11)ClippingContextListForMask[clipIndex];
            if (!cc.IsUsing) continue;

            var maskBuffer = (CubismOffscreenSurface_D3D11)renderer.GetMaskBuffer(offscreenCurrent, cc.BufferIndex);

            if (!ClearedMaskBufferFlags[cc.BufferIndex])
            {
                maskBuffer.Clear(renderContext, 1.0f, 1.0f, 1.0f, 1.0f);
                ClearedMaskBufferFlags[cc.BufferIndex] = true;
            }

            if (maskBuffer != currentMaskBuffer)
            {
                currentMaskBuffer.EndDraw(renderContext);
                currentMaskBuffer = maskBuffer;
                CurrentMaskBuffer = currentMaskBuffer;
                currentMaskBuffer.BeginDraw(renderContext);
            }

            // Compute MatrixForMask / MatrixForDraw for this clip context (D3D11 is right-handed = true)
            var allClippedDrawRect = cc.AllClippedDrawRect;
            var layoutBoundsOnTex01 = cc.LayoutBounds;
            const float MARGIN = 0.05f;
            TmpBoundsOnModel.SetRect(allClippedDrawRect);
            TmpBoundsOnModel.Expand(allClippedDrawRect.Width * MARGIN, allClippedDrawRect.Height * MARGIN);
            float scaleX = layoutBoundsOnTex01.Width / TmpBoundsOnModel.Width;
            float scaleY = layoutBoundsOnTex01.Height / TmpBoundsOnModel.Height;
            CreateMatrixForMask(true, layoutBoundsOnTex01, scaleX, scaleY);
            cc.MatrixForMask.SetMatrix(TmpMatrixForMask.Tr);
            cc.MatrixForDraw.SetMatrix(TmpMatrixForDraw.Tr);

            renderer.SetClippingContextBufferForMask(cc);

            int clipDrawCount = cc.ClippingIdCount;
            for (int i = 0; i < clipDrawCount; i++)
            {
                int clipDrawIndex = cc.ClippingIdList[i];

                if (!model.GetDrawableDynamicFlagIsVisible(clipDrawIndex)) continue;

                renderer.SetIsCulling(model.GetDrawableCulling(clipDrawIndex));

                renderer.DrawMesh(
                    clipDrawIndex,
                    model.GetDrawableTextureIndices(clipDrawIndex),
                    model.GetDrawableVertexIndexCount(clipDrawIndex),
                    model.GetDrawableVertexCount(clipDrawIndex),
                    (ushort*)model.GetDrawableVertexIndices(clipDrawIndex),
                    model.GetDrawableVertices(clipDrawIndex),
                    (float*)model.GetDrawableVertexUvs(clipDrawIndex),
                    model.GetDrawableOpacity(clipDrawIndex),
                    CubismBlendMode.Normal,
                    false
                );
            }
        }

        currentMaskBuffer.EndDraw(renderContext);
        renderer.SetClippingContextBufferForMask(null);
    }
}
