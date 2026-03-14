using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Motion;
using System;
using System.Collections.Generic;
using System.IO;

namespace Live2DCSharpSDK.WinUI.LApp;

/// <summary>
/// サンプルアプリケーションにおいてCubismModelを管理するクラス
/// モデル生成と破棄、タップイベントの処理、モデル切り替えを行う。
/// </summary>
/// <remarks>
/// コンストラクタ
/// </remarks>
public class LAppLive2DManager(LAppDelegate lapp) : IDisposable
{
    public event Action<CubismModel, ACubismMotion>? MotionFinished;

    /// <summary>
    /// モデル描画に用いるView行列
    /// </summary>
    public CubismMatrix44 ViewMatrix { get; } = new();

    /// <summary>
    /// モデルインスタンスのコンテナ
    /// </summary>
    private readonly List<LAppModel> _models = [];

    /// <summary>
    /// 現在のシーンで保持しているモデルを返す
    /// </summary>
    /// <param name="no">モデルリストのインデックス値</param>
    /// <returns>モデルのインスタンスを返す。インデックス値が範囲外の場合はNULLを返す。</returns>
    public LAppModel GetModel(int no)
    {
        return _models[no];
    }

    /// <summary>
    /// 現在のシーンで保持しているすべてのモデルを解放する
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
    /// 画面をドラッグしたときの処理
    /// </summary>
    /// <param name="x">画面のX座標</param>
    /// <param name="y">画面のY座標</param>
    public void OnDrag(float x, float y)
    {
        for (int i = 0; i < _models.Count; i++)
        {
            LAppModel model = GetModel(i);

            model.SetDragging(x, y);
        }
    }

    /// <summary>
    /// 画面をタップしたときの処理
    /// </summary>
    /// <param name="x">画面のX座標</param>
    /// <param name="y">画面のY座標</param>
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
    /// 画面を更新するときの処理
    /// モデルの更新処理および描画処理を行う
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
                // キャンバスがウィンドウより横長: 横幅を基準にスケールしてはみ出しを防ぐ
                model.ModelMatrix.SetWidth(2.0f);
                _projection.Scale(1.0f, (float)width / height);
            }
            else
            {
                // キャンバスがウィンドウより縦長または同じ: 縦幅を基準にスケール
                model.ModelMatrix.SetHeight(2.0f);
                _projection.Scale((float)height / width, 1.0f);
            }

            // 必要があればここで乗算
            if (ViewMatrix != null)
            {
                _projection.MultiplyByMatrix(ViewMatrix);
            }

            model.Update();
            model.Draw(_projection); // 参照渡しなのでprojectionは変質する
        }
    }

    public LAppModel LoadModel(string dir, string name)
    {
        CubismLog.Debug($"[Live2D App]model load: {name}");

        // ModelDir[]に保持したディレクトリ名から
        // model3.jsonのパスを決定する.
        // ディレクトリ名とmodel3.jsonの名前を一致させておくこと.
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
    /// モデル個数を得る
    /// </summary>
    /// <returns>所持モデル個数</returns>
    public int GetModelNum()
    {
        return _models.Count;
    }

    public void Dispose()
    {
        ReleaseAllModel();
    }
}
