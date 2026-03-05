using System;

using Godot;

public class Timer : IDisposable {
	const string GENERIC_ERROR_TIMER = "Failed to create `Timer`";

	internal Timer tickSourceTimer;

	public TimerConfig Config {
		get;
		set {
            field?.Changed -= OnConfigChanged;
			field = value ?? new();

			if(isDisposed || !handleConfig(field)) return;
			field?.Changed += OnConfigChanged;
		}
	}
	public FastEvent OnStart = new(),
					 OnTick = new(),
					 OnTimeout = new(),
					 OnStop = new();
	public float Time { get; set; }

	bool isPaused, isDisposed;

	TickRate prevTickRate;
	float prevTickFrequency;

	void OnConfigChanged() {
    	if(!isDisposed) 
        	handleConfig(Config);
	}

	internal Timer(TimerConfig Config=null) {
		this.Config = Config ?? new();

		if(isDisposed = !handleConfig(this.Config, true)) return;

		if(this.Config.AutoStart) Time = this.Config.MaxTime;
	}

	bool handleConfig(TimerConfig Config, bool firstRun=false) {
		if(Config.MaxTime < 0f) {
			GD.PushError($"{GENERIC_ERROR_TIMER}:\nMaxTime is negative");
			return false;
		}

		if(Config.TickFrequency < 0f) {
			GD.PushError($"{GENERIC_ERROR_TIMER}:\nTickFrequency is negative");
			return false;
		}

		if(firstRun)
			TimerManager.RegisterTimer(this);
		else if(prevTickRate != Config.TickRate
		|| prevTickFrequency != Config.TickFrequency)
			TimerManager.ReRegisterTimer(this);
		
		prevTickRate = Config.TickRate;
		prevTickFrequency = Config.TickFrequency;

		return true;
	}

	internal void Tick(float dt) {
		if(isDisposed)
			return;

		if(Config.AlwaysTick) {
			OnTick?.Invoke();
			return;
		}

		if(isPaused || Time <= 0f
		|| (!Config.TickOnPause && GameBridge.Instance?.Tree?.Paused == true)
		|| (!Config.TickOnZeroTimeScale && Engine.TimeScale == 0f))
			return;

		Time -= dt;

		if(Time <= 0f) {
			OnTimeout?.Invoke();

			if(Config.AutoRefresh) Time += Config.MaxTime;
			if(Config.DisposeOnTimeout) Dispose();
		} else
			OnTick?.Invoke();
	}

	public void Start(float? maxTime=null) {
		if(isDisposed) return;

		OnStart?.Invoke();

		if(maxTime == 0f) {
			// we don't account for `Config.AutoStart` because it'd just be an infinite recursive loop
			OnTimeout?.Invoke();
			if(Config.DisposeOnTimeout) Dispose();
			return;
		}

		Config.MaxTime = Time = maxTime ?? Config.MaxTime;

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
	}

	public void Dispose() {
		if(isDisposed) return;
		isDisposed = true;

		Config.Changed -= OnConfigChanged;

		tickSourceTimer?.Dispose();
		tickSourceTimer = null;

		TimerManager.UnregisterTimer(this);

		OnStart = new();
		OnTick = new();
		OnTimeout = new();
		OnStop = new();
		GC.SuppressFinalize(this);
	}

	public bool IsGoing
		=> !isPaused && Time > 0f;

	public bool HasFinished
		=> !isPaused && Time <= 0f;

	public float PercentageDone
		=> Config.MaxTime == 0f ? 0f : HasFinished ? 1f : 1f - Time / Config.MaxTime;
}
