import { CubismDefaultParameterId } from '@framework/cubismdefaultparameterid';
import { CubismModelSettingJson } from '@framework/cubismmodelsettingjson';
import { BreathParameterData, CubismBreath } from '@framework/effect/cubismbreath';
import { CubismEyeBlink } from '@framework/effect/cubismeyeblink';
import { LookParameterData, CubismLook } from '@framework/effect/cubismlook';
import { ICubismModelSetting } from '@framework/icubismmodelsetting';
import { CubismIdHandle } from '@framework/id/cubismid';
import { CubismFramework } from '@framework/live2dcubismframework';
import { CubismMatrix44 } from '@framework/math/cubismmatrix44';
import { CubismUserModel } from '@framework/model/cubismusermodel';
import { ACubismMotion, FinishedMotionCallback, BeganMotionCallback } from '@framework/motion/acubismmotion';
import { CubismMotion } from '@framework/motion/cubismmotion';
import { CubismMotionQueueEntryHandle, InvalidMotionQueueEntryHandleValue } from '@framework/motion/cubismmotionqueuemanager';
import { CubismUpdateScheduler } from '@framework/motion/cubismupdatescheduler';
import { CubismBreathUpdater } from '@framework/motion/cubismbreathupdater';
import { CubismLookUpdater } from '@framework/motion/cubismlookupdater';
import { CubismEyeBlinkUpdater } from '@framework/motion/cubismeyeblinkupdater';
import { CubismExpressionUpdater } from '@framework/motion/cubismexpressionupdater';
import { CubismPhysicsUpdater } from '@framework/motion/cubismphysicsupdater';
import { CubismPoseUpdater } from '@framework/motion/cubismposeupdater';
import { csmRect } from '@framework/type/csmrectf';
import { CubismMoc } from '@framework/model/cubismmoc';

import * as LAppDefine from './lappdefine';
import { LAppPal } from './lapppal';
import { TextureInfo } from './lapptexturemanager';
import { LAppSubdelegate } from './lappsubdelegate';

enum LoadStep {
  LoadAssets,
  LoadModel,
  WaitLoadModel,
  LoadExpression,
  WaitLoadExpression,
  LoadPhysics,
  WaitLoadPhysics,
  LoadPose,
  WaitLoadPose,
  SetupEyeBlink,
  SetupBreath,
  LoadUserData,
  WaitLoadUserData,
  SetupEyeBlinkIds,
  SetupLipSyncIds,
  SetupLook,
  SetupLayout,
  LoadMotion,
  WaitLoadMotion,
  CompleteInitialize,
  CompleteSetupModel,
  LoadTexture,
  WaitLoadTexture,
  CompleteSetup
}

export class LAppModel extends CubismUserModel {
  public loadAssets(dir: string, fileName: string): void {
    this._modelHomeDir = dir;
    console.log(`[Live2D] Loading model: ${dir}${fileName}`);

    fetch(`${this._modelHomeDir}${fileName}`)
      .then(response => {
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.arrayBuffer();
      })
      .then(arrayBuffer => {
        const setting: ICubismModelSetting = new CubismModelSettingJson(
          arrayBuffer,
          arrayBuffer.byteLength
        );
        this._state = LoadStep.LoadModel;
        this.setupModel(setting);
      })
      .catch(error => {
        console.error(`Failed to load file ${this._modelHomeDir}${fileName}`, error);
      });
  }

  private setupModel(setting: ICubismModelSetting): void {
    this._updating = true;
    this._initialized = false;
    this._modelSetting = setting;

    if (this._modelSetting.getModelFileName() != '') {
      const modelFileName = this._modelSetting.getModelFileName();
      const mocUrl = `${this._modelHomeDir}${modelFileName}`;
      console.log(`[Live2D] Fetching moc3: ${mocUrl}`);

      fetch(mocUrl)
        .then(response => {
          console.log(`[Live2D] moc3 response: ${response.status}, type: ${response.headers.get('content-type')}`);
          if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
          }
          return response.arrayBuffer();
        })
        .then(arrayBuffer => {
          console.log(`[Live2D] moc3 size: ${arrayBuffer.byteLength} bytes`);
          if (arrayBuffer && arrayBuffer.byteLength > 0) {
            this.loadModel(arrayBuffer, this._mocConsistency);
            console.log(`[Live2D] loadModel done, model: ${this.getModel() != null}`);
            this.loadAllResources();
          }
        })
        .catch(error => {
          console.error('[Live2D] Failed to load moc3:', error);
        });

      this._state = LoadStep.WaitLoadModel;
    }
  }

  private loadAllResources(): void {
    this._state = LoadStep.LoadExpression;

    const eyeBlinkCount = this._modelSetting.getEyeBlinkParameterCount();
    this._eyeBlinkIds.length = eyeBlinkCount;
    for (let i = 0; i < eyeBlinkCount; ++i) {
      this._eyeBlinkIds[i] = this._modelSetting.getEyeBlinkParameterId(i);
    }

    const lipSyncCount = this._modelSetting.getLipSyncParameterCount();
    this._lipSyncIds.length = lipSyncCount;
    for (let i = 0; i < lipSyncCount; ++i) {
      this._lipSyncIds[i] = this._modelSetting.getLipSyncParameterId(i);
    }

    const fetchPromises: Promise<void>[] = [];

    const expressionCount = this._modelSetting.getExpressionCount();
    for (let i = 0; i < expressionCount; i++) {
      const expressionName = this._modelSetting.getExpressionName(i);
      const expressionFileName = this._modelSetting.getExpressionFileName(i);
      fetchPromises.push(
        fetch(`${this._modelHomeDir}${expressionFileName}`)
          .then(r => r.ok ? r.arrayBuffer() : new ArrayBuffer(0))
          .then(ab => {
            const motion = this.loadExpression(ab, ab.byteLength, expressionName);
            if (this._expressions.get(expressionName) != null) {
              ACubismMotion.delete(this._expressions.get(expressionName));
              this._expressions.set(expressionName, null);
            }
            this._expressions.set(expressionName, motion);
          })
      );
    }

    if (this._modelSetting.getPhysicsFileName() != '') {
      fetchPromises.push(
        fetch(`${this._modelHomeDir}${this._modelSetting.getPhysicsFileName()}`)
          .then(r => r.ok ? r.arrayBuffer() : new ArrayBuffer(0))
          .then(ab => this.loadPhysics(ab, ab.byteLength))
      );
    }

    if (this._modelSetting.getPoseFileName() != '') {
      fetchPromises.push(
        fetch(`${this._modelHomeDir}${this._modelSetting.getPoseFileName()}`)
          .then(r => r.ok ? r.arrayBuffer() : new ArrayBuffer(0))
          .then(ab => this.loadPose(ab, ab.byteLength))
      );
    }

    if (this._modelSetting.getUserDataFile() != '') {
      fetchPromises.push(
        fetch(`${this._modelHomeDir}${this._modelSetting.getUserDataFile()}`)
          .then(r => r.ok ? r.arrayBuffer() : new ArrayBuffer(0))
          .then(ab => this.loadUserData(ab, ab.byteLength))
      );
    }

    this._model.saveParameters();
    this._allMotionCount = 0;
    this._motionCount = 0;

    const motionGroupCount = this._modelSetting.getMotionGroupCount();
    const group: string[] = [];
    for (let i = 0; i < motionGroupCount; i++) {
      group[i] = this._modelSetting.getMotionGroupName(i);
      this._allMotionCount += this._modelSetting.getMotionCount(group[i]);
    }
    console.log(`[Live2D] Loading motions: ${motionGroupCount} groups, ${this._allMotionCount} total entries`);

    for (let i = 0; i < motionGroupCount; i++) {
      const groupName = group[i];
      const motionCount = this._modelSetting.getMotionCount(groupName);
      for (let j = 0; j < motionCount; j++) {
        const motionFileName = this._modelSetting.getMotionFileName(groupName, j);
        const name = `${groupName}_${j}`;

        if (!motionFileName || motionFileName === '' || motionFileName === 'undefined' || motionFileName === 'NullValue') {
          this._allMotionCount--;
          continue;
        }

        const url = `${this._modelHomeDir}${motionFileName}`;
        fetchPromises.push(
          fetch(url)
            .then(r => r.ok ? r.arrayBuffer() : null)
            .then(ab => {
              if (!ab || ab.byteLength === 0) {
                this._allMotionCount--;
                return;
              }
              const tmpMotion = this.loadMotion(
                ab, ab.byteLength, name, null, null,
                this._modelSetting, groupName, j, this._motionConsistency
              );
              if (tmpMotion != null) {
                tmpMotion.setEffectIds(this._eyeBlinkIds, this._lipSyncIds);
                if (this._motions.get(name) != null) {
                  ACubismMotion.delete(this._motions.get(name));
                }
                this._motions.set(name, tmpMotion);
                this._motionCount++;
              } else {
                this._allMotionCount--;
              }
            })
            .catch(() => {
              this._allMotionCount--;
            })
        );
      }
    }

    console.log(`[Live2D] Fetching ${fetchPromises.length} resources in parallel...`);
    this._state = LoadStep.WaitLoadMotion;

    Promise.all(fetchPromises).then(() => {
      console.log(`[Live2D] All resources loaded (${this._motionCount}/${this._allMotionCount} motions)`);

      if (expressionCount > 0 && this._expressionManager != null) {
        const expressionUpdater = new CubismExpressionUpdater(this._expressionManager);
        this._updateScheduler.addUpdatableList(expressionUpdater);
      }
      if (this._physics) {
        const physicsUpdater = new CubismPhysicsUpdater(this._physics);
        this._updateScheduler.addUpdatableList(physicsUpdater);
      }
      if (this._pose) {
        const poseUpdater = new CubismPoseUpdater(this._pose);
        this._updateScheduler.addUpdatableList(poseUpdater);
      }

      if (eyeBlinkCount > 0) {
        this._eyeBlink = CubismEyeBlink.create(this._modelSetting);
        const eyeBlinkUpdater = new CubismEyeBlinkUpdater(
          () => this._motionUpdated,
          this._eyeBlink
        );
        this._updateScheduler.addUpdatableList(eyeBlinkUpdater);
      }

      this._breath = CubismBreath.create();
      const breathParameters: BreathParameterData[] = [
        new BreathParameterData(this._idParamAngleX, 0.0, 15.0, 6.5345, 0.5),
        new BreathParameterData(this._idParamAngleY, 0.0, 8.0, 3.5345, 0.5),
        new BreathParameterData(this._idParamAngleZ, 0.0, 10.0, 5.5345, 0.5),
        new BreathParameterData(this._idParamBodyAngleX, 0.0, 4.0, 15.5345, 0.5),
        new BreathParameterData(
          CubismFramework.getIdManager().getId(CubismDefaultParameterId.ParamBreath),
          0.5, 0.5, 3.2345, 1
        )
      ];
      this._breath.setParameters(breathParameters);
      const breathUpdater = new CubismBreathUpdater(this._breath);
      this._updateScheduler.addUpdatableList(breathUpdater);

      this._look = CubismLook.create();
      const lookParameters: LookParameterData[] = [
        new LookParameterData(this._idParamAngleX, 15.0, 0.0, 0.0),
        new LookParameterData(this._idParamAngleY, 0.0, 15.0, 0.0),
        new LookParameterData(this._idParamAngleZ, 0.0, 0.0, -15.0),
        new LookParameterData(this._idParamBodyAngleX, 5.0, 0.0, 0.0),
        new LookParameterData(
          CubismFramework.getIdManager().getId(CubismDefaultParameterId.ParamEyeBallX),
          0.8, 0.0, 0.0
        ),
        new LookParameterData(
          CubismFramework.getIdManager().getId(CubismDefaultParameterId.ParamEyeBallY),
          0.0, 0.8, 0.0
        )
      ];
      this._look.setParameters(lookParameters);
      const lookUpdater = new CubismLookUpdater(this._look, this._dragManager);
      this._updateScheduler.addUpdatableList(lookUpdater);

      this._updateScheduler.sortUpdatableList();

      const layout: Map<string, number> = new Map();
      if (this._modelSetting != null && this._modelMatrix != null) {
        this._modelSetting.getLayoutMap(layout);
        this._modelMatrix.setupFromLayout(layout);
      }

      this._state = LoadStep.LoadTexture;
      this._motionManager.stopAllMotions();
      this._updating = false;
      this._initialized = true;

      try {
        this.createRenderer(
          this._subdelegate.getCanvas().width,
          this._subdelegate.getCanvas().height
        );
        this.getRenderer().startUp(this._subdelegate.getGlManager().getGl());
        this.getRenderer().loadShaders(LAppDefine.ShaderPath);
      } catch (e) {
        console.error('[Live2D] Renderer initialization failed:', e);
      }
      this.setupTextures();
    });
  }

  private setupTextures(): void {
    const usePremultiply = true;

    if (this._state == LoadStep.LoadTexture) {
      const textureCount = this._modelSetting.getTextureCount();
      console.log(`[Live2D] Loading ${textureCount} texture(s)...`);

      for (let i = 0; i < textureCount; i++) {
        if (this._modelSetting.getTextureFileName(i) == '') continue;

        let texturePath = this._modelSetting.getTextureFileName(i);
        texturePath = this._modelHomeDir + texturePath;
        console.log(`[Live2D] Fetching texture: ${texturePath}`);

        const onLoad = (textureInfo: TextureInfo): void => {
          this.getRenderer().bindTexture(i, textureInfo.id);
          this._textureCount++;
          console.log(`[Live2D] Texture loaded: ${texturePath} (${this._textureCount}/${textureCount})`);

          if (this._textureCount >= textureCount) {
            this._state = LoadStep.CompleteSetup;
            console.log('[Live2D] Model setup complete, ready to render');
          }
        };

        this._subdelegate
          .getTextureManager()
          .createTextureFromPngFile(texturePath, usePremultiply, onLoad);
        this.getRenderer().setIsPremultipliedAlpha(usePremultiply);
      }

      this._state = LoadStep.WaitLoadTexture;
    }
  }

  public reloadRenderer(): void {
    this.deleteRenderer();
    this.createRenderer(
      this._subdelegate.getCanvas().width,
      this._subdelegate.getCanvas().height
    );
    this.setupTextures();
  }

  public update(): void {
    if (this._state != LoadStep.CompleteSetup) return;

    const deltaTimeSeconds = LAppPal.getDeltaTime();
    this._userTimeSeconds += deltaTimeSeconds;

    this._model.loadParameters();
    this._motionUpdated = false;

    if (this._motionManager.isFinished()) {
      this.startRandomMotion(LAppDefine.MotionGroupIdle, LAppDefine.PriorityIdle);
    } else {
      this._motionUpdated = this._motionManager.updateMotion(
        this._model,
        deltaTimeSeconds
      );
    }
    this._model.saveParameters();

    this._updateScheduler.onLateUpdate(this._model, deltaTimeSeconds);

    this._model.update();
  }

  public startMotion(
    group: string,
    no: number,
    priority: number,
    onFinishedMotionHandler?: FinishedMotionCallback,
    onBeganMotionHandler?: BeganMotionCallback
  ): CubismMotionQueueEntryHandle {
    if (priority == LAppDefine.PriorityForce) {
      this._motionManager.setReservePriority(priority);
    } else if (!this._motionManager.reserveMotion(priority)) {
      return InvalidMotionQueueEntryHandleValue;
    }

    const motionFileName = this._modelSetting.getMotionFileName(group, no);
    const name = `${group}_${no}`;
    let motion: CubismMotion = this._motions.get(name) as CubismMotion;
    let autoDelete = false;

    if (motion == null) {
      if (!motionFileName || motionFileName === '' || motionFileName === 'undefined') {
        this._motionManager.setReservePriority(LAppDefine.PriorityNone);
        return InvalidMotionQueueEntryHandleValue;
      }

      const url = `${this._modelHomeDir}${motionFileName}`;

      fetch(url)
        .then(response => {
          if (!response.ok) return null;
          return response.arrayBuffer();
        })
        .then(arrayBuffer => {
          if (!arrayBuffer || arrayBuffer.byteLength === 0) {
            this._motionManager.setReservePriority(LAppDefine.PriorityNone);
            return;
          }
          try {
            motion = this.loadMotion(
              arrayBuffer,
              arrayBuffer.byteLength,
              null,
              onFinishedMotionHandler,
              onBeganMotionHandler,
              this._modelSetting,
              group,
              no,
              this._motionConsistency
            );
            if (motion) {
              motion.setEffectIds(this._eyeBlinkIds, this._lipSyncIds);
            }
          } catch (e) {
            // ignore parse errors
          }
        })
        .catch(() => {});

      if (motion) {
        motion.setEffectIds(this._eyeBlinkIds, this._lipSyncIds);
        autoDelete = true;
      } else {
        this._motionManager.setReservePriority(LAppDefine.PriorityNone);
        return InvalidMotionQueueEntryHandleValue;
      }
    } else {
      motion.setBeganMotionHandler(onBeganMotionHandler);
      motion.setFinishedMotionHandler(onFinishedMotionHandler);
    }

    if (this._currentAudio) {
      this._currentAudio.pause();
      this._currentAudio.currentTime = 0;
      this._currentAudio = null;
    }

    const voice = this._modelSetting.getMotionSoundFileName(group, no);
    if (voice.localeCompare('') != 0) {
      const audio = new Audio(this._modelHomeDir + voice);
      this._currentAudio = audio;
      audio.play().catch(() => {});
    }

    return this._motionManager.startMotionPriority(motion, autoDelete, priority);
  }

  public startRandomMotion(
    group: string,
    priority: number,
    onFinishedMotionHandler?: FinishedMotionCallback,
    onBeganMotionHandler?: BeganMotionCallback
  ): CubismMotionQueueEntryHandle {
    if (this._modelSetting.getMotionCount(group) == 0) {
      return InvalidMotionQueueEntryHandleValue;
    }

    const no = Math.floor(Math.random() * this._modelSetting.getMotionCount(group));
    return this.startMotion(group, no, priority, onFinishedMotionHandler, onBeganMotionHandler);
  }

  public setExpression(expressionId: string): void {
    const motion = this._expressions.get(expressionId);
    if (motion != null) {
      this._expressionManager.startMotion(motion, false);
    }
  }

  public setRandomExpression(): void {
    if (this._expressions.size == 0) return;

    const no = Math.floor(Math.random() * this._expressions.size);
    const expressionsArray = [...this._expressions.entries()];
    const name = expressionsArray[no][0];
    this.setExpression(name);
  }

  public hitTest(hitAreaName: string, x: number, y: number): boolean {
    if (this._opacity < 1) return false;

    const count = this._modelSetting.getHitAreasCount();
    for (let i = 0; i < count; i++) {
      if (this._modelSetting.getHitAreaName(i) == hitAreaName) {
        const drawId = this._modelSetting.getHitAreaId(i);
        return this.isHit(drawId, x, y);
      }
    }
    return false;
  }

  public playHitAreaMotion(hitAreaName: string): void {
    const setting = this._modelSetting as any;
    const json = setting.getJson();
    const root = json.getRoot();
    const hitAreas = root.getValueByString('HitAreas');

    for (let i = 0; i < hitAreas.getSize(); i++) {
      const area = hitAreas.getValueByIndex(i);
      if (area.getValueByString('Name').getRawString() === hitAreaName) {
        const motionRef = area.getValueByString('Motion').getRawString();
        if (!motionRef) return;

        const colonIdx = motionRef.indexOf(':');
        if (colonIdx < 0) return;

        const group = motionRef.substring(0, colonIdx);
        const motionName = motionRef.substring(colonIdx + 1);

        const motionCount = this._modelSetting.getMotionCount(group);
        let entryIndex = -1;

        for (let j = 0; j < motionCount; j++) {
          const entry = root.getValueByString('FileReferences')
            .getValueByString('Motions')
            .getValueByString(group)
            .getValueByIndex(j);
          if (entry.getValueByString('Name').getRawString() === motionName) {
            entryIndex = j;
            break;
          }
        }

        if (entryIndex < 0) return;

        const visited = new Set<string>();
        let currentGroup = group;
        let currentIndex = entryIndex;
        let finalExpr: string | null = null;
        let finalGroup: string | null = null;
        let finalIndex = -1;

        for (let depth = 0; depth < 10; depth++) {
          const key = `${currentGroup}:${currentIndex}`;
          if (visited.has(key)) break;
          visited.add(key);

          const entry = root.getValueByString('FileReferences')
            .getValueByString('Motions')
            .getValueByString(currentGroup)
            .getValueByIndex(currentIndex);

          const exprVal = entry.getValueByString('Expression');
          if (exprVal && !exprVal.isNull()) {
            const exprName = exprVal.getRawString();
            if (exprName && this._expressions.has(exprName)) {
              finalExpr = exprName;
            }
          }

          const file = entry.getValueByString('File');
          if (file && !file.isNull() && file.getRawString() !== '') {
            finalGroup = currentGroup;
            finalIndex = currentIndex;
            break;
          }

          const nextMtn = entry.getValueByString('NextMtn');
          if (!nextMtn || nextMtn.isNull()) break;

          const nextRef = nextMtn.getRawString();
          if (!nextRef) break;

          const nextColon = nextRef.indexOf(':');
          if (nextColon < 0) break;

          const nextGroup = nextRef.substring(0, nextColon);
          const nextName = nextRef.substring(nextColon + 1);
          const nextCount = this._modelSetting.getMotionCount(nextGroup);
          let found = false;

          for (let j = 0; j < nextCount; j++) {
            const ne = root.getValueByString('FileReferences')
              .getValueByString('Motions')
              .getValueByString(nextGroup)
              .getValueByIndex(j);
            if (ne.getValueByString('Name').getRawString() === nextName) {
              currentGroup = nextGroup;
              currentIndex = j;
              found = true;
              break;
            }
          }

          if (!found) break;
        }

        const isPersistent = finalExpr === 'expression1.exp3' || finalExpr === 'expression2.exp3';

        if (isPersistent) {
          if (this._persistentExpressions.has(finalExpr)) {
            this._persistentExpressions.delete(finalExpr);
          } else {
            this._persistentExpressions.add(finalExpr);
          }
        }

        if (hitAreaName === '饮料') {
          this._persistentExpressions.clear();
        }

        if (finalGroup && finalIndex >= 0) {
          this.startMotion(finalGroup, finalIndex, 3);
        } else if (!isPersistent) {
          if (this._currentAudio) {
            this._currentAudio.pause();
            this._currentAudio.currentTime = 0;
            this._currentAudio = null;
          }
        }

        if (this._persistentExpressions.size > 0) {
          for (const expr of this._persistentExpressions) {
            this.setExpression(expr);
          }
        } else if (finalExpr && !isPersistent) {
          this.setExpression(finalExpr);
        }
        return;
      }
    }
  }

  public preLoadMotionGroup(group: string): void {
    for (let i = 0; i < this._modelSetting.getMotionCount(group); i++) {
      const motionFileName = this._modelSetting.getMotionFileName(group, i);
      const name = `${group}_${i}`;

      if (!motionFileName || motionFileName === '' || motionFileName === 'undefined' || motionFileName === 'NullValue') {
        this._allMotionCount--;
        continue;
      }

      const url = `${this._modelHomeDir}${motionFileName}`;

      fetch(url)
        .then(response => {
          if (!response.ok) return null;
          return response.arrayBuffer();
        })
        .then(arrayBuffer => {
          if (!arrayBuffer || arrayBuffer.byteLength === 0) {
            console.warn(`[Live2D] Motion file empty or invalid: ${url}`);
            this._allMotionCount--;
            return;
          }

          try {
            const tmpMotion = this.loadMotion(
              arrayBuffer,
              arrayBuffer.byteLength,
              name,
              null,
              null,
              this._modelSetting,
              group,
              i,
              this._motionConsistency
            );

            if (tmpMotion != null) {
              tmpMotion.setEffectIds(this._eyeBlinkIds, this._lipSyncIds);

              if (this._motions.get(name) != null) {
                ACubismMotion.delete(this._motions.get(name));
              }

              this._motions.set(name, tmpMotion);
              this._motionCount++;
            } else {
              this._allMotionCount--;
            }
          } catch (e) {
            this._allMotionCount--;
          }

          if (this._motionCount >= this._allMotionCount) {
            console.log(`[Live2D] All motions loaded (${this._motionCount}/${this._allMotionCount}), creating renderer...`);
            this._state = LoadStep.LoadTexture;
            this._motionManager.stopAllMotions();
            this._updating = false;
            this._initialized = true;

            try {
              this.createRenderer(
                this._subdelegate.getCanvas().width,
                this._subdelegate.getCanvas().height
              );
              this.getRenderer().startUp(this._subdelegate.getGlManager().getGl());
              this.getRenderer().loadShaders(LAppDefine.ShaderPath);
            } catch (e) {
              console.error('[Live2D] Renderer initialization failed:', e);
            }
            this.setupTextures();
          }
        })
        .catch((err) => {
          console.warn(`[Live2D] Failed to fetch motion: ${url}`, err);
          this._allMotionCount--;
        });
    }
  }

  public doDraw(): void {
    if (this._model == null) return;

    const canvas = this._subdelegate.getCanvas();
    const viewport = [0, 0, canvas.width, canvas.height];

    this.getRenderer().setRenderState(this._subdelegate.getFrameBuffer(), viewport);
    this.getRenderer().drawModel(LAppDefine.ShaderPath);
  }

  public draw(matrix: CubismMatrix44): void {
    if (this._model == null) return;

    if (this._state == LoadStep.CompleteSetup) {
      matrix.multiplyByMatrix(this._modelMatrix);
      this.getRenderer().setMvpMatrix(matrix);
      this.doDraw();
    }
  }

  public setSubdelegate(subdelegate: LAppSubdelegate): void {
    this._subdelegate = subdelegate;
  }

  public isLoaded(): boolean {
    return this._state == LoadStep.CompleteSetup;
  }

  public release(): void {
    if (this._look) {
      CubismLook.delete(this._look);
      this._look = null;
    }
    if (this._updateScheduler) {
      this._updateScheduler.release();
    }
    super.release();
  }

  public constructor() {
    super();

    this._modelSetting = null;
    this._modelHomeDir = null;
    this._userTimeSeconds = 0.0;

    this._eyeBlinkIds = new Array<CubismIdHandle>();
    this._lipSyncIds = new Array<CubismIdHandle>();

    this._motions = new Map();
    this._expressions = new Map();

    this._hitArea = new Array<csmRect>();
    this._userArea = new Array<csmRect>();

    this._idParamAngleX = CubismFramework.getIdManager().getId(CubismDefaultParameterId.ParamAngleX);
    this._idParamAngleY = CubismFramework.getIdManager().getId(CubismDefaultParameterId.ParamAngleY);
    this._idParamAngleZ = CubismFramework.getIdManager().getId(CubismDefaultParameterId.ParamAngleZ);
    this._idParamBodyAngleX = CubismFramework.getIdManager().getId(CubismDefaultParameterId.ParamBodyAngleX);

    if (LAppDefine.MOCConsistencyValidationEnable) {
      this._mocConsistency = true;
    }

    if (LAppDefine.MotionConsistencyValidationEnable) {
      this._motionConsistency = true;
    }

    this._state = LoadStep.LoadAssets;
    this._expressionCount = 0;
    this._textureCount = 0;
    this._motionCount = 0;
    this._allMotionCount = 0;
    this._consistency = false;
    this._look = null;
    this._updateScheduler = new CubismUpdateScheduler();
    this._motionUpdated = false;
    this._currentAudio = null;
    this._persistentExpressions = new Set<string>();
  }

  private _updateScheduler: CubismUpdateScheduler;
  private _motionUpdated: boolean;
  private _subdelegate: LAppSubdelegate;

  _modelSetting: ICubismModelSetting;
  _modelHomeDir: string;
  _userTimeSeconds: number;

  _eyeBlinkIds: CubismIdHandle[];
  _lipSyncIds: CubismIdHandle[];

  _motions: Map<string, ACubismMotion>;
  _expressions: Map<string, ACubismMotion>;

  _hitArea: csmRect[];
  _userArea: csmRect[];

  _idParamAngleX: CubismIdHandle;
  _idParamAngleY: CubismIdHandle;
  _idParamAngleZ: CubismIdHandle;
  _idParamBodyAngleX: CubismIdHandle;

  _look: CubismLook;

  _state: LoadStep;
  _expressionCount: number;
  _textureCount: number;
  _motionCount: number;
  _allMotionCount: number;
  _consistency: boolean;
  _currentAudio: HTMLAudioElement | null;
  _persistentExpressions: Set<string>;
}
