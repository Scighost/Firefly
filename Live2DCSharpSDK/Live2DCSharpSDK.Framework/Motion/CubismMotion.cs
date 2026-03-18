using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using System.Text.Json;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 动作类。
/// </summary>
public class CubismMotion : ACubismMotion
{
    public const string EffectNameEyeBlink = "EyeBlink";
    public const string EffectNameLipSync = "LipSync";
    public const string TargetNameModel = "Model";
    public const string TargetNameParameter = "Parameter";
    public const string TargetNamePartOpacity = "PartOpacity";

    // Id
    public const string IdNameOpacity = "Opacity";

    /// <summary>
    /// 已加载文件的 FPS，没有符号就使用默认値 15fps
    /// </summary>
    private readonly float _sourceFrameRate;
    /// <summary>
    /// mtn 文件中定义的动作总长度
    /// </summary>
    private readonly float _loopDurationSeconds;
    /// <summary>
    /// 是否循环?
    /// </summary>
    public bool IsLoop { get; set; }
    /// <summary>
    /// 循环时淡入是否有效的标志，默认居有效。
    /// </summary>
    public bool IsLoopFadeIn { get; set; }
    /// <summary>
    /// 最后设置的权重
    /// </summary>
    private float _lastWeight;

    /// <summary>
    /// 实际持有的动作数据本体
    /// </summary>
    private readonly CubismMotionData _motionData;

    /// <summary>
    /// 应用自动眼驱的参数 ID 列表。将模型（模型设置）与参数对应。
    /// </summary>
    private List<string> _eyeBlinkParameterIds;
    /// <summary>
    /// 应用口型同步的参数 ID 列表。将模型（模型设置）与参数对应。
    /// </summary>
    private List<string> _lipSyncParameterIds;

    /// <summary>
    /// 模型中自动眼驱参数 ID 的句柄。将模型与动作对应。
    /// </summary>
    private string _modelCurveIdEyeBlink;
    /// <summary>
    /// 模型中口型同步参数 ID 的句柄。将模型与动作对应。
    /// </summary>
    private string _modelCurveIdLipSync;
    /// <summary>
    /// 模型中不透明度参数 ID 的句柄。将模型与动作对应。
    /// </summary>
    private string _modelCurveIdOpacity;

    /// <summary>
    /// 从动作中获取的不透明度
    /// </summary>
    private float _modelOpacity;

    /**
    * 若要复现 Cubism SDK R2 之前的动作则设为 true，若要正确复现动画制作者的动作则设为 false。
    */
    private readonly bool UseOldBeziersCurveMotion = false;

    private static CubismMotionPoint LerpPoints(CubismMotionPoint a, CubismMotionPoint b, float t)
    {
        return new()
        {
            Time = a.Time + ((b.Time - a.Time) * t),
            Value = a.Value + ((b.Value - a.Value) * t)
        };
    }

    private static float LinearEvaluate(CubismMotionPoint[] points, int start, float time)
    {
        float t = (time - points[start].Time) / (points[start + 1].Time - points[start].Time);

        if (t < 0.0f)
        {
            t = 0.0f;
        }

        return points[start].Value + ((points[start + 1].Value - points[start].Value) * t);
    }

    private static float BezierEvaluate(CubismMotionPoint[] points, int start, float time)
    {
        float t = (time - points[start].Time) / (points[start + 3].Time - points[start].Time);

        if (t < 0.0f)
        {
            t = 0.0f;
        }

        var p01 = LerpPoints(points[start], points[start + 1], t);
        var p12 = LerpPoints(points[start + 1], points[start + 2], t);
        var p23 = LerpPoints(points[start + 2], points[start + 3], t);

        var p012 = LerpPoints(p01, p12, t);
        var p123 = LerpPoints(p12, p23, t);

        return LerpPoints(p012, p123, t).Value;
    }

    //private static float BezierEvaluateBinarySearch(List<CubismMotionPoint> points, int start, float time)
    //{
    //    float x_error = 0.01f;

    //    float x = time;
    //    float x1 = points[0].Time;
    //    float x2 = points[3].Time;
    //    float cx1 = points[1].Time;
    //    float cx2 = points[2].Time;

    //    float ta = 0.0f;
    //    float tb = 1.0f;
    //    float t = 0.0f;
    //    int i = 0;
    //    for (; i < 20; ++i)
    //    {
    //        if (x < x1 + x_error)
    //        {
    //            t = ta;
    //            break;
    //        }

    //        if (x2 - x_error < x)
    //        {
    //            t = tb;
    //            break;
    //        }

    //        float centerx = (cx1 + cx2) * 0.5f;
    //        cx1 = (x1 + cx1) * 0.5f;
    //        cx2 = (x2 + cx2) * 0.5f;
    //        float ctrlx12 = (cx1 + centerx) * 0.5f;
    //        float ctrlx21 = (cx2 + centerx) * 0.5f;
    //        centerx = (ctrlx12 + ctrlx21) * 0.5f;
    //        if (x < centerx)
    //        {
    //            tb = (ta + tb) * 0.5f;
    //            if (centerx - x_error < x)
    //            {
    //                t = tb;
    //                break;
    //            }

    //            x2 = centerx;
    //            cx2 = ctrlx12;
    //        }
    //        else
    //        {
    //            ta = (ta + tb) * 0.5f;
    //            if (x < centerx + x_error)
    //            {
    //                t = ta;
    //                break;
    //            }

    //            x1 = centerx;
    //            cx1 = ctrlx21;
    //        }
    //    }

    //    if (i == 20)
    //    {
    //        t = (ta + tb) * 0.5f;
    //    }

    //    if (t < 0.0f)
    //    {
    //        t = 0.0f;
    //    }
    //    if (t > 1.0f)
    //    {
    //        t = 1.0f;
    //    }

    //    CubismMotionPoint p01 = LerpPoints(points[start], points[start + 1], t);
    //    CubismMotionPoint p12 = LerpPoints(points[start + 1], points[start + 2], t);
    //    CubismMotionPoint p23 = LerpPoints(points[start + 2], points[start + 3], t);

    //    CubismMotionPoint p012 = LerpPoints(p01, p12, t);
    //    CubismMotionPoint p123 = LerpPoints(p12, p23, t);

    //    return LerpPoints(p012, p123, t).Value;
    //}

    private static float BezierEvaluateCardanoInterpretation(CubismMotionPoint[] points, int start, float time)
    {
        float x = time;
        float x1 = points[start].Time;
        float x2 = points[start + 3].Time;
        float cx1 = points[start + 1].Time;
        float cx2 = points[start + 2].Time;

        float a = x2 - 3.0f * cx2 + 3.0f * cx1 - x1;
        float b = 3.0f * cx2 - 6.0f * cx1 + 3.0f * x1;
        float c = 3.0f * cx1 - 3.0f * x1;
        float d = x1 - x;

        float t = CubismMath.CardanoAlgorithmForBezier(a, b, c, d);

        var p01 = LerpPoints(points[start], points[start + 1], t);
        var p12 = LerpPoints(points[start + 1], points[start + 2], t);
        var p23 = LerpPoints(points[start + 2], points[start + 3], t);

        var p012 = LerpPoints(p01, p12, t);
        var p123 = LerpPoints(p12, p23, t);

        return LerpPoints(p012, p123, t).Value;
    }

    private static float SteppedEvaluate(CubismMotionPoint[] points, int start, float time)
    {
        return points[start].Value;
    }

    private static float InverseSteppedEvaluate(CubismMotionPoint[] points, int start, float time)
    {
        return points[start + 1].Value;
    }

    private static float EvaluateCurve(CubismMotionData motionData, int index, float time)
    {
        // Find segment to evaluate.
        var curve = motionData.Curves[index];

        int target = -1;
        int totalSegmentCount = curve.BaseSegmentIndex + curve.SegmentCount;
        int pointPosition = 0;
        for (int i = curve.BaseSegmentIndex; i < totalSegmentCount; ++i)
        {
            // Get first point of next segment.
            pointPosition = motionData.Segments[i].BasePointIndex
                + (motionData.Segments[i].SegmentType ==
                    CubismMotionSegmentType.Bezier ? 3 : 1);

            // Break if time lies within current segment.
            if (motionData.Points[pointPosition].Time > time)
            {
                target = i;
                break;
            }
        }

        if (target == -1)
        {
            return motionData.Points[pointPosition].Value;
        }

        var segment = motionData.Segments[target];

        return segment.Evaluate(motionData.Points, segment.BasePointIndex, time);
    }

    /// <summary>
    /// 创建实例。
    /// </summary>
    /// <param name="buffer">已加载 motion3.json 的缓冲区</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数，为 NULL 时不调用。</param>
    public CubismMotion(string buffer, FinishedMotionCallback? onFinishedMotionHandler = null)
    {
        _sourceFrameRate = 30.0f;
        _loopDurationSeconds = -1.0f;
        IsLoopFadeIn = true;       // 循环时淡入是否有效的标志
        _modelOpacity = 1.0f;

        using var stream = File.Open(buffer, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var obj = JsonSerializer.Deserialize(stream, CubismMotionObjContext.Default.CubismMotionObj)
            ?? throw new Exception("Load Motion error");

        _motionData = new()
        {
            Duration = obj.Meta.Duration,
            Loop = obj.Meta.Loop,
            CurveCount = obj.Meta.CurveCount,
            Fps = obj.Meta.Fps,
            EventCount = obj.Meta.UserDataCount,
            Curves = new CubismMotionCurve[obj.Meta.CurveCount],
            Segments = new CubismMotionSegment[obj.Meta.TotalSegmentCount],
            // TotalPointCount in the JSON metadata may be unreliable across editor versions
            // (e.g. it may not count Bezier control points). Use a guaranteed safe upper bound:
            // 1 start point per curve + up to 3 points per segment (worst case: all Bezier).
            Points = new CubismMotionPoint[obj.Meta.CurveCount + 3 * obj.Meta.TotalSegmentCount],
            Events = new CubismMotionEvent[obj.Meta.UserDataCount]
        };

        bool areBeziersRestructed = obj.Meta.AreBeziersRestricted;

        if (obj.Meta.FadeInTime != null)
        {
            FadeInSeconds = obj.Meta.FadeInTime < 0.0f ? 1.0f : (float)obj.Meta.FadeInTime;
        }
        else
        {
            FadeInSeconds = 1.0f;
        }

        if (obj.Meta.FadeOutTime != null)
        {
            FadeOutSeconds = obj.Meta.FadeOutTime < 0.0f ? 1.0f : (float)obj.Meta.FadeOutTime;
        }
        else
        {
            FadeOutSeconds = 1.0f;
        }

        int totalPointCount = 0;
        int totalSegmentCount = 0;

        // Curves
        for (int curveCount = 0; curveCount < _motionData.CurveCount; ++curveCount)
        {
            var item = obj.Curves[curveCount];
            string key = item.Target;
            _motionData.Curves[curveCount] = new()
            {
                Id = CubismFramework.CubismIdManager.GetId(item.Id),
                BaseSegmentIndex = totalSegmentCount,
                FadeInTime = item.FadeInTime != null ? (float)item.FadeInTime : -1.0f,
                FadeOutTime = item.FadeOutTime != null ? (float)item.FadeOutTime : -1.0f,
                Type = key switch
                {
                    TargetNameModel => CubismMotionCurveTarget.Model,
                    TargetNameParameter => CubismMotionCurveTarget.Parameter,
                    TargetNamePartOpacity => CubismMotionCurveTarget.PartOpacity,
                    _ => throw new Exception("Error: Unable to get segment type from Curve! The number of \"CurveCount\" may be incorrect!")
                }
            };

            // Segments
            for (int segmentPosition = 0; segmentPosition < item.Segments.Count;)
            {
                if (segmentPosition == 0)
                {
                    _motionData.Segments[totalSegmentCount] = new()
                    {
                        BasePointIndex = totalPointCount
                    };
                    _motionData.Points[totalPointCount] = new()
                    {
                        Time = item.Segments[segmentPosition],
                        Value = item.Segments[segmentPosition + 1]
                    };

                    totalPointCount += 1;
                    segmentPosition += 2;
                }
                else
                {
                    _motionData.Segments[totalSegmentCount] = new()
                    {
                        BasePointIndex = totalPointCount - 1
                    };
                }

                switch ((CubismMotionSegmentType)item.Segments[segmentPosition])
                {
                    case CubismMotionSegmentType.Linear:
                        {
                            _motionData.Segments[totalSegmentCount].SegmentType = CubismMotionSegmentType.Linear;
                            _motionData.Segments[totalSegmentCount].Evaluate = LinearEvaluate;

                            _motionData.Points[totalPointCount] = new()
                            {
                                Time = item.Segments[segmentPosition + 1],
                                Value = item.Segments[segmentPosition + 2]
                            };

                            totalPointCount += 1;
                            segmentPosition += 3;

                            break;
                        }
                    case CubismMotionSegmentType.Bezier:
                        {
                            _motionData.Segments[totalSegmentCount].SegmentType = CubismMotionSegmentType.Bezier;
                            if (areBeziersRestructed || UseOldBeziersCurveMotion)
                            {
                                _motionData.Segments[totalSegmentCount].Evaluate = BezierEvaluate;
                            }
                            else
                            {
                                _motionData.Segments[totalSegmentCount].Evaluate = BezierEvaluateCardanoInterpretation;
                            }

                            _motionData.Points[totalPointCount] = new()
                            {
                                Time = item.Segments[segmentPosition + 1],
                                Value = item.Segments[segmentPosition + 2]
                            };

                            _motionData.Points[totalPointCount + 1] = new()
                            {
                                Time = item.Segments[segmentPosition + 3],
                                Value = item.Segments[segmentPosition + 4]
                            };

                            _motionData.Points[totalPointCount + 2] = new()
                            {
                                Time = item.Segments[segmentPosition + 5],
                                Value = item.Segments[segmentPosition + 6]
                            };

                            totalPointCount += 3;
                            segmentPosition += 7;

                            break;
                        }
                    case CubismMotionSegmentType.Stepped:
                        {
                            _motionData.Segments[totalSegmentCount].SegmentType = CubismMotionSegmentType.Stepped;
                            _motionData.Segments[totalSegmentCount].Evaluate = SteppedEvaluate;

                            _motionData.Points[totalPointCount] = new()
                            {
                                Time = item.Segments[segmentPosition + 1],
                                Value = item.Segments[segmentPosition + 2]
                            };

                            totalPointCount += 1;
                            segmentPosition += 3;

                            break;
                        }
                    case CubismMotionSegmentType.InverseStepped:
                        {
                            _motionData.Segments[totalSegmentCount].SegmentType = CubismMotionSegmentType.InverseStepped;
                            _motionData.Segments[totalSegmentCount].Evaluate = InverseSteppedEvaluate;

                            _motionData.Points[totalPointCount] = new()
                            {
                                Time = item.Segments[segmentPosition + 1],
                                Value = item.Segments[segmentPosition + 2]
                            };

                            totalPointCount += 1;
                            segmentPosition += 3;

                            break;
                        }
                    default:
                        {
                            throw new Exception("CubismMotionSegmentType error");
                        }
                }

                ++_motionData.Curves[curveCount].SegmentCount;
                ++totalSegmentCount;
            }
        }

        for (int userdatacount = 0; userdatacount < obj.Meta.UserDataCount; ++userdatacount)
        {
            _motionData.Events[userdatacount] = new()
            {
                FireTime = obj.UserData[userdatacount].Time,
                Value = obj.UserData[userdatacount].Value
            };
        }

        _sourceFrameRate = _motionData.Fps;
        _loopDurationSeconds = _motionData.Duration;
        OnFinishedMotion = onFinishedMotionHandler;
    }

    /// <summary>
    /// 执行模型参数更新。
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="userTimeSeconds">当前时刻[秒]</param>
    /// <param name="fadeWeight">动作权重</param>
    /// <param name="motionQueueEntry">CubismMotionQueueManager 中管理的动作</param>
    public override void DoUpdateParameters(CubismModel model, float userTimeSeconds, float fadeWeight, CubismMotionQueueEntry motionQueueEntry)
    {
        _modelCurveIdEyeBlink ??= CubismFramework.CubismIdManager.GetId(EffectNameEyeBlink);

        _modelCurveIdLipSync ??= CubismFramework.CubismIdManager.GetId(EffectNameLipSync);

        _modelCurveIdOpacity ??= CubismFramework.CubismIdManager.GetId(IdNameOpacity);

        float timeOffsetSeconds = userTimeSeconds - motionQueueEntry.StartTime;

        if (timeOffsetSeconds < 0.0f)
        {
            timeOffsetSeconds = 0.0f; // 防止错误
        }

        float lipSyncValue = float.MaxValue;
        float eyeBlinkValue = float.MaxValue;

        //眼驱、口型同步中检测动作是否应用的标志位（最多 MaxFlagCount 个）
        int MaxTargetSize = 64;
        ulong lipSyncFlags = 0;
        ulong eyeBlinkFlags = 0;

        //眼驱、口型同步目标数超过上限时
        if (_eyeBlinkParameterIds.Count > MaxTargetSize)
        {
            CubismLog.Warning($"[Live2D SDK]too many eye blink targets : {_eyeBlinkParameterIds.Count}");
        }
        if (_lipSyncParameterIds.Count > MaxTargetSize)
        {
            CubismLog.Warning($"[Live2D SDK]too many lip sync targets : {_lipSyncParameterIds.Count}");
        }

        float tmpFadeIn = (FadeInSeconds <= 0.0f) ? 1.0f :
           CubismMath.GetEasingSine((userTimeSeconds - motionQueueEntry.FadeInStartTime) / FadeInSeconds);

        float tmpFadeOut = (FadeOutSeconds <= 0.0f || motionQueueEntry.EndTime < 0.0f) ? 1.0f :
           CubismMath.GetEasingSine((motionQueueEntry.EndTime - userTimeSeconds) / FadeOutSeconds);

        float value;
        int c, parameterIndex;

        // 'Repeat' time as necessary.
        float time = timeOffsetSeconds;

        if (IsLoop)
        {
            while (time > _motionData.Duration)
            {
                time -= _motionData.Duration;
            }
        }

        var curves = _motionData.Curves;

        // Evaluate model curves.
        for (c = 0; c < _motionData.CurveCount && curves[c].Type == CubismMotionCurveTarget.Model; ++c)
        {
            // Evaluate curve and call handler.
            value = EvaluateCurve(_motionData, c, time);

            if (curves[c].Id == _modelCurveIdEyeBlink)
            {
                eyeBlinkValue = value;
            }
            else if (curves[c].Id == _modelCurveIdLipSync)
            {
                lipSyncValue = value;
            }
            else if (curves[c].Id == _modelCurveIdOpacity)
            {
                _modelOpacity = value;

                // ------ 如果存在不透明度倗就应用 ------
                model.SetModelOpacity(GetModelOpacityValue());
            }
        }

        int parameterMotionCurveCount = 0;

        for (; c < _motionData.CurveCount && curves[c].Type == CubismMotionCurveTarget.Parameter; ++c)
        {
            parameterMotionCurveCount++;

            // Find parameter index.
            parameterIndex = model.GetParameterIndex(curves[c].Id);

            // Skip curve evaluation if no value in sink.
            if (parameterIndex == -1)
            {
                continue;
            }

            float sourceValue = model.GetParameterValue(parameterIndex);

            // Evaluate curve and apply value.
            value = EvaluateCurve(_motionData, c, time);

            if (eyeBlinkValue != float.MaxValue)
            {
                for (int i = 0; i < _eyeBlinkParameterIds.Count && i < MaxTargetSize; ++i)
                {
                    if (_eyeBlinkParameterIds[i] == curves[c].Id)
                    {
                        value *= eyeBlinkValue;
                        eyeBlinkFlags |= 1UL << i;
                        break;
                    }
                }
            }

            if (lipSyncValue != float.MaxValue)
            {
                for (int i = 0; i < _lipSyncParameterIds.Count && i < MaxTargetSize; ++i)
                {
                    if (_lipSyncParameterIds[i] == curves[c].Id)
                    {
                        value += lipSyncValue;
                        lipSyncFlags |= 1UL << i;
                        break;
                    }
                }
            }

            float v;
            // 每个参数的淡入淡出
            if (curves[c].FadeInTime < 0.0f && curves[c].FadeOutTime < 0.0f)
            {
                //应用动作整体的淡入淡出
                v = sourceValue + (value - sourceValue) * fadeWeight;
            }
            else
            {
                // 如果参数单独设置了淡入或淡出，则应用该设置
                float fin;
                float fout;

                if (curves[c].FadeInTime < 0.0f)
                {
                    fin = tmpFadeIn;
                }
                else
                {
                    fin = curves[c].FadeInTime == 0.0f ? 1.0f
                            : CubismMath.GetEasingSine((userTimeSeconds - motionQueueEntry.FadeInStartTime) / curves[c].FadeInTime);
                }

                if (curves[c].FadeOutTime < 0.0f)
                {
                    fout = tmpFadeOut;
                }
                else
                {
                    fout = (curves[c].FadeOutTime == 0.0f || motionQueueEntry.EndTime < 0.0f)
                                ? 1.0f
                            : CubismMath.GetEasingSine((motionQueueEntry.EndTime - userTimeSeconds) / curves[c].FadeOutTime);
                }

                float paramWeight = Weight * fin * fout;

                // 应用每个参数的淡入淡出
                v = sourceValue + (value - sourceValue) * paramWeight;
            }

            model.SetParameterValue(parameterIndex, v);
        }

        if (eyeBlinkValue != float.MaxValue)
        {
            for (int i = 0; i < _eyeBlinkParameterIds.Count && i < MaxTargetSize; ++i)
            {
                float sourceValue = model.GetParameterValue(_eyeBlinkParameterIds[i]);
                //动作覆盖时不应用眼驱
                if (((eyeBlinkFlags >> i) & 0x01) != 0UL)
                {
                    continue;
                }

                float v = sourceValue + (eyeBlinkValue - sourceValue) * fadeWeight;

                model.SetParameterValue(_eyeBlinkParameterIds[i], v);
            }
        }

        if (lipSyncValue != float.MaxValue)
        {
            for (int i = 0; i < _lipSyncParameterIds.Count && i < MaxTargetSize; ++i)
            {
                float sourceValue = model.GetParameterValue(_lipSyncParameterIds[i]);
                //动作覆盖时不应用口型同步
                if (((lipSyncFlags >> i) & 0x01) != 0UL)
                {
                    continue;
                }

                float v = sourceValue + (lipSyncValue - sourceValue) * fadeWeight;

                model.SetParameterValue(_lipSyncParameterIds[i], v);
            }
        }

        for (; c < _motionData.CurveCount && curves[c].Type == CubismMotionCurveTarget.PartOpacity; ++c)
        {
            // Find parameter index.
            parameterIndex = model.GetParameterIndex(curves[c].Id);

            // Skip curve evaluation if no value in sink.
            if (parameterIndex == -1)
            {
                continue;
            }

            // Evaluate curve and apply value.
            value = EvaluateCurve(_motionData, c, time);

            model.SetParameterValue(parameterIndex, value);
        }

        if (timeOffsetSeconds >= _motionData.Duration)
        {
            if (IsLoop)
            {
                motionQueueEntry.StartTime = userTimeSeconds; //回到初始状态
                if (IsLoopFadeIn)
                {
                    //循环中且循环淡入有效时，重新设置淡入
                    motionQueueEntry.FadeInStartTime = userTimeSeconds;
                }
            }
            else
            {
                OnFinishedMotion?.Invoke(model, this);

                motionQueueEntry.Finished = true;
            }
        }

        _lastWeight = fadeWeight;
    }

    /// <summary>
    /// 获取动作长度。
    /// </summary>
    /// <returns>动作长度[秒]</returns>
    public override float GetDuration()
    {
        return IsLoop ? -1.0f : _loopDurationSeconds;
    }

    /// <summary>
    /// 获取动作循环时的长度。
    /// </summary>
    /// <returns>动作循环时的长度[秒]</returns>
    public override float GetLoopDuration()
    {
        return _loopDurationSeconds;
    }

    /// <summary>
    /// 设置指定参数的淡入时间。
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <param name="value">淡入所需时间[秒]</param>
    public void SetParameterFadeInTime(string parameterId, float value)
    {
        var curves = _motionData.Curves;

        for (int i = 0; i < _motionData.CurveCount; ++i)
        {
            if (parameterId == curves[i].Id)
            {
                curves[i].FadeInTime = value;
                return;
            }
        }
    }

    /// <summary>
    /// 设置指定参数的淡出时间。
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <param name="value">淡出所需时间[秒]</param>
    public void SetParameterFadeOutTime(string parameterId, float value)
    {
        var curves = _motionData.Curves;

        for (int i = 0; i < _motionData.CurveCount; ++i)
        {
            if (parameterId == curves[i].Id)
            {
                curves[i].FadeOutTime = value;
                return;
            }
        }
    }

    /// <summary>
    /// 获取指定参数的淡入时间。
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <returns>淡入所需时间[秒]</returns>
    public float GetParameterFadeInTime(string parameterId)
    {
        var curves = _motionData.Curves;

        for (int i = 0; i < _motionData.CurveCount; ++i)
        {
            if (parameterId == curves[i].Id)
            {
                return curves[i].FadeInTime;
            }
        }

        return -1;
    }

    /// <summary>
    /// 获取指定参数的淡出时间。
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <returns>淡出所需时间[秒]</returns>
    public float GetParameterFadeOutTime(string parameterId)
    {
        var curves = _motionData.Curves;

        for (int i = 0; i < _motionData.CurveCount; ++i)
        {
            if (parameterId == curves[i].Id)
            {
                return curves[i].FadeOutTime;
            }
        }

        return -1;
    }

    /// <summary>
    /// 设置自动效果应用的参数 ID 列表。
    /// </summary>
    /// <param name="eyeBlinkParameterIds">应用自动眼驱的参数 ID 列表</param>
    /// <param name="lipSyncParameterIds">应用口型同步的参数 ID 列表</param>
    public void SetEffectIds(List<string> eyeBlinkParameterIds, List<string> lipSyncParameterIds)
    {
        _eyeBlinkParameterIds = eyeBlinkParameterIds;
        _lipSyncParameterIds = lipSyncParameterIds;
    }

    /// <summary>
    /// 检测事件是否触发。
    /// 输入时间以该动作调用时刻为 0 的秒数进行计算。
    /// </summary>
    /// <param name="beforeCheckTimeSeconds">上一次事件检测时间[秒]</param>
    /// <param name="motionTimeSeconds">本次播放时间[秒]</param>
    /// <returns></returns>
    public override List<string> GetFiredEvent(float beforeCheckTimeSeconds, float motionTimeSeconds)
    {
        FiredEventValues.Clear();
        /// 事件触发检查
        for (int u = 0; u < _motionData.EventCount; ++u)
        {
            if ((_motionData.Events[u].FireTime > beforeCheckTimeSeconds) &&
                (_motionData.Events[u].FireTime <= motionTimeSeconds))
            {
                FiredEventValues.Add(_motionData.Events[u].Value);
            }
        }

        return FiredEventValues;

    }

    /// <summary>
    /// 检查是否存在不透明度曲线
    /// </summary>
    /// <returns>true  . 存在键
    /// false . 不存在键</returns>
    public override bool IsExistModelOpacity()
    {
        for (int i = 0; i < _motionData.CurveCount; i++)
        {
            CubismMotionCurve curve = _motionData.Curves[i];

            if (curve.Type != CubismMotionCurveTarget.Model)
            {
                continue;
            }

            if (curve.Id == IdNameOpacity)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 返回不透明度曲线的索引
    /// </summary>
    /// <returns>不透明度曲线的索引</returns>
    public override int GetModelOpacityIndex()
    {
        if (IsExistModelOpacity())
        {
            for (int i = 0; i < _motionData.CurveCount; i++)
            {
                CubismMotionCurve curve = _motionData.Curves[i];

                if (curve.Type != CubismMotionCurveTarget.Model)
                {
                    continue;
                }

                if (curve.Id == IdNameOpacity)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// 返回不透明度的 Id
    /// </summary>
    /// <returns>不透明度的 Id</returns>
    public override string? GetModelOpacityId(int index)
    {
        if (index != -1)
        {
            CubismMotionCurve curve = _motionData.Curves[index];

            if (curve.Type == CubismMotionCurveTarget.Model)
            {
                if (curve.Id == IdNameOpacity)
                {
                    return CubismFramework.CubismIdManager.GetId(curve.Id);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 返回不透明度的假，调用 UpdateParameters() 后获取更新后的假。
    /// </summary>
    /// <returns>动作当前时刻的不透明度假</returns>
    public override float GetModelOpacityValue()
    {
        return _modelOpacity;
    }
}
