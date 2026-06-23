/**
 * Type definitions for Live2D Cubism Core JavaScript library.
 * Based on API usage in CubismWebFramework.
 *
 * The actual live2dcubismcore.min.js must be downloaded from:
 * https://www.live2d.com/download/cubism-sdk/download-web/
 */

declare namespace Live2DCubismCore {
  type csmLogFunction = (message: string) => void;

  enum csmParameterType {
    Normal = 0,
    BlendShape = 1
  }

  namespace Version {
    function csmGetVersion(): number;
    function csmGetLatestMocVersion(): number;
    function csmGetMocVersion(mocBytes: ArrayBuffer): number;
  }

  namespace Logging {
    function csmSetLogFunction(func: csmLogFunction): void;
    function csmGetLogFunction(): csmLogFunction | null;
  }

  namespace Memory {
    function initializeAmountOfMemory(size: number): void;
  }

  namespace Utils {
    function hasBlendAdditiveBit(flags: number): boolean;
    function hasBlendMultiplicativeBit(flags: number): boolean;
    function hasBlendColorDidChangeBit(flags: number): boolean;
    function hasIsDoubleSidedBit(flags: number): boolean;
    function hasIsInvertedMaskBit(flags: number): boolean;
    function hasIsVisibleBit(flags: number): boolean;
    function hasOpacityDidChangeBit(flags: number): boolean;
    function hasRenderOrderDidChangeBit(flags: number): boolean;
    function hasVertexPositionsDidChangeBit(flags: number): boolean;
    function hasVisibilityDidChangeBit(flags: number): boolean;
  }

  class Moc {
    static fromArrayBuffer(mocBytes: ArrayBuffer): Moc;
    hasMocConsistency(mocBytes: ArrayBuffer): number;
    _release(): void;
  }

  class Model {
    static fromMoc(moc: Moc): Model;

    canvasinfo: {
      PixelsPerUnit: number;
      CanvasWidth: number;
      CanvasHeight: number;
    };

    parameters: {
      count: number;
      values: Float32Array;
      maximumValues: Float32Array;
      minimumValues: Float32Array;
      defaultValues: Float32Array;
      ids: string[];
      types: Int32Array;
      repeats: Int32Array;
    };

    parts: {
      count: number;
      ids: string[];
      opacities: Float32Array;
      parentIndices: Int32Array;
      offscreenIndices: Int32Array;
    };

    drawables: {
      count: number;
      ids: string[];
      constantFlags: Uint8Array;
      dynamicFlags: Uint8Array;
      textureIndices: Int32Array;
      indexCounts: Int32Array;
      vertexCounts: Int32Array;
      indices: Uint16Array[];
      vertexPositions: Float32Array[];
      vertexUvs: Float32Array[];
      opacities: Float32Array;
      multiplyColors: Float32Array;
      screenColors: Float32Array;
      parentPartIndices: Int32Array;
      blendModes: Int32Array;
      masks: Int32Array[];
      maskCounts: Int32Array;
      drawOrders: Int32Array;
      renderOrders: Int32Array;
      resetDynamicFlags(): void;
    };

    offscreens: {
      count: number;
      constantFlags: Uint8Array;
      opacities: Float32Array;
      multiplyColors: Float32Array;
      screenColors: Float32Array;
      ownerIndices: Int32Array;
      blendModes: Int32Array;
      masks: Int32Array[];
      maskCounts: Int32Array;
    };

    update(): void;
    release(): void;
  }

  const ColorBlendType_Normal: number;
  const ColorBlendType_AddGlow: number;
  const ColorBlendType_Add: number;
  const ColorBlendType_Darken: number;
  const ColorBlendType_Multiply: number;
  const ColorBlendType_ColorBurn: number;
  const ColorBlendType_LinearBurn: number;
  const ColorBlendType_Lighten: number;
  const ColorBlendType_Screen: number;
  const ColorBlendType_ColorDodge: number;
  const ColorBlendType_Overlay: number;
  const ColorBlendType_SoftLight: number;
  const ColorBlendType_HardLight: number;
  const ColorBlendType_LinearLight: number;
  const ColorBlendType_Hue: number;
  const ColorBlendType_Color: number;
  const ColorBlendType_AddCompatible: number;
  const ColorBlendType_MultiplyCompatible: number;
}
