using System;

namespace PowerBIModelingMCP.Library.Common;

public class RetryOptions
{
	private int _maxRetryAttempts = 1;

	private TimeSpan _initialDelay = TimeSpan.FromSeconds(1.0);

	private double _backoffMultiplier = 2.0;

	private const int _timeoutSeconds = 30;

	private TimeSpan? _maxDelay;

	public int MaxRetryAttempts
	{
		get
		{
			return _maxRetryAttempts;
		}
		set
		{
			if (value < 0)
			{
				throw new ArgumentOutOfRangeException("value", "MaxRetryAttempts must be non-negative.");
			}
			_maxRetryAttempts = value;
		}
	}

	public Func<Exception, bool> ShouldRetry { get; set; } = (Exception _) => false;

	public TimeSpan InitialDelay
	{
		get
		{
			return _initialDelay;
		}
		set
		{
			if (value <= TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException("value", "InitialDelay must be greater than zero.");
			}
			_initialDelay = value;
		}
	}

	public TimeSpan? MaxDelay
	{
		get
		{
			return _maxDelay;
		}
		set
		{
			if (value <= TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException("value", "MaxDelay must be greater than zero.");
			}
			_maxDelay = value;
		}
	}

	public double BackoffMultiplier
	{
		get
		{
			return _backoffMultiplier;
		}
		set
		{
			if (value < 1.0)
			{
				throw new ArgumentOutOfRangeException("value", "BackoffMultiplier must be at least 1.0.");
			}
			_backoffMultiplier = value;
		}
	}

	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30.0);
}
