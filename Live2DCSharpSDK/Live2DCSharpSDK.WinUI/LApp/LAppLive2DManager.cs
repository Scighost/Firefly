using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Motion;
using System;
using System.Collections.Generic;
using System.IO;

namespace Live2DCSharpSDK.WinUI.LApp;

/// <summary>
/// 在示例应用程序中管理 CubismModel 的类。
/// 执行模型的生成与销毁、点击事件处理及模型切换。
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
public class LAppLive2DManager(LAppDelegate lapp) : IDisposable
{
    public event Action<CubismModel, ACubismMotion>? MotionFinished;

    /// <summary>
    /// 用于模型绘制的 View 矩阵
    /// </summary>
    public CubismMatrix44 ViewMatrix { get; } = new();

    /// <summary>
    /// 模型实例的容器
    /// </summary>
    private readonly List<LAppModel> _models = [];

    /// <summary>
    /// 返回当前场景中持有的模型
    /// </summary>
    /// <param name="no">模型列表的索引値</param>
    /// <returns>返回模型实例。索引値越界时返回 NULL。</returns>
    public LAppModel GetModel(int no)
    {
        return _models[no];
    }

    /// <summary>
    /// 释放当前场景中持有的所有模型
    /// </summary>
    public void ReleaseAllModel()
    {
        for (int i = 0; i < _models.Count; i++)
        {
            _models[i].Dispose();
        }

        _models.Clear();
    }

    /// <summary>
    /// 拖拽屏幕时的处理
    /// </summary>
    /// <param name="x">屏幕 X 坐标</param>
    /// <param name="y">屏幕 Y 坐标</param>
    public void OnDrag(float x, float y)
    {
        for (int i = 0; i < _models.Count; i++)
        {
            LAppModel model = GetModel(i);

            model.SetDragging(x, y);
        }
    }

    /// <summary>
    /// 点击屏幕时的处理
    /// </summary>
    /// <param name="x">屏幕 X 坐标</param>
    /// <param name="y">屏幕 Y 坐标</param>
    public void OnTap(float x, float y)
    {
        CubismLog.Debug($"[Live2D App]tap point: x:{x:0.00} y:{y:0.00}");

        int width = lapp.WindowWidth;
        int height = lapp.WindowHeight;

        for (int i = 0; i < _models.Count; i++)
        {
            var model = _models[i];
            var hitAreas = model._modelSetting.HitAreas;
            if (hitAreas != null)
            {
                // OnDraw 中叠加了投影缩放，命中测试需要施加其逆变换
                float canvasW = model.Model.GetCanvasWidth();
                float canvasH = model.Model.GetCanvasHeight();
                float tx, ty;
                if (canvasW * height > canvasH * width)
                {
                    // OnDraw: Scale(1.0, width/height) → 逆: y *= height/width
                    tx = x;
                    ty = y * height / width;
                }
                else
                {
                    // OnDraw: Scale(height/width, 1.0) → 逆: x *= width/height
                    tx = x * width / height;
                    ty = y;
                }

                foreach (var area in hitAreas)
                {
                    if (!string.IsNullOrEmpty(area.Motion) && model.HitTest(area.Name, tx, ty))
                    {
                        CubismLog.Debug($"[Live2D App]hit area: [{area.Name}] motion: [{area.Motion}]");
                        model.StartMotionByRef(area.Motion, MotionPriority.PriorityForce);
                        break;
                    }
                }
            }
        }
    }

    private void OnFinishedMotion(CubismModel model, ACubismMotion self)
    {
        CubismLog.Info($"[Live2D App]Motion Finished: {self}");
        MotionFinished?.Invoke(model, self);
    }

    private readonly CubismMatrix44 _projection = new();

    /// <summary>
    /// 更新画面时的处理，执行模型更新和绘制处理
    /// </summary>
    public void OnUpdate()
    {
        lapp.OnUpdatePre();

        int width = lapp.WindowWidth;
        int height = lapp.WindowHeight;
        foreach (var model in _models)
        {
            _projection.LoadIdentity();

            float canvasW = model.Model.GetCanvasWidth();
            float canvasH = model.Model.GetCanvasHeight();

            if (canvasW * height > canvasH * width)
            {
                // Canvas 比窗口宽：以宽度为基准缩放防止超出
                model.ModelMatrix.SetWidth(2.0f);
                _projection.Scale(1.0f, (float)width / height);
            }
            else
            {
                // Canvas 比窗口高或相同：以高度为基准缩放
                model.ModelMatrix.SetHeight(2.0f);
                _projection.Scale((float)height / width, 1.0f);
            }

            // 必要时在此进行矩阵乘法
            if (ViewMatrix != null)
            {
                _projection.MultiplyByMatrix(ViewMatrix);
            }

            model.Update();
            model.Draw(_projection); // 传引用，projection 会被修改
        }
    }

    public LAppModel LoadModel(string dir, string name)
    {
        CubismLog.Debug($"[Live2D App]model load: {name}");

        // 根据 ModelDir[] 中保存的目录名
        // 决定 model3.json 的路径。
        // 请确保目录名与 model3.json 的名称一致。
        if (!dir.EndsWith('\\') && !dir.EndsWith('/'))
        {
            dir = Path.GetFullPath(dir + '/');
        }
        var modelJsonName = Path.GetFullPath($"{dir}{name}");
        if (!File.Exists(modelJsonName))
        {
            modelJsonName = Path.GetFullPath($"{dir}{name}.model3.json");
        }
        if (!File.Exists(modelJsonName))
        {
            dir = Path.GetFullPath(dir + name + '/');
            modelJsonName = Path.GetFullPath($"{dir}{name}.model3.json");
        }
        if (!File.Exists(modelJsonName))
        {
            throw new Exception($"[Live2D]File not found: {modelJsonName}");
        }

        var model = new LAppModel(lapp, dir, modelJsonName);
        _models.Add(model);

        return model;
    }

    public void RemoveModel(int index)
    {
        if (_models.Count > index)
        {
            var model = _models[index];
            _models.RemoveAt(index);
            model.Dispose();
        }
    }

    public void RemoveModel(LAppModel model)
    {
        _models.Remove(model);
        model.Dispose();
    }

    /// <summary>
    /// 获取模型数量
    /// </summary>
    /// <returns>持有的模型数量</returns>
    public int GetModelNum()
    {
        return _models.Count;
    }

    public void Dispose()
    {
        ReleaseAllModel();
    }
}
