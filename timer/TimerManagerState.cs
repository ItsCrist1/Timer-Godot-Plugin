public class TimerCount {
	public uint Total => Process + Physics + Arbitrary;
	public uint Process { get; set; }
	public uint Physics { get; set; }
	public uint Arbitrary { get; set; }
	public uint AutoRefreshing { get; set; }
	public uint IsNode { get; set; }
}

public class TimerEventFireCount {
	public uint OnStart { get; set; }
	public uint OnTick { get; set; }
	public uint OnTimeout { get; set; }
	public uint OnStop { get; set; }

	public TimerEventFireCount(uint OnStart=0u, uint OnTick=0u, uint OnTimeout=0u, uint OnStop=0u) {
		this.OnStart = OnStart;
		this.OnTick = OnTick;
		this.OnTimeout = OnTimeout;
		this.OnStop = OnStop;
	}

	public void Reset()
		=> OnStart
		 = OnTick
		 = OnTimeout
		 = OnStop
		 = 0u;

	public static TimerEventFireCount operator +(TimerEventFireCount a, TimerEventFireCount b)
		=> new (
			a.OnStart + b.OnStart,
			a.OnTick + b.OnTick,
			a.OnTimeout + b.OnTimeout,
			a.OnStop + b.OnStop
		);
}

public class TimerManagerState {
	public TimerCount TimerCount { get; set; }
	public TimerEventFireCount TimerEventFireCount { get; set; }

	public TimerManagerState() {
		TimerCount = new();
		TimerEventFireCount = new();
	}
}
