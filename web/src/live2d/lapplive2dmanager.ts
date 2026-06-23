import { CubismMatrix44 } from '@framework/math/cubismmatrix44';
import { ACubismMotion } from '@framework/motion/acubismmotion';
import { CubismWebGLOffscreenManager } from '@framework/rendering/cubismoffscreenmanager';

import * as LAppDefine from './lappdefine';
import { LAppModel } from './lappmodel';
import { LAppPal } from './lapppal';
import { LAppSubdelegate } from './lappsubdelegate';

export class LAppLive2DManager {
  private releaseAllModel(): void {
    this._models.length = 0;
  }

  public setOffscreenSize(width: number, height: number): void {
    for (let i = 0; i < this._models.length; i++) {
      const model = this._models[i];
      model?.setRenderTargetSize(width, height);
    }
  }

  public onDrag(x: number, y: number): void {
    const model = this._models[0];
    if (model) {
      model.setDragging(x, y);
    }
  }

  private _lastTapTime = 0;
  private _lastTapArea = '';

  public onTap(x: number, y: number): void {
    const model = this._models[0];
    if (!model || !model.isLoaded()) return;

    const now = Date.now();

    const hitAreas = [
      LAppDefine.HitAreaNameBangs,
      LAppDefine.HitAreaNameRightHair,
      LAppDefine.HitAreaNameDrink,
      LAppDefine.HitAreaNameCake,
      LAppDefine.HitAreaNameLeftHair
    ];

    for (const area of hitAreas) {
      if (model.hitTest(area, x, y)) {
        if (area === this._lastTapArea && now - this._lastTapTime < 1000) {
          return;
        }
        this._lastTapTime = now;
        this._lastTapArea = area;
        model.playHitAreaMotion(area);
        break;
      }
    }
  }

  public onUpdate(): void {
    const gl = this._subdelegate.getGl();
    CubismWebGLOffscreenManager.getInstance().beginFrameProcess(gl);

    const { width, height } = this._subdelegate.getCanvas();

    const projection = new CubismMatrix44();
    const model = this._models[0];

    if (model && model.getModel()) {
      if (model.getModel().getCanvasWidth() > 1.0 && width < height) {
        model.getModelMatrix().setWidth(2.0);
        projection.scale(1.0, width / height);
      } else {
        projection.scale(height / width, 1.0);
      }

      if (this._viewMatrix != null) {
        projection.multiplyByMatrix(this._viewMatrix);
      }

      model.update();
      model.draw(projection);
    } else if (model) {
      // Model exists but getModel() returns null - log once
      if (!this._loggedModelWait) {
        console.log('[Live2D] Waiting for model to initialize...');
        this._loggedModelWait = true;
      }
    }

    CubismWebGLOffscreenManager.getInstance().endFrameProcess(gl);
    CubismWebGLOffscreenManager.getInstance().releaseStaleRenderTextures(gl);
  }

  public setViewMatrix(m: CubismMatrix44): void {
    for (let i = 0; i < 16; i++) {
      this._viewMatrix.getArray()[i] = m.getArray()[i];
    }
  }

  public addModel(sceneIndex: number = 0): void {
    this._sceneIndex = sceneIndex;
    this.changeScene(this._sceneIndex);
  }

  private changeScene(index: number): void {
    this._sceneIndex = index;

    const modelJsonName = LAppDefine.ModelDir[index] + '.model3.json';
    const modelPath = LAppDefine.ResourcesPath;

    this.releaseAllModel();
    const instance = new LAppModel();
    instance.setSubdelegate(this._subdelegate);
    instance.loadAssets(modelPath, modelJsonName);
    this._models.push(instance);
  }

  public constructor() {
    this._subdelegate = null;
    this._viewMatrix = new CubismMatrix44();
    this._models = [];
    this._sceneIndex = 0;
    this._loggedModelWait = false;
  }

  public release(): void {}

  public getModel(): LAppModel | null {
    return this._models[0] ?? null
  }

  public initialize(subdelegate: LAppSubdelegate): void {
    this._subdelegate = subdelegate;
    this.changeScene(this._sceneIndex);
  }

  private _subdelegate: LAppSubdelegate;
  _viewMatrix: CubismMatrix44;
  _models: LAppModel[];
  private _sceneIndex: number;
  private _loggedModelWait: boolean;

  beganMotion = (self: ACubismMotion): void => {};
  finishedMotion = (self: ACubismMotion): void => {};
}
