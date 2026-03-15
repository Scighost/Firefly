using System.Numerics;

namespace Live2DCSharpSDK.Framework.Math;

/// <summary>
/// 用于数值计算等的工具类
/// </summary>
public static class CubismMath
{
    public const float Pi = 3.1415926535897932384626433832795f;
    public const float Epsilon = 0.00001f;

    /// <summary>
    /// 将第一个参数限制到最小值与最大值的范围内并返回该值
    /// </summary>
    /// <param name="value">要限制的值</param>
    /// <param name="min">范围的最小值</param>
    /// <param name="max">范围的最大值</param>
    /// <returns>被限制在最小值与最大值范围内的值</returns>
    public static float RangeF(float value, float min, float max)
    {
        if (value < min) value = min;
        else if (value > max) value = max;
        return value;
    }

    /// <summary>
    /// 计算经过缓动处理的正弦值。
    /// 可用于淡入/淡出时的缓动。
    /// </summary>
    /// <param name="value">要进行缓动的值</param>
    /// <returns>缓动处理后的正弦值</returns>
    public static float GetEasingSine(float value)
    {
        if (value < 0.0f) return 0.0f;
        else if (value > 1.0f) return 1.0f;

        return 0.5f - 0.5f * MathF.Cos(value * Pi);
    }

    /// <summary>
    /// 返回较大的值。
    /// </summary>
    /// <param name="l">左侧的值</param>
    /// <param name="r">右侧的值</param>
    /// <returns>较大的值</returns>
    public static float Max(float l, float r)
    {
        return (l > r) ? l : r;
    }

    /// <summary>
    /// 返回较小的值。
    /// </summary>
    /// <param name="l">左侧的值</param>
    /// <param name="r">右侧的值</param>
    /// <returns>较小的值</returns>
    public static float Min(float l, float r)
    {
        return (l > r) ? r : l;
    }

    /// <summary>
    /// 将角度值转换为弧度值。
    /// </summary>
    /// <param name="degrees">角度值</param>
    /// <returns>由角度值转换得到的弧度值</returns>
    public static float DegreesToRadian(float degrees)
    {
        return degrees / 180.0f * Pi;
    }

    /// <summary>
    /// 将弧度值转换为角度值。
    /// </summary>
    /// <param name="radian">弧度值</param>
    /// <returns>由弧度值转换得到的角度值</returns>
    public static float RadianToDegrees(float radian)
    {
        return radian * 180.0f / Pi;
    }

    /// <summary>
    /// 根据两个向量计算方向的弧度值。
    /// </summary>
    /// <param name="from">起点向量</param>
    /// <param name="to">终点向量</param>
    /// <returns>由两个向量计算得到的弧度值</returns>
    public static float DirectionToRadian(Vector2 from, Vector2 to)
    {
        float q1;
        float q2;
        float ret;

        q1 = MathF.Atan2(to.Y, to.X);
        q2 = MathF.Atan2(from.Y, from.X);

        ret = q1 - q2;

        while (ret < -Pi)
        {
            ret += Pi * 2.0f;
        }

        while (ret > Pi)
        {
            ret -= Pi * 2.0f;
        }

        return ret;
    }

    /// <summary>
    /// 根据两个向量计算方向的角度值。
    /// </summary>
    /// <param name="from">起点向量</param>
    /// <param name="to">终点向量</param>
    /// <returns>由两个向量计算得到的角度值</returns>
    public static float DirectionToDegrees(Vector2 from, Vector2 to)
    {
        float radian;
        float degree;

        radian = DirectionToRadian(from, to);
        degree = RadianToDegrees(radian);

        if ((to.X - from.X) > 0.0f)
        {
            degree = -degree;
        }

        return degree;
    }

    /// <summary>
    /// 将弧度值转换为方向向量。
    /// </summary>
    /// <param name="totalAngle">弧度值</param>
    /// <returns>由弧度值转换得到的方向向量</returns>
    public static Vector2 RadianToDirection(float totalAngle)
    {
        return new Vector2(MathF.Sin(totalAngle), MathF.Cos(totalAngle));
    }

    /// <summary>
    /// 当三次方程的三次项系数为0时，用于补充地求解二次方程的解。
    /// a * x^2 + b * x + c = 0
    /// </summary>
    /// <param name="a">二次项系数</param>
    /// <param name="b">一次项系数</param>
    /// <param name="c">常数项值</param>
    /// <returns>二次方程的解</returns>
    public static float QuadraticEquation(float a, float b, float c)
    {
        if (MathF.Abs(a) < Epsilon)
        {
            if (MathF.Abs(b) < Epsilon)
            {
                return -c;
            }
            return -c / b;
        }

        return -(b + MathF.Sqrt(b * b - 4.0f * a * c)) / (2.0f * a);
    }

    /// <summary>
    /// 使用卡尔达诺公式求解与贝塞尔 t 值对应的三次方程的解。
    /// 若为重根，则返回位于 0.0～1.0 之间的解。
    /// 
    /// a * x^3 + b * x^2 + c * x + d = 0
    /// </summary>
    /// <param name="a">三次项系数</param>
    /// <param name="b">二次项系数</param>
    /// <param name="c">一次项系数</param>
    /// <param name="d">常数项值</param>
    /// <returns>位于 0.0 到 1.0 之间的解</returns>
    public static float CardanoAlgorithmForBezier(float a, float b, float c, float d)
    {
        if (MathF.Abs(a) < Epsilon)
        {
            return RangeF(QuadraticEquation(b, c, d), 0.0f, 1.0f);
        }

        float ba = b / a;
        float ca = c / a;
        float da = d / a;

        float p = (3.0f * ca - ba * ba) / 3.0f;
        float p3 = p / 3.0f;
        float q = (2.0f * ba * ba * ba - 9.0f * ba * ca + 27.0f * da) / 27.0f;
        float q2 = q / 2.0f;
        float discriminant = q2 * q2 + p3 * p3 * p3;

        float center = 0.5f;
        float threshold = center + 0.01f;

        float root1;
        float u1;

        if (discriminant < 0.0f)
        {
            float mp3 = -p / 3.0f;
            float mp33 = mp3 * mp3 * mp3;
            float r = MathF.Sqrt(mp33);
            float t = -q / (2.0f * r);
            float cosphi = RangeF(t, -1.0f, 1.0f);
            float phi = MathF.Acos(cosphi);
            float crtr = MathF.Cbrt(r);
            float t1 = 2.0f * crtr;

            root1 = t1 * MathF.Cos(phi / 3.0f) - ba / 3.0f;
            if (MathF.Abs(root1 - center) < threshold)
            {
                return RangeF(root1, 0.0f, 1.0f);
            }

            float root2 = t1 * MathF.Cos((phi + 2.0f * Pi) / 3.0f) - ba / 3.0f;
            if (MathF.Abs(root2 - center) < threshold)
            {
                return RangeF(root2, 0.0f, 1.0f);
            }

            float root3 = t1 * MathF.Cos((phi + 4.0f * Pi) / 3.0f) - ba / 3.0f;
            return RangeF(root3, 0.0f, 1.0f);
        }
        else if (discriminant == 0.0f)
        {
            if (q2 < 0.0f)
            {
                u1 = MathF.Cbrt(-q2);
            }
            else
            {
                u1 = -MathF.Cbrt(q2);
            }

            root1 = 2.0f * u1 - ba / 3.0f;
            if (MathF.Abs(root1 - center) < threshold)
            {
                return RangeF(root1, 0.0f, 1.0f);
            }

            float root2 = -u1 - ba / 3.0f;
            return RangeF(root2, 0.0f, 1.0f);
        }

        float sd = MathF.Sqrt(discriminant);
        u1 = MathF.Cbrt(sd - q2);
        float v1 = MathF.Cbrt(sd + q2);
        root1 = u1 - v1 - ba / 3.0f;
        return RangeF(root1, 0.0f, 1.0f);
    }

    /// <summary>
    /// 将值限制在范围内并返回
    /// </summary>
    /// <param name="val">要限制的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>限制在范围内的值</returns>
    public static int Clamp(int val, int min, int max)
    {
        if (val < min)
        {
            return min;
        }
        else if (max < val)
        {
            return max;
        }

        return val;
    }

    /// <summary>
    /// 将值限制在范围内并返回
    /// </summary>
    /// <param name="val">要限制的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>限制在范围内的值</returns>
    public static float ClampF(float val, float min, float max)
    {
        if (val < min)
        {
            return min;
        }
        else if (max < val)
        {
            return max;
        }

        return val;
    }

    /// <summary>
    /// 计算浮点数的余数。
    /// </summary>
    /// <param name="dividend">被除数（被除的值）</param>
    /// <param name="divisor">除数（用于除的值）</param>
    /// <returns>余数</returns>
    public static float ModF(float dividend, float divisor)
    {
        if (!float.IsFinite(dividend) || divisor == 0 || float.IsNaN(dividend) || float.IsNaN(divisor))
        {
            CubismLog.Warning("[Live2D SDK]dividend: %f, divisor: %f ModF() returns 'NaN'.", dividend, divisor);
            return float.NaN;
        }

        // 转换为绝对值。
        float absDividend = MathF.Abs(dividend);
        float absDivisor = MathF.Abs(divisor);

        // 用绝对值进行除法。
        float result = absDividend - MathF.Floor(absDividend / absDivisor) * absDivisor;

        // 将符号设置为被除数的符号。
        return MathF.CopySign(result, dividend);
    }
}
