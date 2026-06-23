import { CubismMatrix44 } from '@framework/math/cubismmatrix44';
import { CubismViewMatrix } from '@framework/math/cubismviewmatrix';

import * as LAppDefine from './lappdefine';
import { LAppSubdelegate } from './lappsubdelegate';
import { TouchManager } from './touchmanager';

export class LAppView {
  public constructor() {
    this._programId = null;
    this._touchManager = new TouchManager();
    this._deviceToScreen = new CubismMatrix44();
    this._viewMatrix = new CubismViewMatrix();
    this._isMouseDown = false;
    this._smoothX = 0;
    this._smoothY = 0;
    this._targetX = 0;
    this._targetY = 0;
  }

  public initialize(subdelegate: LAppSubdelegate): void {
    this._subdelegate = subdelegate;
    const { width, height } = subdelegate.getCanvas();

    const ratio: number = width / height;
    const left: number = -ratio;
    const right: number = ratio;
    const bottom: number = LAppDefine.ViewLogicalLeft;
    const top: number = LAppDefine.ViewLogicalRight;

    this._viewMatrix.setScreenRect(left, right, bottom, top);
    this._viewMatrix.scale(LAppDefine.ViewScale, LAppDefine.ViewScale);

    this._deviceToScreen.loadIdentity();
    if (width > height) {
      const screenW: number = Math.abs(right - left);
      this._deviceToScreen.scaleRelative(screenW / width, -screenW / width);
    } else {
      const screenH: number = Math.abs(top - bottom);
      this._deviceToScreen.scaleRelative(screenH / height, -screenH / height);
    }
    this._deviceToScreen.translateRelative(-width * 0.5, -height * 0.5);

    this._viewMatrix.setMaxScale(LAppDefine.ViewMaxScale);
    this._viewMatrix.setMinScale(LAppDefine.ViewMinScale);

    this._viewMatrix.setMaxScreenRect(
      LAppDefine.ViewLogicalMaxLeft,
      LAppDefine.ViewLogicalMaxRight,
      LAppDefine.ViewLogicalMaxBottom,
      LAppDefine.ViewLogicalMaxTop
    );
  }

  public release(): void {
    this._viewMatrix = null;
    this._touchManager = null;
    this._deviceToScreen = null;

    if (this._subdelegate && this._programId) {
      this._subdelegate.getGlManager().getGl().deleteProgram(this._programId);
      this._programId = null;
    }
  }

  public render(): void {
    this._subdelegate.getGlManager().getGl().useProgram(this._programId);

    this._subdelegate.getGlManager().getGl().flush();

    const dampingFactor = 0.08;
    this._smoothX += (this._targetX - this._smoothX) * dampingFactor;
    this._smoothY += (this._targetY - this._smoothY) * dampingFactor;

    if (Math.abs(this._smoothX) < 0.001) this._smoothX = 0;
    if (Math.abs(this._smoothY) < 0.001) this._smoothY = 0;

    const live2dManager = this._subdelegate.getLive2DManager();
    if (live2dManager != null) {
      live2dManager.setViewMatrix(this._viewMatrix);
      live2dManager.onDrag(this._smoothX, this._smoothY);
      live2dManager.onUpdate();
    }
  }

  public initializeSprite(): void {
    if (this._programId == null) {
      this._programId = this._subdelegate.createShader();
    }
  }

  public onTouchesBegan(pointX: number, pointY: number): void {
    this._isMouseDown = true;
    this._touchManager.touchesBegan(
      pointX * window.devicePixelRatio,
      pointY * window.devicePixelRatio
    );
  }

  public onTouchesMoved(pointX: number, pointY: number): void {
    if (!this._isMouseDown) return;

    const posX = pointX * window.devicePixelRatio;
    const posY = pointY * window.devicePixelRatio;

    this._touchManager.touchesMoved(posX, posY);

    const viewX: number = this.transformViewX(this._touchManager.getX());
    const viewY: number = this.transformViewY(this._touchManager.getY());

    const maxRange = 0.3;
    this._targetX = Math.max(-maxRange, Math.min(maxRange, viewX));
    this._targetY = Math.max(-maxRange, Math.min(maxRange, viewY));
  }

  public onTouchesEnded(pointX: number, pointY: number): void {
    this._isMouseDown = false;
    this._targetX = 0;
    this._targetY = 0;

    const posX = pointX * window.devicePixelRatio;
    const posY = pointY * window.devicePixelRatio;

    const live2dManager = this._subdelegate.getLive2DManager();

    const x: number = this.transformViewX(posX);
    const y: number = this.transformViewY(posY);

    live2dManager.onTap(x, y);
  }

  public transformViewX(deviceX: number): number {
    const screenX: number = this._deviceToScreen.transformX(deviceX);
    return this._viewMatrix.invertTransformX(screenX);
  }

  public transformViewY(deviceY: number): number {
    const screenY: number = this._deviceToScreen.transformY(deviceY);
    return this._viewMatrix.invertTransformY(screenY);
  }

  private _touchManager: TouchManager;
  private _deviceToScreen: CubismMatrix44;
  private _viewMatrix: CubismViewMatrix;
  private _programId: WebGLProgram;
  private _subdelegate: LAppSubdelegate;
  private _isMouseDown: boolean;
  private _smoothX: number;
  private _smoothY: number;
  private _targetX: number;
  private _targetY: number;
}
