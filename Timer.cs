using System;
using System.Collections.Generic;

using Godot;

public struct TimerConfig {
	public float MaxTime { get; set; }
	public bool AutoRefresh { get; set; }
	public bool AutoStart { get; set; }
	public bool TickOnPause { get; set; }
	public bool DisposeOnTimeout { get; set; }
}

public class Timer : IDisposable {
	const string GENERIC_ERROR_TIMER = "Failed to create `Timer`";

	LinkedListNode<WeakReference<Timer>> timerListRef;

	TimerConfig Config;
	public event Action OnStart, OnTick, Timeout, OnStop;
	public float Time { private get; set; }

	bool isPaused, isDisposed;

	internal Timer(TimerConfig Config = default) {
		this.Config = Config;

		if(Config.MaxTime < 0f) {
			GD.PushError($"{GENERIC_ERROR_TIMER}:\nMaxTime is negative");

			isDisposed = true;
			return;
		}

		if(Config.AutoStart) Time = Config.MaxTime;

		timerListRef = TimerManager.RegisterTimer(new (this));
	}

	~Timer() => Dispose();

	internal void Tick(float dt) {
		if(isDisposed || isPaused || Time <= 0f
		||(!Config.TickOnPause && Engine.TimeScale == 0f))
			return;

		Time -= dt;
		OnTick?.Invoke();

		if(Time <= 0f) {
			Timeout?.Invoke();
			
			if(Config.AutoRefresh) Time += Config.MaxTime;
			if(Config.DisposeOnTimeout) Dispose();
		}
	}

	public void Start(float maxTime = float.NaN) {
		if(isDisposed) return;

		OnStart?.Invoke();

		if(maxTime == 0f) {
			Timeout?.Invoke();
			if (Config.DisposeOnTimeout) Dispose();
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
