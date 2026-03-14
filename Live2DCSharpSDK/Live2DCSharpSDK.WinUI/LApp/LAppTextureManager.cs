using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Graphics.Imaging;

namespace Live2DCSharpSDK.WinUI.LApp;

/// <summary>
/// 画像読み込み、管理を行うクラス。
/// </summary>
public class LAppTextureManager(LAppDelegate lapp)
{
    private readonly List<TextureInfo> _textures = [];

    /// <summary>
    /// 画像読み込み
    /// </summary>
    /// <param name="fileName">読み込む画像ファイルパス名</param>
    /// <returns>画像情報。読み込み失敗時はNULLを返す</returns>
    public unsafe TextureInfo CreateTextureFromPngFile(LAppModel model, int index, string fileName)
    {
        //search loaded texture already.
        var item = _textures.FirstOrDefault(a => a.FileName == fileName);
        if (item != null)
        {
            // Texture data already on GPU; just rebind it to this model's renderer.
            lapp.RebindTexture(model, index, item);
            return item;
        }

        using var fs = File.OpenRead(fileName);
        var decoder = BitmapDecoder.CreateAsync(fs.AsRandomAccessStream()).GetAwaiter().GetResult();
        var pidelDataProvider = decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight, new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage).GetAwaiter().GetResult();
        byte[] pixelData = pidelDataProvider.DetachPixelData();

        TextureInfo info;
        fixed (byte* p = pixelData)
        {
            info = lapp.CreateTexture(model, index, (int)decoder.PixelWidth, (int)decoder.PixelHeight, (nint)p);
        }
        info.FileName = fileName;
        info.Width = (int)decoder.PixelWidth;
        info.Index = index;
        info.Height = (int)decoder.PixelHeight;

        _textures.Add(info);

        return info;
    }

    /// <summary>
    /// 指定したテクスチャIDの画像を解放する
    /// </summary>
    /// <param name="textureId">解放するテクスチャID</param>
    public void ReleaseTexture(TextureInfo info)
    {
        info.Dispose();
        _textures.Remove(info);
    }
}
