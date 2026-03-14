using System.ComponentModel;

namespace Live2DCSharpSDK.Framework;

public record ModelSettingObj
{
    public record FileReference
    {
        public record Expression
        {
            public string Name { get; set; }
            public string File { get; set; }
        }
        public record Motion
        {
            public string Name { get; set; }
            public string File { get; set; }
            public int WrapMode { get; set; }
            public string Sound { get; set; }
            public int SoundDelay { get; set; }
            public string Expression { get; set; }
            public int Priority { get; set; }
            public string NextMtn { get; set; }
            public float FadeInTime { get; set; } = -1;
            public float FadeOutTime { get; set; } = -1;
            public bool Interruptable { get; set; }
        }
        public string Moc { get; set; }
        public List<string> Textures { get; set; }
        public string Physics { get; set; }
        public string Pose { get; set; }
        public string DisplayInfo { get; set; }
        public List<Expression> Expressions { get; set; }
        public Dictionary<string, List<Motion>> Motions { get; set; }
        public string UserData { get; set; }
    }

    public record HitArea
    {
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>格式: "GroupName:MotionName"</summary>
        public string Motion { get; set; }
    }

    public record Parameter
    {
        public string Name { get; set; }
        public List<string> Ids { get; set; }
    }

    public FileReference FileReferences { get; set; }
    public List<HitArea> HitAreas { get; set; }
    public Dictionary<string, float> Layout { get; set; }
    public List<Parameter> Groups { get; set; }
}
