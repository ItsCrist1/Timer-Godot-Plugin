using System;
using System.Collections.Generic;

using Godot;

public class Timer : IDisposable {
	const string GENERIC_ERROR_TIMER = "Failed to create `Timer`";

	LinkedListNode<WeakReference<Timer>> timerListRef;

	TimerConfig Config;
	public event Action OnStart, OnTick, OnTimeout, OnStop;
	public float Time { private get; set; }

	bool isPaused, isDisposed;

	internal Timer(TimerConfig Config=null) {
		this.Config = Config ?? new();

		if(isDisposed = !validateConfig(this.Config)) return;

		if(Config.AutoStart) Time = Config.MaxTime;

		timerListRef = TimerManager.RegisterTimer(new (this));
	}

	public void SetConfig(TimerConfig Config=null) {
		if(isDisposed || Config == null || !validateConfig(Config)) return;
		this.Config = Config;
	}

	bool validateConfig(TimerConfig Config) {
		if(Config.MaxTime < 0f) {
			GD.PushError($"{GENERIC_ERROR_TIMER}:\nMaxTime is negative");
			return false;
		}

		return true;
	}

	~Timer() => Dispose();

	internal void Tick(float dt) {
		if(isDisposed || isPaused || Time <= 0f
		||(!Config.TickOnPause && Engine.TimeScale == 0f))
			return;

		Time -= dt;
		OnTick?.Invoke();

		if(Time <= 0f) {
			OnTimeout?.Invoke();

			if(Config.AutoRefresh) Time += Config.MaxTime;
			if(Config.DisposeOnTimeout) Dispose();
		}
	}

	public void Start(float maxTime = float.NaN) {
		if(isDisposed) return;

		OnStart?.Invoke();

		if(maxTime == 0f) {
			OnTimeout?.Invoke();
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

	public void Dispose() {
		if(isDisposed) return;
		isDisposed = true;

		if(timerListRef.List != null)
			timerListRef.List.Remove(timerListRef);

		OnStart = OnTick = OnTimeout = OnStop = null;
		GC.SuppressFinalize(this);
	}

	public bool IsGoing
		=> !isPaused && Time > 0f;

	public bool HasFinished
		=> !isPaused && Time <= 0f;
}
