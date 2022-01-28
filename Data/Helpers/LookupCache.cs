﻿using Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Data.Helpers
{
    public class LookupCache<T> : ILookupCache<T> where T : class, IUniqueId
    {
        private readonly ILogger _logger;
        private readonly IDatabaseContextFactory _contextFactory;
        private Dictionary<long, T> _cachedItems;
        private readonly SemaphoreSlim _onOperation = new SemaphoreSlim(1, 1);

        public LookupCache(ILogger<LookupCache<T>> logger, IDatabaseContextFactory contextFactory)
        {
            _logger = logger;
            _contextFactory = contextFactory;
        }

        public async Task<T> AddAsync(T item)
        {
            await _onOperation.WaitAsync();
            T existingItem = null;

            if (_cachedItems.ContainsKey(item.Id))
            {
                existingItem = _cachedItems[item.Id];
            }

            if (existingItem != null)
            {
                _logger.LogDebug("Cached item already added for {type} {id} {value}", typeof(T).Name, item.Id,
                    item.Value);
                _onOperation.Release();
                return existingItem;
            }

            try
            {
                _logger.LogDebug("Adding new {type} with {id} {value}", typeof(T).Name, item.Id, item.Value);
                await using var context = _contextFactory.CreateContext();
                context.Set<T>().Add(item);
                await context.SaveChangesAsync();
                _cachedItems.Add(item.Id, item);
                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not add item to cache for {type}", typeof(T).Name);
                throw new Exception("Could not add item to cache");
            }
            finally
            {
                if (_onOperation.CurrentCount == 0)
                {
                    _onOperation.Release();
                }
            }
        }

        public async Task<T> FirstAsync(Func<T, bool> query)
        {
            await _onOperation.WaitAsync();

            try
            {
                var cachedResult = _cachedItems.Values.Where(query);

                if (cachedResult.Any())
                {
                    return cachedResult.FirstOrDefault();
                }
            }

            catch
            {
            }

            finally
            {
                if (_onOperation.CurrentCount == 0)
                {
                    _onOperation.Release(1);
                }
            }

            return null;
        }

        public IEnumerable<T> GetAll()
        {
            return _cachedItems.Values;
        }

        public async Task InitializeAsync()
        {
            try
            {
                await using var context = _contextFactory.CreateContext(false);
                _cachedItems = await context.Set<T>().ToDictionaryAsync(item => item.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not initialize caching for {cacheType}", typeof(T).Name);
            }
        }
    }
}
