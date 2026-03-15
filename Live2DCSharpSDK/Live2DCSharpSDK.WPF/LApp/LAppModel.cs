using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Motion;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Live2DCSharpSDK.WPF.LApp;

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
    /// 各组当前播放动作的优先级
    /// </summary>
    private readonly Dictionary<string, MotionPriority> _groupCurrentPriority = [];
    /// <summary>
    /// 各组当前播放动作是否可被打断
    /// </summary>
    private readonly Dictionary<string, bool> _groupCurrentInterruptable = [];
    /// <summary>
    /// 运行时状态变量表（VarFloats），用于控制动作播放条件与赋值
    /// </summary>
    private readonly Dictionary<string, float> _varFloats = [];

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
                _groupCurrentPriority[group] = MotionPriority.PriorityNone;
                _groupCurrentInterruptable[group] = false;
            }
        }

        // 执行 Start 组 VarFloat 赋值，初始化运行时状态变量
        if (_modelSetting.FileReferences?.Motions?.TryGetValue("Start", out var startMotions) == true)
        {
            foreach (var startItem in startMotions)
                ExecuteVarFloatAssignments(startItem);
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
                _groupCurrentPriority[group] = MotionPriority.PriorityNone;
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
    /// <param name="name">动作名，格式为 "Group_Index"</param>
    /// <param name="priority">优先级；PriorityNone 表示使用 JSON 定义的优先级</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数。为 NULL 时不调用。</param>
    /// <returns>返回已启动动作的标识号。无法启动时返回 "-1"。</returns>
    public CubismMotionQueueEntry? StartMotion(string name, MotionPriority priority = MotionPriority.PriorityNone, FinishedMotionCallback? onFinishedMotionHandler = null)
    {
        var temp = name.Split("_");
        if (temp.Length != 2)
        {
            throw new Exception("motion name error");
        }
        return StartMotion(temp[0], int.Parse(temp[1]), priority, onFinishedMotionHandler);
    }

    /// <summary>
    /// 以 "GroupName:MotionName" 格式的引用开始播放动作。
    /// NextMtn 链式处理及 VarFloat 检查均由 StartMotion(group, no, ...) 负责。
    /// </summary>
    /// <param name="priority">优先级；PriorityNone 表示使用 JSON 定义的优先级</param>
    public CubismMotionQueueEntry? StartMotionByRef(string motionRef, MotionPriority priority = MotionPriority.PriorityNone)
    {
        var parts = motionRef.Split(':', 2);
        if (parts.Length != 2) return null;

        string group = parts[0];
        string motionName = parts[1];

        var motions = _modelSetting.FileReferences?.Motions;
        if (motions == null || !motions.TryGetValue(group, out var list)) return null;

        int index = list.FindIndex(m => m.Name == motionName);
        if (index < 0) return null;

        return StartMotion(group, index, priority);
    }

    public CubismMotionQueueEntry? StartMotion(string group, int no, MotionPriority priority = MotionPriority.PriorityNone, FinishedMotionCallback? onFinishedMotionHandler = null)
    {
        var motionsDef = _modelSetting.FileReferences?.Motions;
        if (motionsDef == null || !motionsDef.TryGetValue(group, out var groupDef)) return null;
        if (no < 0 || no >= groupDef.Count) return null;

        var item = groupDef[no];
        string name = $"{group}_{no}";

        // 计算有效优先级：PriorityNone 表示使用 JSON 定义的优先级，直接将 int 强转，允许超出枚举范围
        var effectivePriority = priority != MotionPriority.PriorityNone ? priority : (MotionPriority)item.Priority;

        // 1. 检查 VarFloats 条件（Type=1）：条件不满足时拒绝启动
        if (!CheckVarFloatConditions(item))
        {
            CubismLog.Debug($"[Live2D App]can't start motion [{name}]: VarFloat condition not met.");
            return null;
        }

        // 2. 组合 NextMtn 回调（仅在动作自然结束时触发，被打断则不触发）
        FinishedMotionCallback? chainCallback = onFinishedMotionHandler;
        if (!string.IsNullOrEmpty(item.NextMtn))
        {
            string nextRef = item.NextMtn;
            var prev = chainCallback;
            chainCallback = (m, a) => { StartMotionByRef(nextRef); prev?.Invoke(m, a); };
        }

        // 3. 处理无 File 动作（纯表情型 / 控制型）
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
                // 非可打断动作（如回正）重置组的可打断标志
                _groupCurrentInterruptable[group] = false;
            }

            // 执行 VarFloat 赋值
            ExecuteVarFloatAssignments(item);

            // 应用表情
            if (!string.IsNullOrEmpty(item.Expression) && _expressions.TryGetValue(item.Expression, out var exprMotion))
            {
                if (_groupExpressionManagers.TryGetValue(group, out var grpMgr))
                {
                    if (item.Interruptable)
                    {
                        grpMgr.StopAllMotions();
                        _groupCurrentInterruptable[group] = true;
                    }
                    grpMgr.StartMotionPriority(exprMotion, item.Priority == 0 ? MotionPriority.PriorityNormal : (MotionPriority)item.Priority);
                }
                else
                    SetExpression(item.Expression);
            }

            // 立即触发 NextMtn 链（无动画文件时不等待结束）
            chainCallback?.Invoke(null!, null!);
            return null;
        }

        // 4. 有 File 的完整动画动作
        if (!_groupMotionManagers.TryGetValue(group, out var groupManager))
        {
            groupManager = new CubismMotionManager();
            _groupMotionManagers[group] = groupManager;
        }

        // 优先级检查：当前有动作播放时，判断是否允许抢占
        if (!groupManager.IsFinished())
        {
            var curPriority = _groupCurrentPriority.GetValueOrDefault(group, MotionPriority.PriorityNone);
            bool curInterruptable = _groupCurrentInterruptable.GetValueOrDefault(group);
            if (!curInterruptable && effectivePriority <= curPriority)
            {
                CubismLog.Debug($"[Live2D App]can't start motion [{name}]: priority {effectivePriority} <= current {curPriority}.");
                return null;
            }
        }

        if (effectivePriority == MotionPriority.PriorityForce)
        {
            groupManager.ReservePriority = effectivePriority;
        }
        else if (!groupManager.ReserveMotion(effectivePriority))
        {
            CubismLog.Debug("[Live2D App]can't start motion.");
            return null;
        }

        // 加载或从缓存获取动作数据
        if (!_motions.TryGetValue(name, out var value))
        {
            string path = Path.GetFullPath(_modelHomeDir + item.File);
            if (!File.Exists(path))
                return null;

            var newMotion = new CubismMotion(path);
            float fadeTime = item.FadeInTime;
            if (fadeTime >= 0.0f) newMotion.FadeInSeconds = fadeTime;
            fadeTime = item.FadeOutTime;
            if (fadeTime >= 0.0f) newMotion.FadeOutSeconds = fadeTime;
            newMotion.SetEffectIds(_eyeBlinkIds, _lipSyncIds);
            _motions[name] = value = newMotion;
        }
        var motion = (CubismMotion)value!;

        // 绑定完成回调（NextMtn 链仅在自然结束时触发）
        motion.OnFinishedMotion = chainCallback;

        // 应用表情
        if (!string.IsNullOrEmpty(item.Expression))
            SetExpression(item.Expression);

        // 播放音效
        string voice = item.Sound;
        if (!string.IsNullOrWhiteSpace(voice))
        {
            string soundPath = Path.GetFullPath(_modelHomeDir + voice);
            _wavFileHandler.Start(soundPath, item.SoundDelay);
        }

        // 执行 VarFloat 赋值（动作确认启动后执行）
        ExecuteVarFloatAssignments(item);

        // 停止所有其他组中正在以待机优先级运行的动作，避免与本次动作参数冲突
        if (effectivePriority > MotionPriority.PriorityIdle)
        {
            foreach (var (otherGroup, otherMgr) in _groupMotionManagers)
            {
                if (otherGroup == group) continue;
                if (_groupCurrentPriority.GetValueOrDefault(otherGroup) == MotionPriority.PriorityIdle)
                {
                    otherMgr.StopAllMotions();
                    _groupCurrentPriority[otherGroup] = MotionPriority.PriorityNone;
                }
            }
        }

        // 更新组状态
        _groupCurrentPriority[group] = effectivePriority;
        _groupCurrentInterruptable[group] = item.Interruptable;
        CubismLog.Debug($"[Live2D App]start motion: [{name}] priority={effectivePriority} interruptable={item.Interruptable}");
        return groupManager.StartMotionPriority(motion, effectivePriority);
    }

    /// <summary>
    /// 尝试播放待机动作。应由外部（如定时器或帧调度器）按需调用。
    /// 优先触发标准 Idle 组；若不存在，则遍历所有组内动作均带有 VarFloat 条件（Type=1）的组。
    /// 只有对应组当前无动作播放时才会触发。
    /// </summary>
    /// <returns>true 表示成功触发了至少一个待机动作</returns>
    public bool TryStartIdleMotion()
    {
        bool triggered = false;

        // 标准 Idle 组
        if (_groupMotionManagers.TryGetValue(LAppDefine.MotionGroupIdle, out var stdIdleMgr)
            && stdIdleMgr.IsFinished())
        {
            triggered |= StartRandomMotion(LAppDefine.MotionGroupIdle, MotionPriority.PriorityIdle) != null;
        }

        // 条件型待机组（组内所有 Motion 均有 VarFloat Type=1 门控条件的组）
        var motionDefs = _modelSetting.FileReferences?.Motions;
        if (motionDefs != null)
        {
            foreach (var (group, mgr) in _groupMotionManagers)
            {
                if (group == LAppDefine.MotionGroupIdle) continue;
                if (!mgr.IsFinished()) continue;
                if (!motionDefs.TryGetValue(group, out var groupList) || groupList.Count == 0) continue;
                if (groupList.All(m => m.VarFloats?.Any(v => v.Type == 1) == true))
                {
                    triggered |= StartRandomMotion(group, MotionPriority.PriorityIdle) != null;
                }
            }
        }

        return triggered;
    }

    /// <summary>
    /// 随机开始播放一个动作。
    /// </summary>
    /// <param name="group">动作组名称</param>
    /// <param name="priority">优先级；PriorityNone 表示使用 JSON 定义的优先级</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数。为 NULL 时不调用。</param>
    /// <returns>返回已启动动作的标识号。无法启动时返回 "-1"。</returns>
    public object? StartRandomMotion(string group, MotionPriority priority = MotionPriority.PriorityNone, FinishedMotionCallback? onFinishedMotionHandler = null)
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

    /// 检查动作的 VarFloats 条件（Type=1）是否全部满足。
    /// 所有条件均通过时返回 true，任意一条失败则返回 false。
    /// </summary>
    private bool CheckVarFloatConditions(ModelSettingObj.FileReference.Motion item)
    {
        if (item.VarFloats == null) return true;
        foreach (var vf in item.VarFloats)
        {
            if (vf.Type != 1) continue;
            var parts = vf.Code?.Split(' ', 2);
            if (parts?.Length == 2 && parts[0] == "equal"
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float expected))
            {
                float current = _varFloats.GetValueOrDefault(vf.Name, 0f);
                if (MathF.Abs(current - expected) > 1e-4f)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 执行动作的 VarFloats 赋值操作（Type=2）。
    /// 应在动作确认可以启动后调用。
    /// </summary>
    private void ExecuteVarFloatAssignments(ModelSettingObj.FileReference.Motion item)
    {
        if (item.VarFloats == null) return;
        foreach (var vf in item.VarFloats)
        {
            if (vf.Type != 2) continue;
            var parts = vf.Code?.Split(' ', 2);
            if (parts?.Length == 2 && parts[0] == "assign"
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            {
                _varFloats[vf.Name] = val;
                CubismLog.Debug($"[Live2D App]VarFloat assign: {vf.Name} = {val}");
            }
        }
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
