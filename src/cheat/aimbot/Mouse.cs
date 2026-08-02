using Osussist.src.config;
using Osussist.src.osu;
using Osussist.src.osu.helpers;
using Osussist.src.utils;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Osussist.src.cheat.aimbot
{
	public class Mouse
	{
		#region NativeImports
		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
		public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

		public const int MOUSEEVENTF_MOVE = 0x0001;
		public const int MOUSEEVENTF_LEFTDOWN = 0x0002;
		public const int MOUSEEVENTF_LEFTUP = 0x0004;
		public const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
		public const int MOUSEEVENTF_RIGHTUP = 0x0010;
		public const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
		public const int MOUSEEVENTF_MIDDLEUP = 0x0040;
		public const int MOUSEEVENTF_ABSOLUTE = 0x8000;
		#endregion

		private static Logger logger = Logger.LoggingInstance;
		private static OsuSDK SDK { get; set; }
		private readonly SliderCursorTracker sliderCursorTracker = new SliderCursorTracker();

		public Mouse(OsuSDK givenSDK)
		{
			SDK = givenSDK;
		}

		public bool isGoodForMovement
		{
			get
			{
				if (Logic.isHoldingKeys)
				{
					logger.Debug("Aimbot.Mouse", "Holding keys, stopping movement.");
					return false;
				}
				else if (!Logic.isAimbotEnabled)
				{
					logger.Debug("Aimbot.Mouse", "Aimbot disabled, stopping movement.");
					return false;
				}
				else if (!SDK.isPlaying || SDK.isPaused || !SDK.isGameFocused)
				{
					logger.Debug("Aimbot.Mouse", "Not playing, paused or game not in focus, stopping movement.");
					return false;
				}
				return true;
			}
		}

		private Vector2 ApplySensitivity(Vector2 targetPosition)
		{
			return ApplySensitivity(targetPosition, SDK.GetRealMousePosition());
		}

		private Vector2 ApplySensitivity(Vector2 targetPosition, Vector2 currentPosition)
		{
			if (Config.config.osusettings.sensitivity == 1f)
				return targetPosition;

			Vector2 movement = targetPosition - currentPosition;
			float movementScalingFactor = 1f / Config.config.osusettings.sensitivity;
			float baseScaling = 0.5f;
			movement *= baseScaling * movementScalingFactor;
			Vector2 newTargetPosition = currentPosition + movement;

			return newTargetPosition;
		}

		public void BeginSliderTracking(Vector2 initialPosition)
		{
			sliderCursorTracker.Reset(initialPosition);
		}

		public void BeginCircleTracking(Vector2 initialPosition)
		{
			sliderCursorTracker.Reset(initialPosition);
		}

		public void BeginSpinnerTracking(Vector2 initialPosition)
		{
			sliderCursorTracker.Reset(initialPosition);
		}

		public void TrackSliderPoint(Vector2 destinationCoords)
		{
			destinationCoords = ApplySensitivity(destinationCoords, sliderCursorTracker.CurrentPosition);
			MoveTrackedCursor(destinationCoords);
		}

		public void TrackCirclePoint(Vector2 destinationCoords)
		{
			destinationCoords = ApplySensitivity(destinationCoords, sliderCursorTracker.CurrentPosition);
			MoveTrackedCursor(destinationCoords);
		}

		public void TrackSpinnerPoint(Vector2 destinationCoords)
		{
			MoveTrackedCursor(destinationCoords);
		}

		private void MoveTrackedCursor(Vector2 destinationCoords)
		{
			Vector2 delta = sliderCursorTracker.MoveTowards(destinationCoords);
			if (delta.X != 0f || delta.Y != 0f)
				mouse_event(MOUSEEVENTF_MOVE, unchecked((uint)(int)delta.X), unchecked((uint)(int)delta.Y), 0, 0);
		}

		public void MoveLinear(Vector2 destinationCoords)
		{
			Vector2 currentPosition = SDK.GetRealMousePosition();
			destinationCoords = ApplySensitivity(destinationCoords);

			if (!isGoodForMovement)
				return;

			Vector2 movementDiff = LinearMovement.CalculateDelta(currentPosition, destinationCoords, Config.config.aimbotsettings.strength);
			int x = (int)System.Math.Truncate(movementDiff.X);
			int y = (int)System.Math.Truncate(movementDiff.Y);
			if (x == 0 && y == 0)
				return;

			logger.Debug("Aimbot.Mouse", $"Moving linearly by {x}, {y}.");
			mouse_event(MOUSEEVENTF_MOVE, unchecked((uint)x), unchecked((uint)y), 0, 0);
		}
	}
}
