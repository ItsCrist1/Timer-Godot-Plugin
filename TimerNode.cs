using System;
using Godot;

public partial class TimerNode : Node {
    [Export] TimerConfig TimerConfig;

    [Signal] delegate void OnStartEventHandler();
    [Signal] delegate void OnTickEventHandler();
    [Signal] delegate void OnTimeoutEventHandler();
    [Signal] delegate void OnStopEventHandler();

    Timer timer;

    public override void _EnterTree() {
        TimerConfig ??= new();
        timer = TimerManager.Create(TimerConfig);

        if(timer == null) return;

        timer.OnStart += _OnStart;
        timer.OnTick += _OnTick;
        timer.OnTimeout += _OnTimeout;
        timer.OnStop += _OnStop;
    }

    public override void _ExitTree() {
        timer?.Dispose();
        timer = null;
    }


    public void Start(float maxTime=float.NaN)
        => timer?.Start(maxTime);

    public void Pause() => timer?.Pause();
    public void Resume() => timer?.Resume();
    public void Stop() => timer?.Stop();
    public void DisposeEarly() => timer?.Dispose();

    public bool IsGoing => timer.IsGoing;
    public bool HasFinished => timer.HasFinished;

    void _OnStart() => EmitSignal(SignalName.OnStart);
    void _OnTick() => EmitSignal(SignalName.OnTick);
    void _OnTimeout() => EmitSignal(SignalName.OnTimeout);
    void _OnStop() => EmitSignal(SignalName.OnStop);
}