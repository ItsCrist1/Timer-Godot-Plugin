using System;
using System.Collections.Generic;

using Godot;

public partial class TimerManager : Node {
	const string GENERIC_LOG_TIMER_MANAGER = "Timer Manager Log";

	public static TimerManager Instance { get; private set; }

	static LinkedList<WeakReference<Timer>> PendingTimers = new();
	LinkedList<WeakReference<Timer>> Timers = new();

	public static LinkedListNode<WeakReference<Timer>> RegisterTimer(WeakReference<Timer> timerWeakRef)
		=> Instance == null ? PendingTimers.AddLast(timerWeakRef) : Instance.Timers.AddLast(timerWeakRef);
	
	public override void _EnterTree() {
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;

		foreach(WeakReference<Timer> timer in PendingTimers)
			Timers.AddLast(timer);

		if(PendingTimers.Count != 0) 
			GD.Print($"{GENERIC_LOG_TIMER_MANAGER}:\nTimers moved from pending static list to active one: {PendingTimers.Count}");

		PendingTimers.Clear();

		Performance.AddCustomMonitor("Timers/Active_Count", Callable.From(() => Timers.Count));
    	Performance.AddCustomMonitor("Timers/Pending_Count", Callable.From(() => PendingTimers.Count));
	}

	public override void _Process(double delta) {
		float dt = (float)delta;

		LinkedListNode<WeakReference<Timer>> currentNode = Timers.First;

		while(currentNode != null) {
			LinkedListNode<WeakReference<Timer>> nextNode = currentNode.Next;
			
			if(currentNode.Value.TryGetTarget(out Timer timer))
				timer.Tick(dt);
			else
				Timers.Remove(currentNode);

			currentNode = nextNode;
		}
	}

	public override void _ExitTree() {
		foreach(WeakReference<Timer> timerRef in Timers)
			if(timerRef.TryGetTarget(out Timer timer))
				timer.Dispose();

		Timers.Clear();
		Instance = null;
	}

	public static Timer Create(TimerConfig Config=null)
		=> new (Config?.Duplicate() as TimerConfig ?? new());

	public static Timer CreateLooping(TimerConfig Config=null) {
		TimerConfig newConfig = Config ?? new();
		newConfig.AutoRefresh = true;
		return new (newConfig);
	}

	public static Timer CreateOneShotTimer(float maxTime, Action onTimeout, TimerConfig Config=null) {
		TimerConfig newConfig = Config ?? new();
		newConfig.MaxTime = maxTime;
		newConfig.AutoStart = newConfig.DisposeOnTimeout = true;
		
		Timer timer = new (newConfig);

		timer.OnTimeout += onTimeout;

		return timer;
	}
}
