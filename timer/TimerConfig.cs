using Godot;

public enum TickRate { Process, PhysicsProcess, Arbitrary }

[GlobalClass]
public partial class TimerConfig : Resource {
    [ExportGroup("Options")]
    [Export] public float MaxTime = 1f;
    [Export] public bool AutoRefresh = false;
    [Export] public bool AutoStart = false;
    [Export] public bool TickOnPause = false;
    [Export] public bool TickOnZeroTimeScale = true;
    [Export] public bool DisposeOnTimeout = false;

    
    [ExportGroup("Tick Rate")]
    [Export] public TickRate TickRate = TickRate.Process;
    [Export] public float TickFrequency = .1f;

    public bool IsNode { get; set; }
    public bool DoMonitor { get; set; } = true;

    public TimerConfig Clone() {
        TimerConfig config = Duplicate() as TimerConfig ?? new();

        config.IsNode = IsNode;
        config.DoMonitor = DoMonitor;

        return config;
    }
}