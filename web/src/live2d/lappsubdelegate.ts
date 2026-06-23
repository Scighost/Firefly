import * as LAppDefine from './lappdefine';
import { LAppGlManager } from './lappglmanager';
import { LAppLive2DManager } from './lapplive2dmanager';
import { LAppPal } from './lapppal';
import { LAppTextureManager } from './lapptexturemanager';
import { LAppView } from './lappview';

export class LAppSubdelegate {
  public constructor() {
    this._canvas = null;
    this._glManager = new LAppGlManager();
    this._textureManager = new LAppTextureManager();
    this._live2dManager = new LAppLive2DManager();
    this._view = new LAppView();
    this._frameBuffer = null;
    this._captured = false;
  }

  public release(): void {
    if (this._resizeObserver) {
      this._resizeObserver.unobserve(this._canvas);
      this._resizeObserver.disconnect();
      this._resizeObserver = null;
    }

    this._live2dManager.release();
    this._live2dManager = null;

    this._view.release();
    this._view = null;

    this._textureManager.release();
    this._textureManager = null;

    this._glManager.release();
    this._glManager = null;
  }

  public initialize(canvas: HTMLCanvasElement): boolean {
    if (!this._glManager.initialize(canvas)) {
      return false;
    }

    this._canvas = canvas;

    if (LAppDefine.CanvasSize === 'auto') {
      this.resizeCanvas();
    } else {
      canvas.width = LAppDefine.CanvasSize.width;
      canvas.height = LAppDefine.CanvasSize.height;
    }

    if (canvas.width === 0 || canvas.height === 0) {
      canvas.width = 400;
      canvas.height = 400;
    }

    this._textureManager.setGlManager(this._glManager);

    const gl = this._glManager.getGl();

    if (!this._frameBuffer) {
      this._frameBuffer = gl.getParameter(gl.FRAMEBUFFER_BINDING);
    }

    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);

    this._view.initialize(this);

    this._live2dManager.setOffscreenSize(this._canvas.width, this._canvas.height);

    this._view.initializeSprite();

    this._live2dManager.initialize(this);

    this._resizeObserver = new ResizeObserver(
      (entries: ResizeObserverEntry[], observer: ResizeObserver) =>
        this.resizeObserverCallback.call(this, entries, observer)
    );
    this._resizeObserver.observe(this._canvas);

    return true;
  }

  private resizeObserverCallback(
    entries: ResizeObserverEntry[],
    observer: ResizeObserver
  ): void {
    if (LAppDefine.CanvasSize === 'auto') {
      this._needResize = true;
    }
  }

  public update(): void {
    if (this._glManager.getGl().isContextLost()) return;

    if (this._needResize) {
      this.resizeCanvas();
      this._view.initialize(this);
      this._view.initializeSprite();
      this._needResize = false;
    }

    const gl = this._glManager.getGl();

    gl.clearColor(0.0, 0.0, 0.0, 0.0);
    gl.enable(gl.DEPTH_TEST);
    gl.depthFunc(gl.LEQUAL);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
    gl.clearDepth(1.0);
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);

    this._view.render();
  }

  public createShader(): WebGLProgram {
    const gl = this._glManager.getGl();

    const vertexShaderId = gl.createShader(gl.VERTEX_SHADER);
    if (vertexShaderId == null) return null;

    const vertexShader =
      'precision mediump float;' +
      'attribute vec3 position;' +
      'attribute vec2 uv;' +
      'varying vec2 vuv;' +
      'void main(void)' +
      '{' +
      '   gl_Position = vec4(position, 1.0);' +
      '   vuv = uv;' +
      '}';

    gl.shaderSource(vertexShaderId, vertexShader);
    gl.compileShader(vertexShaderId);

    const fragmentShaderId = gl.createShader(gl.FRAGMENT_SHADER);
    if (fragmentShaderId == null) return null;

    const fragmentShader =
      'precision mediump float;' +
      'varying vec2 vuv;' +
      'uniform sampler2D texture;' +
      'void main(void)' +
      '{' +
      '   gl_FragColor = texture2D(texture, vuv);' +
      '}';

    gl.shaderSource(fragmentShaderId, fragmentShader);
    gl.compileShader(fragmentShaderId);

    const programId = gl.createProgram();
    gl.attachShader(programId, vertexShaderId);
    gl.attachShader(programId, fragmentShaderId);

    gl.deleteShader(vertexShaderId);
    gl.deleteShader(fragmentShaderId);

    gl.linkProgram(programId);
    gl.useProgram(programId);

    return programId;
  }

  public getTextureManager(): LAppTextureManager {
    return this._textureManager;
  }

  public getFrameBuffer(): WebGLFramebuffer {
    return this._frameBuffer;
  }

  public getCanvas(): HTMLCanvasElement {
    return this._canvas;
  }

  public getGlManager(): LAppGlManager {
    return this._glManager;
  }

  public getGl(): WebGLRenderingContext | WebGL2RenderingContext {
    return this._glManager.getGl();
  }

  public getLive2DManager(): LAppLive2DManager {
    return this._live2dManager;
  }

  private resizeCanvas(): void {
    this._canvas.width = this._canvas.clientWidth * window.devicePixelRatio;
    this._canvas.height = this._canvas.clientHeight * window.devicePixelRatio;

    const gl = this._glManager.getGl();
    gl.viewport(0, 0, gl.drawingBufferWidth, gl.drawingBufferHeight);
  }

  public onPointBegan(pageX: number, pageY: number): void {
    if (!this._view) return;
    this._captured = true;

    const rect = this._canvas.getBoundingClientRect();
    const localX = pageX - rect.left;
    const localY = pageY - rect.top;

    this._view.onTouchesBegan(localX, localY);
  }

  public onPointMoved(pageX: number, pageY: number): void {
    const rect = this._canvas.getBoundingClientRect();
    const localX = pageX - rect.left;
    const localY = pageY - rect.top;

    this._view.onTouchesMoved(localX, localY);
  }

  public onPointEnded(pageX: number, pageY: number): void {
    this._captured = false;
    if (!this._view) return;

    const rect = this._canvas.getBoundingClientRect();
    const localX = pageX - rect.left;
    const localY = pageY - rect.top;

    this._view.onTouchesEnded(localX, localY);
  }

  public onTouchCancel(pageX: number, pageY: number): void {
    this._captured = false;
    if (!this._view) return;

    const rect = this._canvas.getBoundingClientRect();
    const localX = pageX - rect.left;
    const localY = pageY - rect.top;

    this._view.onTouchesEnded(localX, localY);
  }

  private _canvas: HTMLCanvasElement;
  private _view: LAppView;
  private _textureManager: LAppTextureManager;
  private _frameBuffer: WebGLFramebuffer;
  private _glManager: LAppGlManager;
  private _live2dManager: LAppLive2DManager;
  private _resizeObserver: ResizeObserver;
  private _captured: boolean;
  private _needResize: boolean;
}
