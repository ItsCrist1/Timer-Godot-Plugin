using System;

using Godot;

public class Timer : IDisposable {
	const string GENERIC_ERROR_TIMER = "Failed to create `Timer`";

	internal Timer tickSourceTimer;

	public TimerConfig Config {
		get => _Config;
		set {
			if(isDisposed || value == null || !validateConfig(value)) return;
			_Config = value;
		}
	}
	public event Action OnStart, OnTick, OnTimeout, OnStop;
	public float Time { get; set; }

	TimerConfig _Config;
	bool isPaused, isDisposed;

	internal Timer(TimerConfig Config=null) {
		this.Config = Config ?? new();

		if(isDisposed = !validateConfig(_Config)) return;

		if(_Config.AutoStart) Time = _Config.MaxTime;

		TimerManager.RegisterTimer(this);
	}

	bool validateConfig(TimerConfig Config) {
		if(Config.MaxTime < 0f) {
			GD.PushError($"{GENERIC_ERROR_TIMER}:\nMaxTime is negative");
			return false;
		}

		if(Config.TickFrequency < 0f) {
			GD.PushError($"{GENERIC_ERROR_TIMER}:\nTickFrequency is negative");
			return false;
		}

		return true;
	}

	~Timer() => Dispose();

	internal void Tick(float dt) {
		if(isDisposed || isPaused || Time <= 0f
		|| (!_Config.TickOnPause && GameBridge.Instance?.Tree?.Paused == true)
		|| (!_Config.TickOnZeroTimeScale && Engine.TimeScale == 0f))
			return;

		Time -= dt;
		OnTick?.Invoke();

		if(Time <= 0f) {
			OnTimeout?.Invoke();

			if(_Config.AutoRefresh) Time += _Config.MaxTime;
			if(_Config.DisposeOnTimeout) Dispose();
		}
	}

	public void Start(float? maxTime=null) {
		if(isDisposed) return;

		OnStart?.Invoke();

		if(maxTime == 0f) {
			OnTimeout?.Invoke();
			if (_Config.DisposeOnTimeout) Dispose();
			return;
		}

		Config.MaxTime = Time = maxTime ?? _Config.MaxTime;

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

		tickSourceTimer?.Dispose();
		tickSourceTimer = null;

		TimerManager.UnregisterTimer(this);

		OnStart = OnTick = OnTimeout = OnStop = null;
		GC.SuppressFinalize(this);
	}

	public bool IsGoing
		=> !isPaused && Time > 0f;

	public bool HasFinished
		=> !isPaused && Time <= 0f;
}
