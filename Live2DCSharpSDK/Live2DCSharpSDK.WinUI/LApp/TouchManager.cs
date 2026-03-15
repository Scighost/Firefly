using System;

namespace Live2DCSharpSDK.WinUI.LApp;

public class TouchManager
{
    /// <summary>
    /// 触摸开始时的 x 値
    /// </summary>
    private float _startY;
    /// <summary>
    /// 触摸开始时的 y 値
    /// </summary>
    private float _startX;
    /// <summary>
    /// 单指触摸时的 x 値
    /// </summary>
    private float _lastX;
    /// <summary>
    /// 单指触摸时的 y 値
    /// </summary>
    private float _lastY;
    /// <summary>
    /// 双指触摸时第一根手指的 x 値
    /// </summary>
    private float _lastX1;
    /// <summary>
    /// 双指触摸时第一根手指的 y 値
    /// </summary>
    private float _lastY1;
    /// <summary>
    /// 双指触摸时第二根手指的 x 値
    /// </summary>
    private float _lastX2;
    /// <summary>
    /// 双指触摸时第二根手指的 y 値
    /// </summary>
    private float _lastY2;
    /// <summary>
    /// 两根以上手指触摸时的手指间距
    /// </summary>
    private float _lastTouchDistance;
    /// <summary>
    /// 从上次値到本次値的 x 移动距离。
    /// </summary>
    private float _deltaX;
    /// <summary>
    /// 从上次値到本次値的 y 移动距离。
    /// </summary>
    private float _deltaY;
    /// <summary>
    /// 本帧的缩放倍率。非缩放操作时为 1。
    /// </summary>
    private float _scale;
    /// <summary>
    /// 单指触摸时为 true
    /// </summary>
    private bool _touchSingle;
    /// <summary>
    /// 是否启用轻扫
    /// </summary>
    private bool _flipAvailable;

    public TouchManager()
    {
        _scale = 1.0f;
    }

    public float GetCenterX() { return _lastX; }
    public float GetCenterY() { return _lastY; }
    public float GetDeltaX() { return _deltaX; }
    public float GetDeltaY() { return _deltaY; }
    public float GetStartX() { return _startX; }
    public float GetStartY() { return _startY; }
    public float GetScale() { return _scale; }
    public float GetX() { return _lastX; }
    public float GetY() { return _lastY; }
    public float GetX1() { return _lastX1; }
    public float GetY1() { return _lastY1; }
    public float GetX2() { return _lastX2; }
    public float GetY2() { return _lastY2; }
    public bool IsSingleTouch() { return _touchSingle; }
    public bool IsFlickAvailable() { return _flipAvailable; }
    public void DisableFlick() { _flipAvailable = false; }

    /// <summary>
    /// 触摸开始事件
    /// </summary>
    /// <param name="deviceX">触摸屏幕的 x 値</param>
    /// <param name="deviceY">触摸屏幕的 y 値</param>
    public void TouchesBegan(float deviceX, float deviceY)
    {
        _lastX = deviceX;
        _lastY = deviceY;
        _startX = deviceX;
        _startY = deviceY;
        _lastTouchDistance = -1.0f;
        _flipAvailable = true;
        _touchSingle = true;
    }

    /// <summary>
    /// 单指拖拽事件
    /// </summary>
    /// <param name="deviceX">触摸屏幕的 x 値</param>
    /// <param name="deviceY">触摸屏幕的 y 値</param>
    public void TouchesMoved(float deviceX, float deviceY)
    {
        _lastX = deviceX;
        _lastY = deviceY;
        _lastTouchDistance = -1.0f;
        _touchSingle = true;
    }

    /// <summary>
    /// 双指拖拽事件
    /// </summary>
    /// <param name="deviceX1">第 1 根手指触摸屏幕的 x 値</param>
    /// <param name="deviceY1">第 1 根手指触摸屏幕的 y 値</param>
    /// <param name="deviceX2">第 2 根手指触摸屏幕的 x 値</param>
    /// <param name="deviceY2">第 2 根手指触摸屏幕的 y 値</param>
    public void TouchesMoved(float deviceX1, float deviceY1, float deviceX2, float deviceY2)
    {
        float distance = CalculateDistance(deviceX1, deviceY1, deviceX2, deviceY2);
        float centerX = (deviceX1 + deviceX2) * 0.5f;
        float centerY = (deviceY1 + deviceY2) * 0.5f;

        if (_lastTouchDistance > 0.0f)
        {
            _scale = MathF.Pow(distance / _lastTouchDistance, 0.75f);
            _deltaX = CalculateMovingAmount(deviceX1 - _lastX1, deviceX2 - _lastX2);
            _deltaY = CalculateMovingAmount(deviceY1 - _lastY1, deviceY2 - _lastY2);
        }
        else
        {
            _scale = 1.0f;
            _deltaX = 0.0f;
            _deltaY = 0.0f;
        }

        _lastX = centerX;
        _lastY = centerY;
        _lastX1 = deviceX1;
        _lastY1 = deviceY1;
        _lastX2 = deviceX2;
        _lastY2 = deviceY2;
        _lastTouchDistance = distance;
        _touchSingle = false;
    }

    /// <summary>
    /// 轻扫距离测量
    /// </summary>
    /// <returns>轻扫距离</returns>
    public float GetFlickDistance()
    {
        return CalculateDistance(_startX, _startY, _lastX, _lastY);
    }

    /// <summary>
    /// 计算两点间的距离
    /// </summary>
    /// <param name="x1">第 1 个触摸点的 x 値</param>
    /// <param name="y1">第 1 个触摸点的 y 値</param>
    /// <param name="x2">第 2 个触摸点的 x 値</param>
    /// <param name="y2">第 2 个触摸点的 y 値</param>
    /// <returns>两点间的距离</returns>
    public static float CalculateDistance(float x1, float y1, float x2, float y2)
    {
        return MathF.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
    }

    /// <summary>
    /// 根据两个値计算移动量。
    /// 方向相反时为 0，相同时取绝对値较小的値。
    /// </summary>
    /// <param name="v1">第一个移动量</param>
    /// <param name="v2">第二个移动量</param>
    /// <returns>较小的移动量</returns>
    public static float CalculateMovingAmount(float v1, float v2)
    {
        if ((v1 > 0.0f) != (v2 > 0.0f))
        {
            return 0.0f;
        }

        float sign = v1 > 0.0f ? 1.0f : -1.0f;
        float absoluteValue1 = MathF.Abs(v1);
        float absoluteValue2 = MathF.Abs(v2);
        return sign * ((absoluteValue1 < absoluteValue2) ? absoluteValue1 : absoluteValue2);
    }
}
