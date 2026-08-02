using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;
using Osussist.src.config;
using Osussist.src.config.objects;
using Osussist.src.osu;
using Osussist.src.osu.helpers;
using Osussist.src.utils;
using System.Numerics;

namespace Osussist.src.cheat.aimbot
{
	public class Stable
	{
		private Logger logger { get; set; }
		private OsuSDK SDK { get; set; }
		private Mouse Mouse { get; set; }

		private int HitWin50;
		private int HitWin300;
		private int SongIndex;
		private int LastHitTime;
		private Beatmap CurrentBeatmap;
		private int LastBeatmapId = -99;
		private HitObject CurrentHitObject;
		private Vector2 LastOnNotePos = Vector2.Zero;
		private const int FastCircleIntervalMs = 250;
		private enum TrackingState
		{
			Active,
			Retry,
			Stop
		}

		public Stable(OsuSDK givenSDK)
		{
			SDK = givenSDK;
			Mouse = new Mouse(SDK);
			logger = Logger.LoggingInstance;
		}

		private int ClosestHitObjectIndex
		{
			get
			{
				int currentTime = SDK.CurrentTime;
				for (int i = 0; i < CurrentBeatmap.HitObjects.Count; i++)
				{
					if (CurrentBeatmap.HitObjects[i].StartTime >= currentTime)
					{
						return i;
					}
				}
				return CurrentBeatmap.HitObjects.Count;
			}
		}

		public void Loop()
		{
			while (SDK.isGameFocused && !SDK.isPaused)
			{
				if (SDK.isPlaying)
				{
					CurrentBeatmap = Relax.CurrentBeatmap;
					if (CurrentBeatmap == null)
					{
						logger.Info("Aimbot.Stable", "No beatmap loaded, stopping aimbot");
						ResetLoop();
					}
					else if (LastBeatmapId != CurrentBeatmap.MetadataSection.BeatmapID)
					{
						LastBeatmapId = CurrentBeatmap.MetadataSection.BeatmapID;
						logger.Info("Aimbot.Stable", $"Loaded beatmap: {CurrentBeatmap.MetadataSection.Artist} - {CurrentBeatmap.MetadataSection.Title} [{CurrentBeatmap.MetadataSection.Version}]");
					}
					HitWin50 = SDK.HitWindow50(CurrentBeatmap.DifficultySection.OverallDifficulty);
					HitWin300 = SDK.HitWindow300(CurrentBeatmap.DifficultySection.OverallDifficulty);

					ResetLoop();

					while ((SDK.isGameFocused && !SDK.isPaused) && SongIndex < CurrentBeatmap.HitObjects.Count)
					{
						if (!SDK.isPlaying)
						{
							logger.Info("Aimbot.Stable", "Game is not playing, stopping aimbot");
							ResetLoop();
						}
						else if (SDK.isPaused || !SDK.isGameFocused)
						{
							logger.Info("Aimbot.Stable", "Game is paused or not focused, stopping aimbot");
							ResetLoop();
						}
						else
						{
							int possibleTime = SDK.CurrentTime + Config.config.osusettings.audiooffset;
							if (possibleTime < LastHitTime)
							{
								logger.Debug("Aimbot.Stable", "Detected time travel, resetting aimbot");
								ResetLoop();
							}
							else
							{
								if (CurrentHitObject != null && SDK != null)
								{
									if (possibleTime >= CurrentHitObject.StartTime - HitWin50)
									{
										if (CurrentHitObject is Slider slider)
										{
											if (FollowSlider(slider, possibleTime))
												NextObject();
											else
												RecoverFromTrackingFailure(possibleTime);
											continue;
										}
										if (CurrentHitObject is Spinner spinner)
										{
											if (FollowSpinner(spinner))
												NextObject();
											else
												RecoverFromTrackingFailure(possibleTime);
											continue;
										}
										if (CurrentHitObject is HitCircle circle && ShouldUseFastCircleTracker())
										{
											if (FollowCircle(circle))
												AdvanceToNextViableObject(GetCurrentTargetTime());
											else
												RecoverFromTrackingFailure(possibleTime);
											continue;
										}

										if (possibleTime <= CurrentHitObject.StartTime + HitWin300)
										{
											Vector2 hitObject;
											if (Config.config.aimbotsettings.hardrockenabled)
												hitObject = SDK.GetHRHitObjectPos(CurrentHitObject);
											else
												hitObject = SDK.GetRealHitObjectPos(CurrentHitObject);

											logger.Debug("Aimbot.Stable", $"Hitobject found at {hitObject.X}, {hitObject.Y}");
											if (!Logic.isHoldingKeys && Logic.isAimbotEnabled && SDK.GetRealMousePosition().LengthRelativeTo(hitObject) <= Config.config.aimbotsettings.fovsize)
											{
												LastOnNotePos = hitObject;
												PerformMove(hitObject);
											}
										}
										else
										{
											logger.Debug("Aimbot.Stable", "Missed hitobject, going to next object");
											AdvanceToNextViableObject(possibleTime);
										}
									}
								}
								else
								{
									logger.Debug("Aimbot.Stable", "No hitobject found, resetting aimbot");
									ResetLoop();
								}
							}
						}
					}
					while (!SDK.isPaused || !SDK.isGameFocused)
					{
						Thread.Sleep(5);
					}
				}
			}
		}

		private void ResetLoop()
		{
			try
			{
				SongIndex = ClosestHitObjectIndex;
				CurrentHitObject = CurrentBeatmap.HitObjects[SongIndex];
				LastHitTime = -CurrentBeatmap.GeneralSection.AudioLeadIn;
			}
			catch (Exception e)
			{
				logger.Error("Aimbot.Stable", $"Failed to reset loop: {e.Message}");
			}
		}

		private void NextObject()
		{
			int tempIndex = SongIndex;
			SongIndex = tempIndex + 1;
			if (SongIndex < CurrentBeatmap.HitObjects.Count)
			{
				CurrentHitObject = CurrentBeatmap.HitObjects[SongIndex];
			}
		}

		private bool ShouldUseFastCircleTracker()
		{
			if (SongIndex + 1 >= CurrentBeatmap.HitObjects.Count)
				return false;

			int interval = CurrentBeatmap.HitObjects[SongIndex + 1].StartTime - CurrentHitObject.StartTime;
			return interval <= FastCircleIntervalMs;
		}

		private bool FollowCircle(HitCircle circle)
		{
			Vector2 targetPosition = GetPlayfieldPosition(circle.Position);
			Vector2 initialCursorPosition = SDK.GetRealMousePosition();
			if (initialCursorPosition.LengthRelativeTo(targetPosition) > Config.config.aimbotsettings.fovsize)
				return false;

			Mouse.BeginCircleTracking(initialCursorPosition);
			int failedPlaybackReads = 0;
			while (true)
			{
				TrackingState trackingState = GetTrackingState(ref failedPlaybackReads, "Fast circle tracking", out int circleTime);
				if (trackingState == TrackingState.Stop)
					return false;
				if (trackingState == TrackingState.Retry)
				{
					Thread.Sleep(1);
					continue;
				}

				if (circleTime > circle.StartTime + HitWin300)
					return true;

				Mouse.TrackCirclePoint(targetPosition);
				Thread.Sleep(1);
			}
		}

		private int GetCurrentTargetTime()
		{
			return SDK.CurrentTime + Config.config.osusettings.audiooffset;
		}

		private void AdvanceToNextViableObject(int currentTime)
		{
			SongIndex++;
			while (SongIndex < CurrentBeatmap.HitObjects.Count
				&& CurrentBeatmap.HitObjects[SongIndex].EndTime + HitWin300 < currentTime)
			{
				SongIndex++;
			}

			if (SongIndex < CurrentBeatmap.HitObjects.Count)
				CurrentHitObject = CurrentBeatmap.HitObjects[SongIndex];

			LastHitTime = currentTime;
		}

		private void RecoverFromTrackingFailure(int currentTime)
		{
			logger.Debug("Aimbot.Stable", "Tracking failed, resynchronizing to the next viable object.");
			AdvanceToNextViableObject(currentTime);
		}

		private bool FollowSlider(Slider slider, int initialSliderTime)
		{
			var sliderPath = new SliderPathEvaluator(slider);
			Vector2 initialCursorPosition = SDK.GetRealMousePosition();
			Vector2 sliderHead = GetPlayfieldPosition(sliderPath.PositionAtTime(initialSliderTime));
			if (initialCursorPosition.LengthRelativeTo(sliderHead) > Config.config.aimbotsettings.fovsize)
			{
				logger.Debug("Aimbot.Stable", "Slider head is outside the configured FOV.");
				return false;
			}

			Mouse.BeginSliderTracking(initialCursorPosition);
			int failedPlaybackReads = 0;
			while (true)
			{
				TrackingState trackingState = GetTrackingState(ref failedPlaybackReads, "Slider tracking", out int sliderTime);
				if (trackingState == TrackingState.Stop)
					return false;
				if (trackingState == TrackingState.Retry)
				{
					Thread.Sleep(1);
					continue;
				}

				if (sliderTime > slider.EndTime)
				{
					logger.Debug("Aimbot.Stable", "Slider tracking completed at the tail.");
					return true;
				}

				Mouse.TrackSliderPoint(GetPlayfieldPosition(sliderPath.PositionAtTime(sliderTime)));

				Thread.Sleep(1);
			}
		}

		private bool FollowSpinner(Spinner spinner)
		{
			Mouse.BeginSpinnerTracking(SDK.GetRealMousePosition());
			int failedPlaybackReads = 0;
			while (true)
			{
				TrackingState trackingState = GetTrackingState(ref failedPlaybackReads, "Spinner tracking", out int spinnerTime);
				if (trackingState == TrackingState.Stop)
					return false;
				if (trackingState == TrackingState.Retry)
				{
					Thread.Sleep(1);
					continue;
				}

				if (spinnerTime > spinner.EndTime)
				{
					logger.Debug("Aimbot.Stable", "Spinner tracking completed at the tail.");
					return true;
				}

				Mouse.TrackSpinnerPoint(GetSpinnerPosition(spinner, spinnerTime));
				Thread.Sleep(1);
			}
		}

		private TrackingState GetTrackingState(ref int failedPlaybackReads, string trackerName, out int currentTime)
		{
			currentTime = 0;
			if (!Logic.isAimbotEnabled)
			{
				logger.Debug("Aimbot.Stable", $"{trackerName} stopped because aim assist was disabled.");
				return TrackingState.Stop;
			}
			if (!SDK.isGameFocused)
			{
				logger.Debug("Aimbot.Stable", $"{trackerName} stopped because osu! lost focus.");
				return TrackingState.Stop;
			}
			if (!SDK.isPlaying)
			{
				logger.Debug("Aimbot.Stable", $"{trackerName} stopped because osu! is not playing.");
				return TrackingState.Stop;
			}
			if (!SDK.TryGetPlaybackState(out currentTime, out bool isPaused))
			{
				failedPlaybackReads++;
				if (failedPlaybackReads >= 3)
				{
					logger.Debug("Aimbot.Stable", $"{trackerName} stopped after repeated IPC playback failures.");
					return TrackingState.Stop;
				}

				return TrackingState.Retry;
			}

			failedPlaybackReads = 0;
			if (isPaused)
			{
				logger.Debug("Aimbot.Stable", $"{trackerName} stopped because osu! is paused.");
				return TrackingState.Stop;
			}

			currentTime += Config.config.osusettings.audiooffset;
			return TrackingState.Active;
		}

		private Vector2 GetSpinnerPosition(Spinner spinner, int currentTime)
		{
			float radius = SDK.OsuManager.WindowManager.PlayfieldSize.Y * 0.25f;
			Vector2 center = SDK.GetRealPlayfieldPosition(new Vector2(256f, 192f));
			return SpinnerPath.PositionAt(center, radius, spinner.StartTime, currentTime);
		}

		private Vector2 GetPlayfieldPosition(Vector2 position)
		{
			return Config.config.aimbotsettings.hardrockenabled
				? SDK.GetHRPlayfieldPosition(position)
				: SDK.GetRealPlayfieldPosition(position);
		}

        private void PerformMove(Vector2 hitObject)
        {
            Mouse.MoveLinear(hitObject);
        }
    }
}
