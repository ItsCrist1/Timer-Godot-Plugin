using System;
using System.Collections;
using System.Collections.Generic;

using Godot;

public partial class TimerManager : Node {
	public static TimerManager Instance { get; private set; }

	static List<Timer> PendingTimers = new();
	List<Timer> Timers = new();
	
	public override void _EnterTree() {
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		
		Timers.AddRange(PendingTimers);
		PendingTimers.Clear();
	}

	public override void _Process(double delta) {
		float dt = (float)delta;

		for(int i=Timers.Count-1; i >= 0; --i)
			Timers[i].Tick(dt);
	}

	public override void _ExitTree() {
		foreach(Timer timer in Timers)
			timer?.Dispose();

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

	public class Timer : IDisposable {
		const string GENERIC_ERROR = "Failed to create `Timer`";

		TimerConfig Config;
		public event Action OnStart, OnTick, Timeout, OnStop;
		public float Time { private get; set; }

		bool isPaused, isDisposed;

		internal Timer (TimerConfig Config = default) {
			this.Config = Config;

			if(Config.MaxTime < 0f) {
				GD.PrintErr($"{GENERIC_ERROR}:\nMaxTime is negative");

				isDisposed = true;
				return;
			}
			
			if(Config.AutoStart) Time = Config.MaxTime;

			if(Instance == null) PendingTimers.Add(this);
			else Instance.Timers.Add(this);
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
				if(Config.AutoRefresh) Time = Config.MaxTime;
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
			OnStart = OnTick = Timeout = OnStop = null;

			if(Instance != null) Instance.Timers.Remove(this);
			else PendingTimers.Remove(this);

			GC.SuppressFinalize(this);
		}
	}
}
