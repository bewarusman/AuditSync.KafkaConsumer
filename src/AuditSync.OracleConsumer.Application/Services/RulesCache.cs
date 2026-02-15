using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AuditSync.OracleConsumer.Application.Services;

/// <summary>
/// Caches rules from database with automatic refresh every 24 hours.
/// Thread-safe for concurrent access.
/// </summary>
public class RulesCache
{
    private List<Rule>? _cachedRules;
    private DateTime _lastRefresh = DateTime.MinValue;
    private readonly TimeSpan _refreshInterval;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IRulesEngineRepository _rulesRepository;
    private readonly string _targetName;
    private readonly ILogger<RulesCache> _logger;

    public RulesCache(
        IRulesEngineRepository rulesRepository,
        string targetName,
        ILogger<RulesCache> logger,
        int refreshHours = 24)
    {
        _rulesRepository = rulesRepository;
        _targetName = targetName;
        _logger = logger;
        _refreshInterval = TimeSpan.FromHours(refreshHours);
    }

    /// <summary>
    /// Gets cached rules, refreshing if needed.
    /// Thread-safe and uses double-check locking pattern.
    /// </summary>
    public async Task<List<Rule>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        if (ShouldRefresh())
        {
            await RefreshRulesAsync(cancellationToken);
        }

        return _cachedRules ?? new List<Rule>();
    }

    /// <summary>
    /// Forces an immediate refresh of the rules cache.
    /// </summary>
    public async Task ForceRefreshAsync(CancellationToken cancellationToken = default)
    {
        await RefreshRulesAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if cache should be refreshed.
    /// </summary>
    private bool ShouldRefresh()
    {
        return _cachedRules == null ||
               DateTime.UtcNow - _lastRefresh > _refreshInterval;
    }

    /// <summary>
    /// Refreshes the rules cache from database.
    /// Uses double-check locking to prevent redundant refreshes.
    /// </summary>
    private async Task RefreshRulesAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check: another thread might have refreshed while we waited
            if (!ShouldRefresh())
            {
                _logger.LogDebug("Rules cache was refreshed by another thread");
                return;
            }

            _logger.LogInformation("Refreshing rules cache for target: {Target}", _targetName);

            var rules = await _rulesRepository.GetRulesByTargetNameAsync(_targetName, cancellationToken);

            // Sort by ORDER_POSITION to ensure correct evaluation order
            _cachedRules = rules.OrderBy(r => r.OrderPosition).ToList();
            _lastRefresh = DateTime.UtcNow;

            _logger.LogInformation(
                "Cached {Count} rules for target {Target}. Next refresh at {NextRefresh}",
                _cachedRules.Count,
                _targetName,
                _lastRefresh.Add(_refreshInterval));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing rules cache for target {Target}. Using stale cache if available.", _targetName);

            // Keep using stale cache rather than throwing exception
            // This provides resilience in case of temporary database issues
            if (_cachedRules != null)
            {
                _logger.LogWarning("Continuing with stale cache ({Count} rules from {LastRefresh})",
                    _cachedRules.Count, _lastRefresh);
            }
            else
            {
                // If no cache exists at all, we need to throw
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets cache statistics for monitoring/debugging.
    /// </summary>
    public (int RuleCount, DateTime LastRefresh, DateTime NextRefresh) GetCacheStats()
    {
        return (
            _cachedRules?.Count ?? 0,
            _lastRefresh,
            _lastRefresh.Add(_refreshInterval)
        );
    }
}
