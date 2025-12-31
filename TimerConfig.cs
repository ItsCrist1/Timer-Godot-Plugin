using Godot;

public enum TickRate { Process, PhysicsProcess, Arbitrary }

[GlobalClass]
public partial class TimerConfig : Resource {
    [ExportGroup("Options")]
    [Export] public float MaxTime = 1f;
    [Export] public bool AutoRefresh = false;
    [Export] public bool AutoStart = false;
    [Export] public bool TickOnPause = false;
    [Export] public bool DisposeOnTimeout = false;

    
    [ExportGroup("Tick Rate")]
    [Export] public TickRate TickRate = TickRate.Process;
    [Export] public float TickFrequency = .1f;
}