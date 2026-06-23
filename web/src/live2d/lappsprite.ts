import { LAppSubdelegate } from './lappsubdelegate';

export class LAppSprite {
  constructor(
    x: number,
    y: number,
    width: number,
    height: number,
    textureId: WebGLTexture
  ) {
    this._rect = new Float32Array([
      x - width * 0.5,
      y + height * 0.5,
      x + width * 0.5,
      y + height * 0.5,
      x - width * 0.5,
      y - height * 0.5,
      x + width * 0.5,
      y - height * 0.5
    ]);
    this._uv = new Float32Array([0.0, 0.0, 1.0, 0.0, 0.0, 1.0, 1.0, 1.0]);
    this._textureId = textureId;
  }

  public setSubdelegate(subdelegate: LAppSubdelegate): void {
    this._subdelegate = subdelegate;
  }

  public render(programId: WebGLProgram): void {
    if (this._textureId === null || !this._subdelegate) return;

    const gl = this._subdelegate.getGl();

    const positionLocation = gl.getAttribLocation(programId, 'position');
    const uvLocation = gl.getAttribLocation(programId, 'uv');
    const textureLocation = gl.getUniformLocation(programId, 'texture');

    gl.uniform1i(textureLocation, 0);

    gl.enableVertexAttribArray(positionLocation);
    gl.bindBuffer(gl.ARRAY_BUFFER, this._vertexBufferId);
    gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);

    gl.enableVertexAttribArray(uvLocation);
    gl.bindBuffer(gl.ARRAY_BUFFER, this._uvBufferId);
    gl.vertexAttribPointer(uvLocation, 2, gl.FLOAT, false, 0, 0);

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, this._textureId);

    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
  }

  public isHit(pointX: number, pointY: number): boolean {
    const x = this._rect[0];
    const y = this._rect[7];
    const w = this._rect[2] - x;
    const h = this._rect[1] - y;

    return pointX >= x && pointX <= x + w && pointY >= y && pointY <= y + h;
  }

  public release(): void {
    if (this._subdelegate) {
      const gl = this._subdelegate.getGl();
      gl.deleteBuffer(this._vertexBufferId);
      gl.deleteBuffer(this._uvBufferId);
    }
  }

  private _rect: Float32Array;
  private _uv: Float32Array;
  private _textureId: WebGLTexture;
  private _vertexBufferId: WebGLBuffer = null;
  private _uvBufferId: WebGLBuffer = null;
  private _subdelegate: LAppSubdelegate = null;
}
