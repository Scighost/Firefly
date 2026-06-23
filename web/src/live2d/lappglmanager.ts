export class LAppGlManager {
  public initialize(canvas: HTMLCanvasElement): boolean {
    const gl =
      canvas.getContext('webgl2', { premultipliedAlpha: true, alpha: true }) ??
      canvas.getContext('webgl', { premultipliedAlpha: true, alpha: true });
    if (!gl) {
      return false;
    }
    this._gl = gl;
    return true;
  }

  public getGl(): WebGLRenderingContext | WebGL2RenderingContext {
    return this._gl;
  }

  public release(): void {
    this._gl = null;
  }

  private _gl: WebGLRenderingContext | WebGL2RenderingContext = null;
}
