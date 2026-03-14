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
    /// 模型设置信息
    /// </summary>
    public readonly ModelSettingObj _modelSetting;
    /// <summary>
    /// 模型设置文件所在目录
    /// </summary>
    public readonly string _modelHomeDir;
    /// <summary>
    /// 模型中设置的眨眼功能参数 ID
    /// </summary>
    public readonly List<string> _eyeBlinkIds = [];
    /// <summary>
    /// 模型中设置的口型同步功能参数 ID
    /// </summary>
    public readonly List<string> _lipSyncIds = [];
    /// <summary>
    /// 已加载的动作列表
    /// </summary>
    public readonly Dictionary<string, ACubismMotion> _motions = [];
    /// <summary>
    /// 已加载的表情列表
    /// </summary>
    public readonly Dictionary<string, ACubismMotion> _expressions = [];

    /// <summary>
    /// 各组独立的动作管理器
    /// </summary>
    private readonly Dictionary<string, CubismMotionManager> _groupMotionManagers = [];
    /// <summary>
    /// 各组的表情管理器（在 SaveParameters 后应用，用于无 motion 文件的纯表情组）
    /// </summary>
    private readonly Dictionary<string, CubismMotionManager> _groupExpressionManagers = [];
    /// <summary>
    /// 各组当前播放动作的 JSON 优先级（用于抓占判断）
    /// </summary>
    private readonly Dictionary<string, int> _groupCurrentPriority = [];
    /// <summary>
    /// 各组当前播放动作是否可被打断
    /// </summary>
    private readonly Dictionary<string, bool> _groupCurrentInterruptable = [];

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
    /// 增量时间的累积値（秒）
    /// </summary>
    public float UserTimeSeconds { get; set; }

    public bool RandomMotion { get; set; } = true;
    public bool CustomValueUpdate { get; set; }

    public Action<LAppModel>? ValueUpdate;

    /// <summary>
    /// 参数 ID: ParamAngleX
    /// </summary>
    public string IdParamAngleX { get; set; }
    /// <summary>
    /// 参数 ID: ParamAngleY
    /// </summary>
    public string IdParamAngleY { get; set; }
    /// <summary>
    /// 参数 ID: ParamAngleZ
    /// </summary>
    public string IdParamAngleZ { get; set; }
    /// <summary>
    /// 参数 ID: ParamBodyAngleX
    /// </summary>
    public string IdParamBodyAngleX { get; set; }
    /// <summary>
    /// 参数 ID: ParamEyeBallX
    /// </summary>
    public string IdParamEyeBallX { get; set; }
    /// <summary>
    /// 参数 ID: ParamEyeBallY
    /// </summary>
    public string IdParamEyeBallY { get; set; }

    public string IdParamBreath { get; set; } = CubismFramework.CubismIdManager
        .GetId(CubismDefaultParameterId.ParamBreath);

    /// <summary>
    /// WAV 文件处理器（音频播放与口型同步 RMS 计算）
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

        // 初始化各组专用的动作管理器
        if (_modelSetting.FileReferences?.Motions != null)
        {
            foreach (var group in _modelSetting.FileReferences.Motions.Keys)
            {
                _groupMotionManagers[group] = new CubismMotionManager();
                _groupExpressionManagers[group] = new CubismMotionManager();
                _groupCurrentPriority[group] = 0;
                _groupCurrentInterruptable[group] = false;
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
    /// 重建渲染器
    /// </summary>
    public void ReloadRenderer()
    {
        DeleteRenderer();

        CreateRenderer(_lapp.CreateRenderer(Model));

        SetupTextures();
    }

    /// <summary>
    /// 模型更新处理。根据模型参数决定绘制状态。
    /// </summary>
    public void Update()
    {
        float deltaTimeSeconds = LAppPal.DeltaTime;
        UserTimeSeconds += deltaTimeSeconds;

        _dragManager.Update(deltaTimeSeconds);
        _dragX = _dragManager.FaceX;
        _dragY = _dragManager.FaceY;

        // 是否由动作更新参数
        bool motionUpdated = false;

        //-----------------------------------------------------------------
        Model.LoadParameters(); // 加载上次保存的状态

        // 独立更新各组的动作管理器
        foreach (var (group, mgr) in _groupMotionManagers)
        {
            motionUpdated |= mgr.UpdateMotion(Model, deltaTimeSeconds);
            if (mgr.IsFinished())
            {
                // 仅重置优先级；_groupCurrentInterruptable 由 StartMotion 内的显式路径
                // （非可打断动作触发时）负责清零，避免纯表情组每帧误清标志
                _groupCurrentPriority[group] = 0;
            }
        }

        // 空闲组结束时随机播放
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

        Model.SaveParameters(); // 保存状态

        //-----------------------------------------------------------------

        // 不透明度
        Opacity = Model.GetModelOpacity();

        // 眨眼
        if (!motionUpdated)
        {
            // 主动作无更新时
            _eyeBlink?.UpdateParameters(Model, deltaTimeSeconds); // 眨眼
        }

        _expressionManager?.UpdateMotion(Model, deltaTimeSeconds); // 表情更新参数（相对变化）
        // 纯表情组在 SaveParameters 后应用（防止被 motion 文件覆盖）
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
            //拖拽产生的变化
            //拖拽调整脸部朝向
            Model.AddParameterValue(IdParamAngleX, _dragX * 30); // 叠加 -30 到 30 的値
            Model.AddParameterValue(IdParamAngleY, _dragY * 30);
            Model.AddParameterValue(IdParamAngleZ, _dragX * _dragY * -30);

            //拖拽调整身体朝向
            Model.AddParameterValue(IdParamBodyAngleX, _dragX * 10); // 叠加 -10 到 10 的値

            //拖拽调整眼球朝向
            Model.AddParameterValue(IdParamEyeBallX, _dragX); // 叠加 -1 到 1 的値
            Model.AddParameterValue(IdParamEyeBallY, _dragY);
        }

        // 呼吸等
        _breath?.UpdateParameters(Model, deltaTimeSeconds);

        // 物理运算设置
        _physics?.Evaluate(Model, deltaTimeSeconds);

        // 口型同步设置
        if (_lipSync)
        {
            _wavFileHandler.Update(deltaTimeSeconds);
            float value = _wavFileHandler.GetRms();

            for (int i = 0; i < _lipSyncIds.Count; ++i)
            {
                Model.AddParameterValue(_lipSyncIds[i], value, 0.8f);
            }
        }

        // 姿势设置
        _pose?.UpdateParameters(Model, deltaTimeSeconds);

        Model.Update();
    }

    /// <summary>
    /// 模型绘制处理。传入用于绘制模型空间的 View-Projection 矩阵。
    /// </summary>
    /// <param name="matrix">View-Projection 矩阵</param>
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
    /// 开始播放指定名称的动作。
    /// </summary>
    /// <param name="group">动作组名称</param>
    /// <param name="no">组内编号</param>
    /// <param name="priority">优先级</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数。为 NULL 时不调用。</param>
    /// <returns>返回已启动动作的标识号。无法启动时返回 "-1"。</returns>
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
    /// 以 "GroupName:MotionName" 格式的引用开始播放动作。存在 NextMtn 时串联播放。
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
            if (list[index].Interruptable)
            {
                // 已有可打断表情在播，不重复触发
                if (_groupCurrentInterruptable.GetValueOrDefault(group))
                    return null;
            }
            else
            {
                // 非可打断表情（如回正）重置标志
                _groupCurrentInterruptable[group] = false;
            }
            string? expr = list[index].Expression;
            if (!string.IsNullOrEmpty(expr) && _expressions.TryGetValue(expr, out var exprMotion))
            {
                if (_groupExpressionManagers.TryGetValue(group, out var grpMgr))
                {
                    // Interruptable 表情立即清空旧队列，不留残影
                    if (list[index].Interruptable)
                    {
                        grpMgr.StopAllMotions();
                        _groupCurrentInterruptable[group] = true;
                    }
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

        // 当 File 为空时仅切换表情，由组专属表情 manager 独立播放（在 SaveParameters 后应用）
        if (string.IsNullOrEmpty(item.File))
        {
            if (item.Interruptable)
            {
                // 已有可打断表情在播，不重复触发
                if (_groupCurrentInterruptable.GetValueOrDefault(group))
                    return null;
            }
            else
            {
                _groupCurrentInterruptable[group] = false;
            }
            if (!string.IsNullOrEmpty(item.Expression) && _expressions.TryGetValue(item.Expression, out var exprMotion))
            {
                if (_groupExpressionManagers.TryGetValue(group, out var grpMgr))
                {
                    // Interruptable 表情立即清空旧队列，不留残影
                    if (item.Interruptable)
                    {
                        grpMgr.StopAllMotions();
                        _groupCurrentInterruptable[group] = true;
                    }
                    grpMgr.StartMotionPriority(exprMotion, MotionPriority.PriorityForce);
                }
                else
                    SetExpression(item.Expression);
            }
            onFinishedMotionHandler?.Invoke(null!, null!);
            return null;
        }

        // 获取或创建组专用管理器
        if (!_groupMotionManagers.TryGetValue(group, out var groupManager))
        {
            groupManager = new CubismMotionManager();
            _groupMotionManagers[group] = groupManager;
        }

        // 同一组有动作播放时，根据 JSON 优先级判断是否抓占
        if (!groupManager.IsFinished())
        {
            bool curInterruptable = _groupCurrentInterruptable.GetValueOrDefault(group);
            int curPriority = _groupCurrentPriority.GetValueOrDefault(group);
            if (!curInterruptable && item.Priority <= curPriority)
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
        _groupCurrentInterruptable[group] = item.Interruptable;
        CubismLog.Debug($"[Live2D App]start motion: [{group}_{no}] priority={item.Priority} interruptable={item.Interruptable}");
        return groupManager.StartMotionPriority(motion, priority);
    }

    /// <summary>
    /// 随机开始播放一个动作。
    /// </summary>
    /// <param name="group">动作组名称</param>
    /// <param name="priority">优先级</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数。为 NULL 时不调用。</param>
    /// <returns>返回已启动动作的标识号。无法启动时返回 "-1"。</returns>
    public object? StartRandomMotion(string group, MotionPriority priority, FinishedMotionCallback? onFinishedMotionHandler = null)
    {
        var motionGroups = _modelSetting.FileReferences?.Motions;
        if (motionGroups != null && motionGroups.TryGetValue(group, out var groupList) && groupList.Count > 0)
            return StartMotion(group, _random.Next(groupList.Count), priority, onFinishedMotionHandler);

        return null;
    }

    /// <summary>
    /// 设置指定的表情动作
    /// </summary>
    /// <param name="expressionID">表情动作的 ID</param>
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
    /// 随机设置表情动作
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
    /// 接收事件触发
    /// </summary>
    /// <param name="eventValue"></param>
    protected override void MotionEventFired(string eventValue)
    {
        CubismLog.Debug($"[Live2D App]{eventValue} is fired on LAppModel!!");
        Motion?.Invoke(this, eventValue);
    }

    /// <summary>
    /// 碰撞检测测试。
    /// 根据指定 ID 的顶点列表计算矩形，判断坐标是否在矩形范围内。
    /// </summary>
    /// <param name="hitAreaName">待测试碰撞的目标 ID</param>
    /// <param name="x">判定用 X 坐标</param>
    /// <param name="y">判定用 Y 坐标</param>
    /// <returns></returns>
    public bool HitTest(string hitAreaName, float x, float y)
    {
        // 透明时无碰撞检测。
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
        return false; // 不存在时返回 false
    }

    /// <summary>
    /// 模型绘制处理（内部调用）。
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
    /// 将纹理加载到纹理单元
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
    /// 批量加载指定组名的动作数据。
    /// 动作数据的名称内部从 ModelSetting 获取。
    /// </summary>
    /// <param name="group">动作数据的组名称</param>
    private void PreloadMotionGroup(string group)
    {
        // 获取组中注册的动作数量
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
