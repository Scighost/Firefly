using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Live2DCSharpSDK.WinUI.LApp;

/// <summary>
/// 管理 WAV 文件播放与口型同步用 RMS 计算的类。
/// 对应 CubismSdkForNative 的 LAppWavFileHandler_Common。
/// </summary>
public sealed class LAppWavFileHandler : IDisposable
{
    private MediaPlayer? _player;

    // 口型同步用单声道 PCM 数据（-1～1）
    private float[]? _pcmData;
    private int _sampleRate;
    private int _sampleOffset;
    private float _userTimeSeconds;
    private float _lastRms;

    private CancellationTokenSource? _delayCts;

    /// <summary>最近一次测量的 RMS 値（口型同步用）</summary>
    public float GetRms() => _lastRms;

    /// <summary>
    /// 每帧调用。更新 RMS 値。
    /// </summary>
    /// <param name="deltaTimeSeconds">距上帧经过的秒数</param>
    /// <returns>若有更新则返回 true</returns>
    public bool Update(float deltaTimeSeconds)
    {
        if (_pcmData == null || _sampleOffset >= _pcmData.Length)
        {
            _lastRms = 0f;
            return false;
        }

        _userTimeSeconds += deltaTimeSeconds;
        int goalOffset = Math.Min((int)(_userTimeSeconds * _sampleRate), _pcmData.Length);

        int count = goalOffset - _sampleOffset;
        float rms = 0f;
        if (count > 0)
        {
            for (int i = _sampleOffset; i < goalOffset; i++)
            {
                float s = _pcmData[i];
                rms += s * s;
            }
            rms = MathF.Sqrt(rms / count);
        }

        _lastRms = rms;
        _sampleOffset = goalOffset;
        return true;
    }

    /// <summary>
    /// 开始播放 WAV 文件。
    /// </summary>
    /// <param name="filePath">WAV 文件的完整路径</param>
    /// <param name="delayMs">延迟播放的毫秒数（SoundDelay）</param>
    public void Start(string filePath, int delayMs = 0)
    {
        // 取消上次的延迟任务
        _delayCts?.Cancel();
        _delayCts?.Dispose();
        _delayCts = null;

        _player?.Pause();

        _pcmData = null;
        _sampleOffset = 0;
        _userTimeSeconds = 0f;
        _lastRms = 0f;

        if (!File.Exists(filePath))
            return;

        // 预读 PCM 数据供口型同步使用
        LoadWav(filePath);

        var cts = new CancellationTokenSource();
        _delayCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                if (delayMs > 0)
                    await Task.Delay(delayMs, cts.Token);

                if (!cts.Token.IsCancellationRequested)
                    PlayFile(filePath);
            }
            catch (OperationCanceledException) { }
        });
    }

    /// <summary>停止播放并重置状态</summary>
    public void Stop()
    {
        _delayCts?.Cancel();
        _delayCts?.Dispose();
        _delayCts = null;

        _player?.Pause();

        _pcmData = null;
        _sampleOffset = 0;
        _userTimeSeconds = 0f;
        _lastRms = 0f;
    }

    private void PlayFile(string filePath)
    {
        if (_player == null)
        {
            _player = new MediaPlayer();
        }
        _player.Source = MediaSource.CreateFromUri(new Uri(filePath));
        _player.Play();
    }

    /// <summary>
    /// 解析 WAV 文件，加载口型同步用单声道 PCM 数据。
    /// 仅支持 PCM 格式（format tag 1）。
    /// </summary>
    private bool LoadWav(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            using var br = new BinaryReader(fs);

            // "RIFF" 签名
            if (new string(br.ReadChars(4)) != "RIFF") return false;
            br.ReadInt32(); // 文件大小 - 8（跳过）
            if (new string(br.ReadChars(4)) != "WAVE") return false;

            int channels = 0, bitsPerSample = 0;
            _sampleRate = 0;

            while (fs.Position <= fs.Length - 8)
            {
                var chunkId = new string(br.ReadChars(4));
                int chunkSize = br.ReadInt32();
                long chunkEnd = fs.Position + chunkSize;

                if (chunkId == "fmt ")
                {
                    short audioFormat = br.ReadInt16();
                    if (audioFormat != 1) return false; // 仅支持线性 PCM
                    channels = br.ReadInt16();
                    _sampleRate = br.ReadInt32();
                    br.ReadInt32(); // 平均数据速率
                    br.ReadInt16(); // 块大小
                    bitsPerSample = br.ReadInt16();
                    // 跳过 fmt 块的扩展部分
                    fs.Position = chunkEnd;
                }
                else if (chunkId == "data")
                {
                    if (_sampleRate == 0 || channels == 0 || bitsPerSample == 0)
                        return false;

                    int bytesPerSample = bitsPerSample / 8;
                    int totalSamples = chunkSize / (bytesPerSample * channels);
                    _pcmData = new float[totalSamples];

                    // 将多声道混音为单声道
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float mixed = 0f;
                        for (int c = 0; c < channels; c++)
                            mixed += ReadNormalizedSample(br, bitsPerSample);
                        _pcmData[i] = mixed / channels;
                    }
                    return true;
                }
                else
                {
                    // 奇数大小的块有填充字节（RIFF 规范）
                    fs.Position = chunkEnd + (chunkSize & 1);
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static float ReadNormalizedSample(BinaryReader br, int bitsPerSample)
    {
        // 对应 C++ 版的 GetPcmSample
        switch (bitsPerSample)
        {
            case 8:
                return (br.ReadByte() - 128) / 128f;
            case 16:
                return br.ReadInt16() / 32768f;
            case 24:
                {
                    int b0 = br.ReadByte();
                    int b1 = br.ReadByte();
                    int b2 = br.ReadByte();
                    int s = b0 | (b1 << 8) | ((sbyte)b2 << 16); // 符号扩展到 24 位有符号整数
                    return s / 8388608f;
                }
            case 32:
                return br.ReadInt32() / (float)int.MaxValue;
            default:
                return 0f;
        }
    }

    public void Dispose()
    {
        _delayCts?.Cancel();
        _delayCts?.Dispose();
        _delayCts = null;
        _player?.Dispose();
        _player = null;
    }
}
