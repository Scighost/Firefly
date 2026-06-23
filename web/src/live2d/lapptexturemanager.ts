import { LAppGlManager } from './lappglmanager';

export class TextureInfo {
  id: WebGLTexture = null;
  width = 0;
  height = 0;
}

export class LAppTextureManager {
  public setGlManager(glManager: LAppGlManager): void {
    this._glManager = glManager;
  }

  public createTextureFromPngFile(
    fileName: string,
    usePremultiply: boolean,
    callback: (textureInfo: TextureInfo) => void
  ): void {
    const gl = this._glManager.getGl();

    const img = new Image();
    img.onerror = () => {
      console.error(`[Live2D] Failed to load texture: ${fileName}`);
    };
    img.onload = () => {
      const tex: WebGLTexture = gl.createTexture();

      gl.bindTexture(gl.TEXTURE_2D, tex);

      gl.texParameteri(
        gl.TEXTURE_2D,
        gl.TEXTURE_MIN_FILTER,
        gl.LINEAR_MIPMAP_LINEAR
      );
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);

      if (usePremultiply) {
        gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, 1);
      }

      gl.texImage2D(
        gl.TEXTURE_2D,
        0,
        gl.RGBA,
        gl.RGBA,
        gl.UNSIGNED_BYTE,
        img
      );

      gl.generateMipmap(gl.TEXTURE_2D);

      gl.bindTexture(gl.TEXTURE_2D, null);

      const textureInfo = new TextureInfo();
      textureInfo.id = tex;
      textureInfo.width = img.width;
      textureInfo.height = img.height;

      this._textures.push(textureInfo);

      callback(textureInfo);
    };
    img.src = fileName;
  }

  public release(): void {
    for (const tex of this._textures) {
      this._glManager.getGl().deleteTexture(tex.id);
    }
    this._textures = [];
  }

  private _glManager: LAppGlManager = null;
  private _textures: TextureInfo[] = [];
}
