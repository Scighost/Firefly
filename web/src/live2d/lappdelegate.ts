import { CubismFramework, Option } from '@framework/live2dcubismframework';
import * as LAppDefine from './lappdefine';
import { LAppPal } from './lapppal';
import { LAppSubdelegate } from './lappsubdelegate';

export let s_instance: LAppDelegate = null;

export class LAppDelegate {
  public static getInstance(): LAppDelegate {
    if (s_instance == null) {
      s_instance = new LAppDelegate();
    }
    return s_instance;
  }

  public static releaseInstance(): void {
    if (s_instance != null) {
      s_instance.release();
    }
    s_instance = null;
  }

  private onPointerBegan(e: PointerEvent): void {
    for (let i = 0; i < this._subdelegates.length; i++) {
      this._subdelegates[i].onPointBegan(e.clientX, e.clientY);
    }
  }

  private onPointerMoved(e: PointerEvent): void {
    for (let i = 0; i < this._subdelegates.length; i++) {
      this._subdelegates[i].onPointMoved(e.clientX, e.clientY);
    }
  }

  private onPointerEnded(e: PointerEvent): void {
    for (let i = 0; i < this._subdelegates.length; i++) {
      this._subdelegates[i].onPointEnded(e.clientX, e.clientY);
    }
  }

  private onPointerCancel(e: PointerEvent): void {
    for (let i = 0; i < this._subdelegates.length; i++) {
      this._subdelegates[i].onTouchCancel(e.clientX, e.clientY);
    }
  }

  public run(): void {
    const loop = (): void => {
      if (s_instance == null) return;

      LAppPal.updateTime();

      for (let i = 0; i < this._subdelegates.length; i++) {
        this._subdelegates[i].update();
      }

      this._animationFrameId = requestAnimationFrame(loop);
    };
    loop();
  }

  public stop(): void {
    if (this._animationFrameId) {
      cancelAnimationFrame(this._animationFrameId);
      this._animationFrameId = 0;
    }
  }

  private release(): void {
    this.releaseEventListener();
    this.releaseSubdelegates();
    CubismFramework.dispose();
    this._cubismOption = null;
  }

  private releaseEventListener(): void {
    if (this._canvas) {
      this._canvas.removeEventListener('pointerdown', this.pointBeganEventListener);
      this._canvas.removeEventListener('pointerup', this.pointEndedEventListener);
      this._canvas.removeEventListener('pointercancel', this.pointCancelEventListener);
    }
    document.removeEventListener('pointermove', this.pointMovedEventListener);
    this.pointBeganEventListener = null;
    this.pointMovedEventListener = null;
    this.pointEndedEventListener = null;
    this.pointCancelEventListener = null;
  }

  private releaseSubdelegates(): void {
    for (let i = 0; i < this._subdelegates.length; i++) {
      this._subdelegates[i].release();
    }
    this._subdelegates.length = 0;
    this._subdelegates = null;
  }

  public initialize(canvas: HTMLCanvasElement): boolean {
    this._canvas = canvas;

    this.initializeCubism();
    this.initializeSubdelegates(canvas);
    this.initializeEventListener();

    return true;
  }

  private initializeEventListener(): void {
    this.pointBeganEventListener = this.onPointerBegan.bind(this);
    this.pointMovedEventListener = this.onPointerMoved.bind(this);
    this.pointEndedEventListener = this.onPointerEnded.bind(this);
    this.pointCancelEventListener = this.onPointerCancel.bind(this);

    this._canvas.addEventListener('pointerdown', this.pointBeganEventListener, { passive: true });
    document.addEventListener('pointermove', this.pointMovedEventListener, { passive: true });
    this._canvas.addEventListener('pointerup', this.pointEndedEventListener, { passive: true });
    this._canvas.addEventListener('pointercancel', this.pointCancelEventListener, { passive: true });
  }

  private initializeCubism(): void {
    LAppPal.updateTime();

    this._cubismOption.logFunction = LAppPal.printMessage;
    this._cubismOption.loggingLevel = LAppDefine.CubismLoggingLevel;
    CubismFramework.startUp(this._cubismOption);
    CubismFramework.initialize();
  }

  private initializeSubdelegates(canvas: HTMLCanvasElement): void {
    const subdelegate = new LAppSubdelegate();
    subdelegate.initialize(canvas);
    this._subdelegates.push(subdelegate);
  }

  public getLive2DManager() {
    return this._subdelegates[0]?.getLive2DManager() ?? null
  }

  private constructor() {
    this._cubismOption = new Option();
    this._subdelegates = [];
    this._canvas = null;
    this._animationFrameId = 0;
  }

  private _cubismOption: Option;
  private _canvas: HTMLCanvasElement;
  private _subdelegates: LAppSubdelegate[];
  private _animationFrameId: number;

  private pointBeganEventListener: (e: PointerEvent) => void;
  private pointMovedEventListener: (e: PointerEvent) => void;
  private pointEndedEventListener: (e: PointerEvent) => void;
  private pointCancelEventListener: (e: PointerEvent) => void;
}
