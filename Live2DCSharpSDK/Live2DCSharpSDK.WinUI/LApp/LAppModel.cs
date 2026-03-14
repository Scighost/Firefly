using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Motion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Live2DCSharpSDK.WinUI.LApp;

public class LAppModel : CubismUserModel
{
    /// <summary>
    /// モデルセッティング情報
    /// </summary>
    public readonly ModelSettingObj _modelSetting;
    /// <summary>
    /// モデルセッティングが置かれたディレクトリ
    /// </summary>
    public readonly string _modelHomeDir;
    /// <summary>
    /// モデルに設定されたまばたき機能用パラメータID
    /// </summary>
    public readonly List<string> _eyeBlinkIds = [];
    /// <summary>
    /// モデルに設定されたリップシンク機能用パラメータID
    /// </summary>
    public readonly List<string> _lipSyncIds = [];
    /// <summary>
    /// 読み込まれているモーションのリスト
    /// </summary>
    public readonly Dictionary<string, ACubismMotion> _motions = [];
    /// <summary>
    /// 読み込まれている表情のリスト
    /// </summary>
    public readonly Dictionary<string, ACubismMotion> _expressions = [];

    /// <summary>
    /// グループごとに独立したモーションマネージャー
    /// </summary>
    private readonly Dictionary<string, CubismMotionManager> _groupMotionManagers = [];
    /// <summary>
    /// グループごとの表情マネージャー（SaveParameters後に適用、motionファイルなしの純表情グループ用）
    /// </summary>
    private readonly Dictionary<string, CubismMotionManager> _groupExpressionManagers = [];
    /// <summary>
    /// グループごとの現在再生中モーションのJSONプライオリティ（抢占判断に使用）
    /// </summary>
    private readonly Dictionary<string, int> _groupCurrentPriority = [];

    public IReadOnlyCollection<string> Motions => _motions.Keys;
    public IReadOnlyCollection<string> Expressions => _expressions.Keys;
    public IEnumerable<(string Id, int Index, float Opacity)> Parts
    {
        get
        {
            int count = Model.GetPartCount();
            for (int a = 0; a < count; a++)
                yield return (Model.GetPartId(a), a, Model.GetPartOpacity(a));
        }
    }

    public List<TextureInfo> Textures = [];

    public IEnumerable<string> Parameters => Model.ParameterIds;

    /// <summary>
    /// デルタ時間の積算値[秒]
    /// </summary>
    public float UserTimeSeconds { get; set; }

    public bool RandomMotion { get; set; } = true;
    public bool CustomValueUpdate { get; set; }

    public Action<LAppModel>? ValueUpdate;

    /// <summary>
    /// パラメータID: ParamAngleX
    /// </summary>
    public string IdParamAngleX { get; set; }
    /// <summary>
    /// パラメータID: ParamAngleY
    /// </summary>
    public string IdParamAngleY { get; set; }
    /// <summary>
    /// パラメータID: ParamAngleZ
    /// </summary>
    public string IdParamAngleZ { get; set; }
    /// <summary>
    /// パラメータID: ParamBodyAngleX
    /// </summary>
    public string IdParamBodyAngleX { get; set; }
    /// <summary>
    /// パラメータID: ParamEyeBallX
    /// </summary>
    public string IdParamEyeBallX { get; set; }
    /// <summary>
    /// パラメータID: ParamEyeBallXY
    /// </summary>
    public string IdParamEyeBallY { get; set; }

    public string IdParamBreath { get; set; } = CubismFramework.CubismIdManager
        .GetId(CubismDefaultParameterId.ParamBreath);

    /// <summary>
    /// wavファイルハンドラ（音声再生とリップシンクRMS計算）
    /// </summary>
    private readonly LAppWavFileHandler _wavFileHandler = new();

    private readonly LAppDelegate _lapp;

    private readonly Random _random = new();

    public event Action<LAppModel, string>? Motion;

    public LAppModel(LAppDelegate lapp, string dir, string fileName)
    {
        _lapp = lapp;

        if (LAppDefine.MocConsistencyValidationEnable)
        {
            _mocConsistency = true;
        }

        IdParamAngleX = CubismFramework.CubismIdManager.GetId(CubismDefaultParameterId.ParamAngleX);
        IdParamAngleY = CubismFramework.CubismIdManager.GetId(CubismDefaultParameterId.ParamAngleY);
        IdParamAngleZ = CubismFramework.CubismIdManager.GetId(CubismDefaultParameterId.ParamAngleZ);
        IdParamBodyAngleX = CubismFramework.CubismIdManager.GetId(CubismDefaultParameterId.ParamBodyAngleX);
        IdParamEyeBallX = CubismFramework.CubismIdManager.GetId(CubismDefaultParameterId.ParamEyeBallX);
        IdParamEyeBallY = CubismFramework.CubismIdManager.GetId(CubismDefaultParameterId.ParamEyeBallY);

        _modelHomeDir = dir;

        CubismLog.Debug($"[Live2D App]load model setting: {fileName}");

        using var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        _modelSetting = JsonSerializer.Deserialize(stream, ModelSettingObjContext.Default.ModelSettingObj)
            ?? throw new Exception("model3.json error");

        Updating = true;
        Initialized = false;

        //Cubism Model
        var path = _modelSetting.FileReferences?.Moc;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(_modelHomeDir + path);
            if (!File.Exists(path))
            {
                throw new Exception("model is null");
            }

            CubismLog.Debug($"[Live2D App]create model: {path}");

            LoadModel(File.ReadAllBytes(path), _mocConsistency);
        }

        //Expression
        if (_modelSetting.FileReferences?.Expressions?.Count > 0)
        {
            for (int i = 0; i < _modelSetting.FileReferences.Expressions.Count; i++)
            {
                var item = _modelSetting.FileReferences.Expressions[i];
                string name = item.Name;
                path = item.File;
                path = Path.GetFullPath(_modelHomeDir + path);
                if (!File.Exists(path))
                {
                    continue;
                }

                _expressions[name] = new CubismExpressionMotion(path);
            }
        }

        //Physics
        path = _modelSetting.FileReferences?.Physics;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(_modelHomeDir + path);
            if (File.Exists(path))
            {
                LoadPhysics(path);
            }
        }

        //Pose
        path = _modelSetting.FileReferences?.Pose;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(_modelHomeDir + path);
            if (File.Exists(path))
            {
                LoadPose(path);
            }
        }

        //EyeBlink
        if (_modelSetting.IsExistEyeBlinkParameters())
        {
            _eyeBlink = new CubismEyeBlink(_modelSetting);
        }

        LoadBreath();

        //UserData
        path = _modelSetting.FileReferences?.UserData;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(_modelHomeDir + path);
            if (File.Exists(path))
            {
                LoadUserData(path);
            }
        }

        // EyeBlinkIds
        if (_eyeBlink != null)
        {
            _eyeBlinkIds.AddRange(_eyeBlink.ParameterIds);
        }

        // LipSyncIds
        if (_modelSetting.IsExistLipSyncParameters())
        {
            foreach (var item in _modelSetting.Groups)
            {
                if (item.Name == CubismModelSettingJson.LipSync)
                {
                    _lipSyncIds.AddRange(item.Ids);
                }
            }
        }

        //Layout
        Dictionary<string, float> layout = [];
        _modelSetting.GetLayoutMap(layout);
        ModelMatrix.SetupFromLayout(layout);

        Model.SaveParameters();

        if (_modelSetting.FileReferences?.Motions?.Count > 0)
        {
            foreach (var item in _modelSetting.FileReferences.Motions)
            {
                PreloadMotionGroup(item.Key);
            }
        }

        _motionManager.StopAllMotions();

        // グループ専用のモーションマネージャーを初期化
        if (_modelSetting.FileReferences?.Motions != null)
        {
            foreach (var group in _modelSetting.FileReferences.Motions.Keys)
            {
                _groupMotionManagers[group] = new CubismMotionManager();
                _groupExpressionManagers[group] = new CubismMotionManager();
                _groupCurrentPriority[group] = 0;
            }
        }

        Updating = false;
        Initialized = true;
        if (Renderer != null)
        {
            DeleteRenderer();
        }
        Renderer = lapp.CreateRenderer(Model);

        SetupTextures();
    }

    public new void Dispose()
    {
        base.Dispose();

        _wavFileHandler.Dispose();
        _motions.Clear();
        _expressions.Clear();

        foreach (var item in Textures)
        {
            _lapp.TextureManager.ReleaseTexture(item);
        }
        Textures.Clear();
    }

    public void LoadBreath()
    {
        //Breath
        _breath = new()
        {
            Parameters =
            [
                new()
                {
                    ParameterId = IdParamAngleX,
                    Offset = 0.0f,
                    Peak = 15.0f,
                    Cycle = 6.5345f,
                    Weight = 0.5f
                },
                new()
                {
                    ParameterId = IdParamAngleY,
                    Offset = 0.0f,
                    Peak = 8.0f,
                    Cycle = 3.5345f,
                    Weight = 0.5f
                },
                new()
                {
                    ParameterId = IdParamAngleZ,
                    Offset = 0.0f,
                    Peak = 10.0f,
                    Cycle = 5.5345f,
                    Weight = 0.5f
                },
                new()
                {
                    ParameterId = IdParamBodyAngleX,
                    Offset = 0.0f,
                    Peak = 4.0f,
                    Cycle = 15.5345f,
                    Weight = 0.5f
                },
                new()
                {
                    ParameterId = IdParamBreath,
                    Offset = 0.5f,
                    Peak = 0.5f,
                    Cycle = 3.2345f,
                    Weight = 0.5f
                }
            ]
        };
    }

    /// <summary>
    /// レンダラを再構築する
    /// </summary>
    public void ReloadRenderer()
    {
        DeleteRenderer();

        CreateRenderer(_lapp.CreateRenderer(Model));

        SetupTextures();
    }

    /// <summary>
    /// モデルの更新処理。モデルのパラメータから描画状態を決定する。
    /// </summary>
    public void Update()
    {
        float deltaTimeSeconds = LAppPal.DeltaTime;
        UserTimeSeconds += deltaTimeSeconds;

        _dragManager.Update(deltaTimeSeconds);
        _dragX = _dragManager.FaceX;
        _dragY = _dragManager.FaceY;

        // モーションによるパラメータ更新の有無
        bool motionUpdated = false;

        //-----------------------------------------------------------------
        Model.LoadParameters(); // 前回セーブされた状態をロード

        // 各グループのモーションマネージャーを独立して更新
        foreach (var (group, mgr) in _groupMotionManagers)
        {
            motionUpdated |= mgr.UpdateMotion(Model, deltaTimeSeconds);
            if (mgr.IsFinished())
                _groupCurrentPriority[group] = 0;
        }

        // アイドルグループが終了していたらランダム再生
        if (RandomMotion)
        {
            string idleGroup = LAppDefine.MotionGroupIdle;
            bool idleFinished = !_groupMotionManagers.TryGetValue(idleGroup, out var idleMgr)
                || idleMgr.IsFinished();
            if (idleFinished)
            {
                StartRandomMotion(idleGroup, MotionPriority.PriorityIdle);
            }
        }

        Model.SaveParameters(); // 状態を保存

        //-----------------------------------------------------------------

        // 不透明度
        Opacity = Model.GetModelOpacity();

        // まばたき
        if (!motionUpdated)
        {
            // メインモーションの更新がないとき
            _eyeBlink?.UpdateParameters(Model, deltaTimeSeconds); // 目パチ
        }

        _expressionManager?.UpdateMotion(Model, deltaTimeSeconds); // 表情でパラメータ更新（相対変化）
        // 純表情グループはSaveParameters後に適用（motionファイルが上書きしないよう）
        foreach (var (_, mgr) in _groupExpressionManagers)
        {
            mgr.UpdateMotion(Model, deltaTimeSeconds);
        }

        if (CustomValueUpdate)
        {
            ValueUpdate?.Invoke(this);
        }
        else
        {
            //ドラッグによる変化
            //ドラッグによる顔の向きの調整
            Model.AddParameterValue(IdParamAngleX, _dragX * 30); // -30から30の値を加える
            Model.AddParameterValue(IdParamAngleY, _dragY * 30);
            Model.AddParameterValue(IdParamAngleZ, _dragX * _dragY * -30);

            //ドラッグによる体の向きの調整
            Model.AddParameterValue(IdParamBodyAngleX, _dragX * 10); // -10から10の値を加える

            //ドラッグによる目の向きの調整
            Model.AddParameterValue(IdParamEyeBallX, _dragX); // -1から1の値を加える
            Model.AddParameterValue(IdParamEyeBallY, _dragY);
        }

        // 呼吸など
        _breath?.UpdateParameters(Model, deltaTimeSeconds);

        // 物理演算の設定
        _physics?.Evaluate(Model, deltaTimeSeconds);

        // リップシンクの設定
        if (_lipSync)
        {
            _wavFileHandler.Update(deltaTimeSeconds);
            float value = _wavFileHandler.GetRms();

            for (int i = 0; i < _lipSyncIds.Count; ++i)
            {
                Model.AddParameterValue(_lipSyncIds[i], value, 0.8f);
            }
        }

        // ポーズの設定
        _pose?.UpdateParameters(Model, deltaTimeSeconds);

        Model.Update();
    }

    /// <summary>
    /// モデルを描画する処理。モデルを描画する空間のView-Projection行列を渡す。
    /// </summary>
    /// <param name="matrix">View-Projection行列</param>
    public void Draw(CubismMatrix44 matrix)
    {
        if (Model == null)
        {
            return;
        }

        matrix.MultiplyByMatrix(ModelMatrix);
        if (Renderer != null)
        {
            Renderer.SetMvpMatrix(matrix);
        }

        DoDraw();
    }

    /// <summary>
    /// 引数で指定したモーションの再生を開始する。
    /// </summary>
    /// <param name="group">モーショングループ名</param>
    /// <param name="no">グループ内の番号</param>
    /// <param name="priority">優先度</param>
    /// <param name="onFinishedMotionHandler">モーション再生終了時に呼び出されるコールバック関数。NULLの場合、呼び出されない。</param>
    /// <returns>開始したモーションの識別番号を返す。個別のモーションが終了したか否かを判定するIsFinished()の引数で使用する。開始できない時は「-1」</returns>
    public CubismMotionQueueEntry? StartMotion(string name, MotionPriority priority, FinishedMotionCallback? onFinishedMotionHandler = null)
    {
        var temp = name.Split("_");
        if (temp.Length != 2)
        {
            throw new Exception("motion name error");
        }
        return StartMotion(temp[0], int.Parse(temp[1]), priority, onFinishedMotionHandler);
    }

    /// <summary>
    /// "GroupName:MotionName" 形式の参照でモーションを開始する。NextMtnが存在する場合は連鎖して再生する。
    /// </summary>
    public CubismMotionQueueEntry? StartMotionByRef(string motionRef, MotionPriority priority)
    {
        var parts = motionRef.Split(':', 2);
        if (parts.Length != 2) return null;

        string group = parts[0];
        string motionName = parts[1];

        var motions = _modelSetting.FileReferences?.Motions;
        if (motions == null || !motions.TryGetValue(group, out var list)) return null;

        int index = list.FindIndex(m => m.Name == motionName);
        if (index < 0) return null;

        string? nextMtn = list[index].NextMtn;
        FinishedMotionCallback? callback = null;
        if (!string.IsNullOrEmpty(nextMtn))
        {
            callback = (_, _) => StartMotionByRef(nextMtn, MotionPriority.PriorityForce);
        }

        // 没有 File 时是纯表情触发动作，走该组专属表情 manager（SaveParameters后适用）
        if (string.IsNullOrEmpty(list[index].File))
        {
            string? expr = list[index].Expression;
            if (!string.IsNullOrEmpty(expr) && _expressions.TryGetValue(expr, out var exprMotion))
            {
                if (_groupExpressionManagers.TryGetValue(group, out var grpMgr))
                {
                    grpMgr.StartMotionPriority(exprMotion, MotionPriority.PriorityForce);
                }
                else
                    SetExpression(expr);
            }
            callback?.Invoke(null!, null!);
            return null;
        }

        return StartMotion(group, index, priority, callback);
    }

    public CubismMotionQueueEntry? StartMotion(string group, int no, MotionPriority priority, FinishedMotionCallback? onFinishedMotionHandler = null)
    {
        var item = _modelSetting.FileReferences.Motions[group][no];

        //ex) idle_0
        string name = $"{group}_{no}";

        // File が空の場合は表情のみ切り替え、グループ専属表情 manager で独立再生（SaveParameters後適用）
        if (string.IsNullOrEmpty(item.File))
        {
            if (!string.IsNullOrEmpty(item.Expression) && _expressions.TryGetValue(item.Expression, out var exprMotion))
            {
                if (_groupExpressionManagers.TryGetValue(group, out var grpMgr))
                {
                    grpMgr.StartMotionPriority(exprMotion, MotionPriority.PriorityForce);
                }
                else
                    SetExpression(item.Expression);
            }
            onFinishedMotionHandler?.Invoke(null!, null!);
            return null;
        }

        // グループ専用マネージャーを取得または作成
        if (!_groupMotionManagers.TryGetValue(group, out var groupManager))
        {
            groupManager = new CubismMotionManager();
            _groupMotionManagers[group] = groupManager;
        }

        // 同グループで再生中のモーションがある場合、JSONプライオリティで抢占を判断する
        if (!groupManager.IsFinished())
        {
            int curPriority = _groupCurrentPriority.GetValueOrDefault(group);
            if (item.Priority <= curPriority)
            {
                CubismLog.Debug($"[Live2D App]can't start motion: priority {item.Priority} <= current {curPriority}.");
                return null;
            }
        }

        if (priority == MotionPriority.PriorityForce)
        {
            groupManager.ReservePriority = priority;
        }
        else if (!groupManager.ReserveMotion(priority))
        {
            CubismLog.Debug("[Live2D App]can't start motion.");
            return null;
        }

        if (!_motions.TryGetValue(name, out var value))
        {
            string path = Path.GetFullPath(_modelHomeDir + item.File);
            if (!File.Exists(path))
                return null;

            var newMotion = new CubismMotion(path);
            float fadeTime = item.FadeInTime;
            if (fadeTime >= 0.0f)
                newMotion.FadeInSeconds = fadeTime;
            fadeTime = item.FadeOutTime;
            if (fadeTime >= 0.0f)
                newMotion.FadeOutSeconds = fadeTime;
            newMotion.SetEffectIds(_eyeBlinkIds, _lipSyncIds);
            _motions[name] = value = newMotion;
        }
        var motion = (CubismMotion)value!;
        motion.OnFinishedMotion = onFinishedMotionHandler;

        //voice
        string voice = item.Sound;
        if (!string.IsNullOrWhiteSpace(voice))
        {
            string soundPath = Path.GetFullPath(_modelHomeDir + voice);
            _wavFileHandler.Start(soundPath, item.SoundDelay);
        }

        _groupCurrentPriority[group] = item.Priority;
        CubismLog.Debug($"[Live2D App]start motion: [{group}_{no}] priority={item.Priority}");
        return groupManager.StartMotionPriority(motion, priority);
    }

    /// <summary>
    /// ランダムに選ばれたモーションの再生を開始する。
    /// </summary>
    /// <param name="group">モーショングループ名</param>
    /// <param name="priority">優先度</param>
    /// <param name="onFinishedMotionHandler">モーション再生終了時に呼び出されるコールバック関数。NULLの場合、呼び出されない。</param>
    /// <returns>開始したモーションの識別番号を返す。個別のモーションが終了したか否かを判定するIsFinished()の引数で使用する。開始できない時は「-1」</returns>
    public object? StartRandomMotion(string group, MotionPriority priority, FinishedMotionCallback? onFinishedMotionHandler = null)
    {
        var motionGroups = _modelSetting.FileReferences?.Motions;
        if (motionGroups != null && motionGroups.TryGetValue(group, out var groupList) && groupList.Count > 0)
            return StartMotion(group, _random.Next(groupList.Count), priority, onFinishedMotionHandler);

        return null;
    }

    /// <summary>
    /// 引数で指定した表情モーションをセットする
    /// </summary>
    /// <param name="expressionID">表情モーションのID</param>
    public void SetExpression(string expressionID)
    {
        if (!_expressions.TryGetValue(expressionID, out var motion))
        {
            CubismLog.Debug($"[Live2D App]expression[{expressionID}] is null ");
            return;
        }
        CubismLog.Debug($"[Live2D App]expression: [{expressionID}]");
        _expressionManager.StartMotionPriority(motion, MotionPriority.PriorityForce);
    }

    /// <summary>
    /// ランダムに選ばれた表情モーションをセットする
    /// </summary>
    public void SetRandomExpression()
    {
        if (_expressions.Count == 0)
        {
            return;
        }

        int target = _random.Next(_expressions.Count);
        foreach (var key in _expressions.Keys)
        {
            if (target-- == 0)
            {
                SetExpression(key);
                return;
            }
        }
    }

    /// <summary>
    /// イベントの発火を受け取る
    /// </summary>
    /// <param name="eventValue"></param>
    protected override void MotionEventFired(string eventValue)
    {
        CubismLog.Debug($"[Live2D App]{eventValue} is fired on LAppModel!!");
        Motion?.Invoke(this, eventValue);
    }

    /// <summary>
    /// 当たり判定テスト。
    /// 指定IDの頂点リストから矩形を計算し、座標が矩形範囲内か判定する。
    /// </summary>
    /// <param name="hitAreaName">当たり判定をテストする対象のID</param>
    /// <param name="x">判定を行うX座標</param>
    /// <param name="y">判定を行うY座標</param>
    /// <returns></returns>
    public bool HitTest(string hitAreaName, float x, float y)
    {
        // 透明時は当たり判定なし。
        if (Opacity < 1)
        {
            return false;
        }
        if (_modelSetting.HitAreas?.Count > 0)
        {
            for (int i = 0; i < _modelSetting.HitAreas.Count; i++)
            {
                if (_modelSetting.HitAreas[i].Name == hitAreaName)
                {
                    var id = CubismFramework.CubismIdManager.GetId(_modelSetting.HitAreas[i].Id);

                    return IsHit(id, x, y);
                }
            }
        }
        return false; // 存在しない場合はfalse
    }

    /// <summary>
    /// モデルを描画する処理。モデルを描画する空間のView-Projection行列を渡す。
    /// </summary>
    protected void DoDraw()
    {
        if (Model == null)
        {
            return;
        }

        Renderer?.DrawModel();
    }

    /// <summary>
    /// OpenGLのテクスチャユニットにテクスチャをロードする
    /// </summary>
    private void SetupTextures()
    {
        if (_modelSetting.FileReferences?.Textures?.Count > 0)
        {
            for (int index = 0; index < _modelSetting.FileReferences.Textures.Count; index++)
            {
                var texturePath = _modelSetting.FileReferences.Textures[index];
                if (string.IsNullOrWhiteSpace(texturePath))
                    continue;
                texturePath = Path.GetFullPath(_modelHomeDir + texturePath);
                var texture = _lapp.TextureManager.CreateTextureFromPngFile(this, index, texturePath);
                Textures.Add(texture);
            }
        }
    }

    /// <summary>
    /// モーションデータをグループ名から一括でロードする。
    /// モーションデータの名前は内部でModelSettingから取得する。
    /// </summary>
    /// <param name="group">モーションデータのグループ名</param>
    private void PreloadMotionGroup(string group)
    {
        // グループに登録されているモーション数を取得
        var list = _modelSetting.FileReferences.Motions[group];

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (string.IsNullOrEmpty(item.File))
                continue;
            //ex) idle_0
            string name = $"{group}_{i}";
            var path = Path.GetFullPath(_modelHomeDir + item.File);
            if (!File.Exists(path))
                continue;

            var tmpMotion = new CubismMotion(path);

            float fadeTime = item.FadeInTime;
            if (fadeTime >= 0.0f)
                tmpMotion.FadeInSeconds = fadeTime;
            fadeTime = item.FadeOutTime;
            if (fadeTime >= 0.0f)
                tmpMotion.FadeOutSeconds = fadeTime;
            tmpMotion.SetEffectIds(_eyeBlinkIds, _lipSyncIds);
            _motions[name] = tmpMotion;
        }
    }
}
