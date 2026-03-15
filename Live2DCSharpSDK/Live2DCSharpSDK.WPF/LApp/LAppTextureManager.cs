using System.IO;
using System.Windows.Media.Imaging;

namespace Live2DCSharpSDK.WPF.LApp;

/// <summary>
/// 负责图像加载和管理的类。
/// </summary>
public class LAppTextureManager(LAppDelegate lapp)
{
    private readonly List<TextureInfo> _textures = [];

    /// <summary>
    /// 加载图像。
    /// </summary>
    /// <param name="fileName">要加载的图像文件路径</param>
    /// <returns>图像信息。加载失败时返回 NULL</returns>
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
        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
        BitmapSource frame = decoder.Frames[0];
        int width = frame.PixelWidth;
        int height = frame.PixelHeight;
        byte[] pixels = new byte[width * height * 4];
        frame.CopyPixels(pixels, width * 4, 0);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte b = pixels[i];
            pixels[i] = pixels[i + 2];
            pixels[i + 2] = b;
        }

        TextureInfo info;
        fixed (byte* p = pixels)
        {
            info = lapp.CreateTexture(model, index, width, height, (nint)p);
        }
        info.FileName = fileName;
        info.Width = width;
        info.Index = index;
        info.Height = height;

        _textures.Add(info);

        return info;
    }

    /// <summary>
    /// 释放指定纹理 ID 对应的图像。
    /// </summary>
    /// <param name="textureId">要释放的纹理 ID</param>
    public void ReleaseTexture(TextureInfo info)
    {
        info.Dispose();
        _textures.Remove(info);
    }
}
