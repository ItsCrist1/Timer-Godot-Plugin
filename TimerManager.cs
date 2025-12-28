using System;
using System.Collections;
using System.Collections.Generic;

using Godot;

public partial class TimerManager : Node {
	const string GENERIC_LOG_TIMER_MANAGER = "Timer Manager Log";

	public static TimerManager Instance { get; private set; }

	static LinkedList<WeakReference<Timer>> PendingTimers = new();
	LinkedList<WeakReference<Timer>> Timers = new();
	
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

	public struct TimerConfig {
		public float MaxTime { get; set; }
		public bool AutoRefresh { get; set; }
		public bool AutoStart { get; set; }
		public bool TickOnPause { get; set; }
		public bool DisposeOnTimeout { get; set; }
	}

	public static Timer Create(TimerConfig Config = default)
		=> new (Config);

	public static Timer CreateLooping(TimerConfig Config = default) {
		Config.AutoRefresh = true;
		return new (Config);
	}

	public static Timer CreateOneShotTimer(float maxTime, Action onTimeout) {
		Timer timer = new Timer(
			new() {
				MaxTime = maxTime, 
				AutoStart = true, 
				DisposeOnTimeout = true }
		);

		timer.Timeout += onTimeout;

		return timer;
	}

	public class Timer : IDisposable {
		const string GENERIC_ERROR_TIMER = "Failed to create `Timer`";

		LinkedListNode<WeakReference<Timer>> timerListRef;

		TimerConfig Config;
		public event Action OnStart, OnTick, Timeout, OnStop;
		public float Time { private get; set; }

		bool isPaused, isDisposed;

		internal Timer (TimerConfig Config = default) {
			this.Config = Config;

			if(Config.MaxTime < 0f) {
				GD.PushError($"{GENERIC_ERROR_TIMER}:\nMaxTime is negative");

				isDisposed = true;
				return;
			}
			
			if(Config.AutoStart) Time = Config.MaxTime;

			WeakReference<Timer> timerWeakRef = new (this);

			if(Instance == null) timerListRef = PendingTimers.AddLast(timerWeakRef);
			else timerListRef = Instance.Timers.AddLast(timerWeakRef);
		}

		~Timer() => Dispose();

		internal void Tick(float dt) {
			if(isDisposed || isPaused || Time <= 0f 
			|| (!Config.TickOnPause && Engine.TimeScale == 0f))
				return;

			Time -= dt;
			OnTick?.Invoke();

			if(Time <= 0f) {
				Timeout?.Invoke();
				if(Config.AutoRefresh) Time += Config.MaxTime;
				if(Config.DisposeOnTimeout) Dispose();
			}
		}

		public void Start(float maxTime=float.NaN) {
			if(isDisposed) return;

			OnStart?.Invoke();

			if(maxTime == 0f) {
				Timeout?.Invoke();
				if(Config.DisposeOnTimeout) Dispose();
				return;
			}

			maxTime = float.IsNaN(maxTime) ? Config.MaxTime : maxTime;
			
			Config.MaxTime = Time = maxTime;
			
			Resume();
		}
		
		public void Pause() {
			if(isDisposed) return;
			isPaused = true;
		}

		public void Resume() {
			if(isDisposed) return;
			isPaused = false;
		}

		public void Stop() {
			if(isDisposed) return;

			OnStop?.Invoke();

			Time = 0f;
			Resume();
		}

		public bool IsGoing
			=> !isPaused && Time > 0f;

		public bool HasFinished
			=> !isPaused && Time <= 0f;

		public void Dispose() {
			if(isDisposed) return;
			isDisposed = true;

			if(timerListRef.List != null)
				timerListRef.List.Remove(timerListRef);

			OnStart = OnTick = Timeout = OnStop = null;
			GC.SuppressFinalize(this);
		}
	}
}
