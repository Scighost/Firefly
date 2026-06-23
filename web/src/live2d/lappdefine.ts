import { LogLevel } from '@framework/live2dcubismframework';

export const CanvasSize: { width: number; height: number } | 'auto' = 'auto';

export const ViewScale = 1;
export const ViewMaxScale = 2.0;
export const ViewMinScale = 0.2;

export const ViewLogicalLeft = -1.0;
export const ViewLogicalRight = 1.0;
export const ViewLogicalBottom = -1.0;
export const ViewLogicalTop = 1.0;

export const ViewLogicalMaxLeft = -2.0;
export const ViewLogicalMaxRight = 2.0;
export const ViewLogicalMaxBottom = -2.0;
export const ViewLogicalMaxTop = 2.0;

export const ResourcesPath = '/model/';
export const ShaderPath = '/Framework/Shaders/WebGL/';

export const ModelDir: string[] = ['FileReferences_Moc_0'];
export const ModelDirSize: number = ModelDir.length;

export const MotionGroupIdle = 'Tick2';
export const MotionGroupTapBody = '表情组';

export const HitAreaNameDrink = '饮料';
export const HitAreaNameCake = '蛋糕';
export const HitAreaNameBangs = '刘海';
export const HitAreaNameLeftHair = '左侧后发';
export const HitAreaNameRightHair = '右侧后发';

export const PriorityNone = 0;
export const PriorityIdle = 1;
export const PriorityNormal = 2;
export const PriorityForce = 3;

export const MOCConsistencyValidationEnable = true;
export const MotionConsistencyValidationEnable = true;

export const DebugLogEnable = false;
export const DebugTouchLogEnable = false;

export const CubismLoggingLevel: LogLevel = LogLevel.LogLevel_Verbose;
