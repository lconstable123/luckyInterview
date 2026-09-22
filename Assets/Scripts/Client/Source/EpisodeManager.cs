using System;
using Hazel;

namespace LuckyInterview
{
	public enum EpisodeState
	{
		Idle,
		Running,
		Completed,
		TimedOut
	}
    // note: AS THIS IS A ASSESSMENT, I NOTE THAT THIS PARTICULAR CLASS WAS MADE BY AI

	// Reusable start/timeout/complete/restart lifecycle for episode-driven Take_* scripts.
	// Owner supplies the episode body (playEpisode) and how to interrupt in-flight motion (onInterrupt).
	public class EpisodeManager
	{
		public EpisodeState State { get; private set; } = EpisodeState.Idle;

		private readonly Action _playEpisode;
		private readonly Action? _onInterrupt;
		private readonly float _timeoutSeconds;
		private readonly bool _restartOnCompletion;
		private Timer.Handle _timeoutHandle;

		public EpisodeManager(Action playEpisode, float timeoutSeconds, bool restartOnCompletion, Action? onInterrupt = null)
		{
			_playEpisode = playEpisode;
			_timeoutSeconds = timeoutSeconds;
			_restartOnCompletion = restartOnCompletion;
			_onInterrupt = onInterrupt;
		}

		public void Start()
		{
			if (State == EpisodeState.Running)
			{
				Log.Info("Episode already running, restarting.");
				_onInterrupt?.Invoke();
			}
            
			Timer.Clear(_timeoutHandle);
			State = EpisodeState.Running;
			_timeoutHandle = Timer.Set(OnTimeout, _timeoutSeconds, looping: false);
			Log.Info("Episode started.");
			_playEpisode();
		}

		public void Complete()
		{
			if (State != EpisodeState.Running) return;
			Log.Info("Episode completed successfully.");
			State = EpisodeState.Completed;
			Timer.Clear(_timeoutHandle);
			if (_restartOnCompletion) Start();
		}

		private void OnTimeout()
		{
			if (State != EpisodeState.Running) return;
			Log.Error($"Episode timed out after {_timeoutSeconds}s.");
			State = EpisodeState.TimedOut;
			_onInterrupt?.Invoke();
			if (_restartOnCompletion) Start();
		}

		// Cancel the pending timeout - call from the owner's OnDestroy.
		public void Shutdown()
		{
			Timer.Clear(_timeoutHandle);
		}
	}
}
