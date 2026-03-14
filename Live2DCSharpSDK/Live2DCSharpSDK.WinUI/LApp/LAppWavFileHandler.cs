using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Live2DCSharpSDK.WinUI.LApp;

/// <summary>
/// WAVファイルの再生とリップシンク用RMS計算を管理するクラス。
/// CubismSdkForNative の LAppWavFileHandler_Common に相当する。
/// </summary>
public sealed class LAppWavFileHandler : IDisposable
{
    private MediaPlayer? _player;

    // リップシンク用モノラルPCMデータ（-1〜1）
    private float[]? _pcmData;
    private int _sampleRate;
    private int _sampleOffset;
    private float _userTimeSeconds;
    private float _lastRms;

    private CancellationTokenSource? _delayCts;

    /// <summary>最後に計測したRMS値（リップシンク用）</summary>
    public float GetRms() => _lastRms;

    /// <summary>
    /// 毎フレーム呼び出す。RMS値を更新する。
    /// </summary>
    /// <param name="deltaTimeSeconds">前フレームからの経過秒数</param>
    /// <returns>更新されていれば true</returns>
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
    /// WAVファイルの再生を開始する。
    /// </summary>
    /// <param name="filePath">WAVファイルのフルパス</param>
    /// <param name="delayMs">再生開始を遅らせるミリ秒数（SoundDelay）</param>
    public void Start(string filePath, int delayMs = 0)
    {
        // 前回の遅延タスクをキャンセル
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

        // リップシンク用にPCMを先読みしておく
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

    /// <summary>再生を停止し状態をリセットする</summary>
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
    /// WAVファイルを解析してリップシンク用モノラルPCMデータをロードする。
    /// PCMフォーマット（format tag 1）のみ対応。
    /// </summary>
    private bool LoadWav(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            using var br = new BinaryReader(fs);

            // "RIFF" シグネチャ
            if (new string(br.ReadChars(4)) != "RIFF") return false;
            br.ReadInt32(); // ファイルサイズ - 8（読み飛ばし）
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
                    if (audioFormat != 1) return false; // リニアPCMのみ対応
                    channels = br.ReadInt16();
                    _sampleRate = br.ReadInt32();
                    br.ReadInt32(); // 平均データ速度
                    br.ReadInt16(); // ブロックサイズ
                    bitsPerSample = br.ReadInt16();
                    // fmt チャンクの拡張部分を読み飛ばし
                    fs.Position = chunkEnd;
                }
                else if (chunkId == "data")
                {
                    if (_sampleRate == 0 || channels == 0 || bitsPerSample == 0)
                        return false;

                    int bytesPerSample = bitsPerSample / 8;
                    int totalSamples = chunkSize / (bytesPerSample * channels);
                    _pcmData = new float[totalSamples];

                    // マルチチャンネルをモノラルにミックスダウン
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
                    // 奇数サイズのチャンクはパディングバイトあり（RIFF仕様）
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
        // C++版の GetPcmSample に相当
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
                    int s = b0 | (b1 << 8) | ((sbyte)b2 << 16); // 符号付き24bit拡張
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
