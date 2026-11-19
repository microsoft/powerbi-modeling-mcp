using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PowerBIModelingMCP.Library.Common;

public class RetryHelper
{
	private static volatile RetryHelper _default = new RetryHelper(NullLogger<RetryHelper>.Instance);

	private readonly ILogger _logger;

	public static RetryHelper Default => _default;

	public static void ConfigureDefault(RetryHelper retryHelper)
	{
		ArgumentNullException.ThrowIfNull(retryHelper, "retryHelper");
		_default = retryHelper;
	}

	public RetryHelper(ILogger logger)
	{
		_logger = logger ?? throw new ArgumentNullException("logger");
	}

	public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, RetryOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(operation, "operation");
		if (options == null)
		{
			options = new RetryOptions();
		}
		int totalAttempts = options.MaxRetryAttempts + 1;
		TimeSpan currentDelay = options.InitialDelay;
		for (int attempt = 1; attempt <= totalAttempts; attempt++)
		{
			bool succeeded = false;
			try
			{
				Task task = Task.Delay(options.Timeout, cancellationToken);
				Task<T> operationTask = operation();
				if (await Task.WhenAny(operationTask, task) == operationTask)
				{
					succeeded = true;
					return await operationTask;
				}
				_logger.LogWarning("RetryHelper: operation timed out");
				throw new TimeoutException("The operation timed out.");
			}
			catch (Exception arg) when (attempt < totalAttempts)
			{
				if (options.ShouldRetry != null && !options.ShouldRetry(arg))
				{
					throw;
				}
				_logger.LogWarning($"{"RetryHelper"}: Retrying, number of attempt: {attempt} .");
				cancellationToken.ThrowIfCancellationRequested();
				await Task.Delay(currentDelay, cancellationToken);
				currentDelay = ComputeNextDelay(currentDelay, options);
			}
			finally
			{
				_logger.LogInformation($"{"RetryHelper"}: Attempt {attempt} {(succeeded ? "succeeded" : "failed")}.");
			}
		}
		throw new InvalidOperationException("Unexpected state in retry loop.");
	}

	public async Task ExecuteWithRetryAsync(Func<Task> operation, RetryOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(operation, "operation");
		await ExecuteWithRetryAsync(async delegate
		{
			await operation();
			return true;
		}, options, cancellationToken);
	}

	private static TimeSpan ComputeNextDelay(TimeSpan currentDelay, RetryOptions options)
	{
		long num = (long)((double)currentDelay.Ticks * options.BackoffMultiplier);
		if (num < 0 || num > TimeSpan.MaxValue.Ticks)
		{
			num = TimeSpan.MaxValue.Ticks;
		}
		TimeSpan timeSpan = TimeSpan.FromTicks(num);
		if (options.MaxDelay.HasValue && timeSpan > options.MaxDelay.Value)
		{
			timeSpan = options.MaxDelay.Value;
		}
		return timeSpan;
	}
}
