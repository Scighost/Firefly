export class TouchManager {
  public touchesBegan(x: number, y: number): void {
    this._startX = x;
    this._startY = y;
    this._lastX = x;
    this._lastY = y;
    this._currentX = x;
    this._currentY = y;
    this._isTouch = true;
  }

  public touchesMoved(x: number, y: number): void {
    this._lastX = this._currentX;
    this._lastY = this._currentY;
    this._currentX = x;
    this._currentY = y;
  }

  public touchesEnded(x: number, y: number): void {
    this._isTouch = false;
  }

  public getX(): number {
    return this._currentX;
  }

  public getY(): number {
    return this._currentY;
  }

  public getDeltaX(): number {
    return this._lastX - this._currentX;
  }

  public getDeltaY(): number {
    return this._lastY - this._currentY;
  }

  public getStartX(): number {
    return this._startX;
  }

  public getStartY(): number {
    return this._startY;
  }

  public isTouch(): boolean {
    return this._isTouch;
  }

  private _startX = 0;
  private _startY = 0;
  private _lastX = 0;
  private _lastY = 0;
  private _currentX = 0;
  private _currentY = 0;
  private _isTouch = false;
}
