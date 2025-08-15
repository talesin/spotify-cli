namespace SpotifyCLI.Services

open System
open System.Collections.Concurrent
open SpotifyCLI.Domain

/// <summary>
/// Cache entry with TTL (Time To Live) support.
/// Stores cached values with expiration timestamps.
/// </summary>
[<Struct>]
type CacheEntry<'T> = {
    /// The cached value
    Value: 'T
    /// When this cache entry expires
    ExpiresAt: DateTime
}

/// <summary>
/// Cache service interface for storing and retrieving cached values.
/// Provides TTL-based caching with automatic expiration.
/// </summary>
type ICacheService =
    /// <summary>Get cached value if it exists and hasn't expired</summary>
    /// <param name="key">Cache key to look up</param>
    /// <returns>Some value if found and valid, None if missing or expired</returns>
    abstract Get<'T> : key: string -> 'T option
    
    /// <summary>Store value in cache with TTL</summary>
    /// <param name="key">Cache key to store under</param>
    /// <param name="value">Value to cache</param>
    /// <param name="ttl">Time to live (how long to keep cached)</param>
    abstract Set<'T> : key: string * value: 'T * ttl: TimeSpan -> unit
    
    /// <summary>Remove value from cache</summary>
    /// <param name="key">Cache key to remove</param>
    abstract Remove : key: string -> unit
    
    /// <summary>Clear all cached values</summary>
    abstract Clear : unit -> unit
    
    /// <summary>Clean up expired entries</summary>
    abstract CleanupExpired : unit -> unit

/// <summary>
/// In-memory cache service implementation using concurrent dictionary.
/// Thread-safe and suitable for single-process caching needs.
/// </summary>
/// <example>
/// let cache = InMemoryCacheService()
/// cache.Set("user-profile", userProfile, TimeSpan.FromMinutes(5.0))
/// match cache.Get<UserProfile>("user-profile") with
/// | Some profile -> (* use cached profile *)
/// | None -> (* fetch fresh data *)
/// </example>
type InMemoryCacheService() =
    
    let cache = ConcurrentDictionary<string, obj * DateTime>()
    
    /// Check if a cache entry is still valid (not expired)
    let isValidEntry (expiresAt: DateTime) : bool =
        DateTime.UtcNow < expiresAt
    
    /// Remove expired entries from the cache
    let cleanupExpiredEntries () : unit =
        let now = DateTime.UtcNow
        let expiredKeys = 
            cache
            |> Seq.filter (fun kvp -> snd kvp.Value <= now)
            |> Seq.map (fun kvp -> kvp.Key)
            |> List.ofSeq
        
        expiredKeys |> List.iter (fun key -> cache.TryRemove(key) |> ignore)
    
    interface ICacheService with
        
        member _.Get<'T>(key: string) : 'T option =
            match cache.TryGetValue(key) with
            | true, (value, expiresAt) when isValidEntry expiresAt ->
                try
                    Some (value :?> 'T)
                with
                | :? InvalidCastException -> 
                    // Type mismatch - remove invalid entry
                    cache.TryRemove(key) |> ignore
                    None
            | true, _ -> 
                // Expired entry - remove it
                cache.TryRemove(key) |> ignore
                None
            | false, _ -> None
        
        member _.Set<'T>(key: string, value: 'T, ttl: TimeSpan) : unit =
            let expiresAt = DateTime.UtcNow.Add(ttl)
            cache.AddOrUpdate(key, (box value, expiresAt), fun _ _ -> (box value, expiresAt)) |> ignore
        
        member _.Remove(key: string) : unit =
            cache.TryRemove(key) |> ignore
        
        member _.Clear() : unit =
            cache.Clear()
        
        member _.CleanupExpired() : unit =
            cleanupExpiredEntries()

/// <summary>
/// Cache service specifically designed for HTTP API responses.
/// Provides domain-specific caching methods with appropriate TTL defaults.
/// </summary>
type ApiCacheService(cacheService: ICacheService) =
    
    /// Default TTL for user profile data (5 minutes)
    let userProfileTtl = TimeSpan.FromMinutes(5.0)
    
    /// Default TTL for playlist data (5 minutes) 
    let playlistsTtl = TimeSpan.FromMinutes(5.0)
    
    /// Default TTL for tokens (cache briefly to avoid repeated validation)
    let tokenValidationTtl = TimeSpan.FromMinutes(1.0)
    
    /// <summary>Cache user profile data</summary>
    /// <param name="userId">User ID to cache under</param>
    /// <param name="profile">User profile to cache</param>
    member _.CacheUserProfile(userId: string, profile: UserProfile) : unit =
        let key = $"user-profile:{userId}"
        cacheService.Set(key, profile, userProfileTtl)
    
    /// <summary>Get cached user profile data</summary>
    /// <param name="userId">User ID to look up</param>
    /// <returns>Cached user profile if available and valid</returns>
    member _.GetCachedUserProfile(userId: string) : UserProfile option =
        let key = $"user-profile:{userId}"
        cacheService.Get<UserProfile>(key)
    
    /// <summary>Cache playlist data for a user</summary>
    /// <param name="userId">User ID to cache under</param>
    /// <param name="playlists">List of playlists to cache</param>
    member _.CacheUserPlaylists(userId: string, playlists: PlaylistInfo list) : unit =
        let key = $"user-playlists:{userId}"
        cacheService.Set(key, playlists, playlistsTtl)
    
    /// <summary>Get cached playlist data for a user</summary>
    /// <param name="userId">User ID to look up</param>
    /// <returns>Cached playlists if available and valid</returns>
    member _.GetCachedUserPlaylists(userId: string) : PlaylistInfo list option =
        let key = $"user-playlists:{userId}"
        cacheService.Get<PlaylistInfo list>(key)
    
    /// <summary>Cache token validation result</summary>
    /// <param name="tokenHash">Hash of the token (for security)</param>
    /// <param name="isValid">Whether the token is valid</param>
    member _.CacheTokenValidation(tokenHash: string, isValid: bool) : unit =
        let key = $"token-validation:{tokenHash}"
        cacheService.Set(key, isValid, tokenValidationTtl)
    
    /// <summary>Get cached token validation result</summary>
    /// <param name="tokenHash">Hash of the token to look up</param>
    /// <returns>Cached validation result if available</returns>
    member _.GetCachedTokenValidation(tokenHash: string) : bool option =
        let key = $"token-validation:{tokenHash}"
        cacheService.Get<bool>(key)
    
    /// <summary>Invalidate all cached data for a user</summary>
    /// <param name="userId">User ID to invalidate cache for</param>
    member _.InvalidateUserCache(userId: string) : unit =
        cacheService.Remove($"user-profile:{userId}")
        cacheService.Remove($"user-playlists:{userId}")
    
    /// <summary>Clean up expired cache entries</summary>
    member _.CleanupExpired() : unit =
        cacheService.CleanupExpired()

/// <summary>
/// Factory functions for creating cache services.
/// Provides convenient creation methods with sensible defaults.
/// </summary>
module CacheService =
    
    /// <summary>Create a new in-memory cache service</summary>
    /// <returns>New ICacheService instance</returns>
    let createInMemory () : ICacheService =
        InMemoryCacheService() :> ICacheService
    
    /// <summary>Create an API-specific cache service</summary>
    /// <param name="cacheService">Underlying cache service to use</param>
    /// <returns>New ApiCacheService instance</returns>
    let createApiCache (cacheService: ICacheService) : ApiCacheService =
        ApiCacheService(cacheService)
    
    /// <summary>Create a complete API cache with in-memory backend</summary>
    /// <returns>New ApiCacheService with in-memory storage</returns>
    let createDefault () : ApiCacheService =
        let cache = createInMemory()
        createApiCache cache