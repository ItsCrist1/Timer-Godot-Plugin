using Godot;

[GlobalClass]
public partial class TimerConfig : Resource {
    [Export] public float MaxTime = 4f;
    [Export] public bool AutoRefresh;
    [Export] public bool AutoStart;
    [Export] public bool TickOnPause;
    [Export] public bool DisposeOnTimeout;
}