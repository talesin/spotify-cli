namespace SpotifyCLI.Tests.Services

open Expecto
open FsCheck
open System
open SpotifyCLI.Domain
open SpotifyCLI.Services
open SpotifyCLI.Tests.TestUtilities

/// Comprehensive tests for CacheService functionality
module CacheServiceTests =
    
    /// Test helpers and utilities
    module TestHelpers =
        
        /// Create fresh cache service for testing
        let createCacheService () = InMemoryCacheService() :> ICacheService
        
        /// Wait for a specified duration to test TTL expiration
        let waitForExpiration (ms: int) =
            System.Threading.Thread.Sleep(ms)
        
        /// Create test data
        let testString = "test-value"
        let testInt = 42
        let testUserProfile = TestData.sampleUserProfile
        let testPlaylist = TestData.samplePlaylist
        
        /// Short TTL for testing expiration (100ms)
        let shortTtl = TimeSpan.FromMilliseconds(100.0)
        
        /// Long TTL for stable tests (1 hour)
        let longTtl = TimeSpan.FromHours(1.0)
    
    /// Tests for basic cache operations
    module BasicCacheOperationsTests =
        
        [<Tests>]
        let tests = testList "Basic Cache Operations" [
            
            test "should store and retrieve string value" {
                let cache = TestHelpers.createCacheService()
                let key = "test-key"
                let value = TestHelpers.testString
                
                cache.Set(key, value, TestHelpers.longTtl)
                let result = cache.Get<string>(key)
                
                match result with
                | Some retrievedValue -> Expect.equal retrievedValue value "Should retrieve the stored value"
                | None -> failtest "Should retrieve stored value"
            }
            
            test "should store and retrieve integer value" {
                let cache = TestHelpers.createCacheService()
                let key = "int-key"
                let value = TestHelpers.testInt
                
                cache.Set(key, value, TestHelpers.longTtl)
                let result = cache.Get<int>(key)
                
                match result with
                | Some retrievedValue -> Expect.equal retrievedValue value "Should retrieve the stored integer"
                | None -> failtest "Should retrieve stored integer"
            }
            
            test "should store and retrieve complex domain objects" {
                let cache = TestHelpers.createCacheService()
                let key = "user-profile"
                let value = TestHelpers.testUserProfile
                
                cache.Set(key, value, TestHelpers.longTtl)
                let result = cache.Get<UserProfile>(key)
                
                match result with
                | Some retrievedProfile -> Expect.equal retrievedProfile value "Should retrieve the stored user profile"
                | None -> failtest "Should retrieve stored user profile"
            }
            
            test "should return None for non-existent key" {
                let cache = TestHelpers.createCacheService()
                
                let result = cache.Get<string>("non-existent-key")
                
                match result with
                | Some _ -> failtest "Should return None for non-existent key"
                | None -> () // Expected
            }
            
            test "should overwrite existing value" {
                let cache = TestHelpers.createCacheService()
                let key = "overwrite-key"
                let value1 = "first-value"
                let value2 = "second-value"
                
                cache.Set(key, value1, TestHelpers.longTtl)
                cache.Set(key, value2, TestHelpers.longTtl)
                let result = cache.Get<string>(key)
                
                match result with
                | Some retrievedValue -> Expect.equal retrievedValue value2 "Should have the second value"
                | None -> failtest "Should retrieve overwritten value"
            }
            
            test "should handle multiple different keys" {
                let cache = TestHelpers.createCacheService()
                let key1, value1 = "key1", "value1"
                let key2, value2 = "key2", "value2"
                let key3, value3 = "key3", "value3"
                
                cache.Set(key1, value1, TestHelpers.longTtl)
                cache.Set(key2, value2, TestHelpers.longTtl)
                cache.Set(key3, value3, TestHelpers.longTtl)
                
                let result1 = cache.Get<string>(key1)
                let result2 = cache.Get<string>(key2)
                let result3 = cache.Get<string>(key3)
                
                Assertions.assertResultOk (Some value1) (Ok result1)
                Assertions.assertResultOk (Some value2) (Ok result2)
                Assertions.assertResultOk (Some value3) (Ok result3)
            }
        ]
    
    /// Tests for TTL and expiration functionality
    module TTLExpirationTests =
        
        [<Tests>]
        let tests = testList "TTL and Expiration" [
            
            test "should expire entry after TTL" {
                let cache = TestHelpers.createCacheService()
                let key = "expire-key"
                let value = TestHelpers.testString
                
                cache.Set(key, value, TestHelpers.shortTtl)
                
                // Should be available immediately
                let immediateResult = cache.Get<string>(key)
                match immediateResult with
                | Some _ -> () // Expected
                | None -> failtest "Should be available immediately after setting"
                
                // Wait for expiration
                TestHelpers.waitForExpiration(150) // Wait longer than shortTtl
                
                // Should be expired now
                let expiredResult = cache.Get<string>(key)
                match expiredResult with
                | Some _ -> failtest "Should be expired after TTL"
                | None -> () // Expected
            }
            
            test "should not expire entry before TTL" {
                let cache = TestHelpers.createCacheService()
                let key = "not-expire-key"
                let value = TestHelpers.testString
                let mediumTtl = TimeSpan.FromMilliseconds(300.0)
                
                cache.Set(key, value, mediumTtl)
                
                // Wait for less than TTL
                TestHelpers.waitForExpiration(100)
                
                // Should still be available
                let result = cache.Get<string>(key)
                match result with
                | Some retrievedValue -> Expect.equal retrievedValue value "Should still be available before TTL"
                | None -> failtest "Should not expire before TTL"
            }
            
            test "should handle different TTLs for different keys" {
                let cache = TestHelpers.createCacheService()
                let quickKey, quickValue = "quick", "quick-value"
                let slowKey, slowValue = "slow", "slow-value"
                
                cache.Set(quickKey, quickValue, TestHelpers.shortTtl)
                cache.Set(slowKey, slowValue, TestHelpers.longTtl)
                
                // Wait for quick expiration
                TestHelpers.waitForExpiration(150)
                
                let quickResult = cache.Get<string>(quickKey)
                let slowResult = cache.Get<string>(slowKey)
                
                match quickResult, slowResult with
                | None, Some slowValue' -> 
                    Expect.equal slowValue' slowValue "Slow value should still be available"
                | Some _, _ -> failtest "Quick value should be expired"
                | None, None -> failtest "Slow value should still be available"
            }
        ]
    
    /// Tests for cache removal and clearing
    module RemovalClearingTests =
        
        [<Tests>]
        let tests = testList "Removal and Clearing" [
            
            test "should remove specific key" {
                let cache = TestHelpers.createCacheService()
                let key1, value1 = "keep", "keep-value"
                let key2, value2 = "remove", "remove-value"
                
                cache.Set(key1, value1, TestHelpers.longTtl)
                cache.Set(key2, value2, TestHelpers.longTtl)
                
                cache.Remove(key2)
                
                let result1 = cache.Get<string>(key1)
                let result2 = cache.Get<string>(key2)
                
                match result1, result2 with
                | Some value, None -> Expect.equal value value1 "Should keep non-removed value"
                | Some _, Some _ -> failtest "Should remove specified key"
                | None, _ -> failtest "Should not remove other keys"
            }
            
            test "should handle removing non-existent key gracefully" {
                let cache = TestHelpers.createCacheService()
                
                // Should not throw exception
                cache.Remove("non-existent-key")
                
                // Should still work normally after
                cache.Set("test", "value", TestHelpers.longTtl)
                let result = cache.Get<string>("test")
                
                match result with
                | Some "value" -> () // Expected
                | _ -> failtest "Should work normally after removing non-existent key"
            }
            
            test "should clear all cached values" {
                let cache = TestHelpers.createCacheService()
                
                cache.Set("key1", "value1", TestHelpers.longTtl)
                cache.Set("key2", "value2", TestHelpers.longTtl)
                cache.Set("key3", "value3", TestHelpers.longTtl)
                
                cache.Clear()
                
                let result1 = cache.Get<string>("key1")
                let result2 = cache.Get<string>("key2")
                let result3 = cache.Get<string>("key3")
                
                match result1, result2, result3 with
                | None, None, None -> () // Expected
                | _ -> failtest "Should clear all values"
            }
            
            test "should work normally after clearing" {
                let cache = TestHelpers.createCacheService()
                
                cache.Set("before-clear", "value", TestHelpers.longTtl)
                cache.Clear()
                cache.Set("after-clear", "new-value", TestHelpers.longTtl)
                
                let result = cache.Get<string>("after-clear")
                
                match result with
                | Some "new-value" -> () // Expected
                | _ -> failtest "Should work normally after clearing"
            }
        ]
    
    /// Tests for cleanup operations
    module CleanupTests =
        
        [<Tests>]
        let tests = testList "Cleanup Operations" [
            
            test "should cleanup expired entries" {
                let cache = TestHelpers.createCacheService()
                let expiredKey, expiredValue = "expired", "expired-value"
                let validKey, validValue = "valid", "valid-value"
                
                cache.Set(expiredKey, expiredValue, TestHelpers.shortTtl)
                cache.Set(validKey, validValue, TestHelpers.longTtl)
                
                // Wait for expiration
                TestHelpers.waitForExpiration(150)
                
                // Cleanup expired entries
                cache.CleanupExpired()
                
                let expiredResult = cache.Get<string>(expiredKey)
                let validResult = cache.Get<string>(validKey)
                
                match expiredResult, validResult with
                | None, Some value -> Expect.equal value validValue "Should keep valid entries"
                | Some _, _ -> failtest "Should cleanup expired entries"
                | None, None -> failtest "Should keep valid entries"
            }
            
            test "should handle cleanup with no expired entries" {
                let cache = TestHelpers.createCacheService()
                let key, value = "valid", "value"
                
                cache.Set(key, value, TestHelpers.longTtl)
                cache.CleanupExpired()
                
                let result = cache.Get<string>(key)
                
                match result with
                | Some retrievedValue -> Expect.equal retrievedValue value "Should not affect valid entries"
                | None -> failtest "Should not remove valid entries during cleanup"
            }
            
            test "should handle cleanup with empty cache" {
                let cache = TestHelpers.createCacheService()
                
                // Should not throw exception
                cache.CleanupExpired()
                
                // Should still work normally after
                cache.Set("test", "value", TestHelpers.longTtl)
                let result = cache.Get<string>("test")
                
                match result with
                | Some "value" -> () // Expected
                | _ -> failtest "Should work normally after cleanup on empty cache"
            }
        ]
    
    /// Tests for type safety and error handling
    module TypeSafetyTests =
        
        [<Tests>]
        let tests = testList "Type Safety" [
            
            test "should handle type mismatch gracefully" {
                let cache = TestHelpers.createCacheService()
                let key = "type-mismatch-key"
                
                // Store as string
                cache.Set(key, "string-value", TestHelpers.longTtl)
                
                // Try to retrieve as int
                let result = cache.Get<int>(key)
                
                match result with
                | Some _ -> failtest "Should not return value for type mismatch"
                | None -> () // Expected - type mismatch should return None
            }
            
            test "should clean up after type mismatch" {
                let cache = TestHelpers.createCacheService()
                let key = "cleanup-type-mismatch"
                
                // Store as string
                cache.Set(key, "string-value", TestHelpers.longTtl)
                
                // Try to retrieve as int (should fail and clean up)
                let _ = cache.Get<int>(key)
                
                // Try to retrieve as string (should also return None since entry was cleaned)
                let result = cache.Get<string>(key)
                
                match result with
                | Some _ -> failtest "Should clean up entry after type mismatch"
                | None -> () // Expected
            }
            
            test "should handle complex domain types correctly" {
                let cache = TestHelpers.createCacheService()
                let userKey = "user-profile"
                let playlistKey = "playlist"
                
                cache.Set(userKey, TestHelpers.testUserProfile, TestHelpers.longTtl)
                cache.Set(playlistKey, TestHelpers.testPlaylist, TestHelpers.longTtl)
                
                let userResult = cache.Get<UserProfile>(userKey)
                let playlistResult = cache.Get<PlaylistInfo>(playlistKey)
                
                match userResult, playlistResult with
                | Some user, Some playlist ->
                    Expect.equal user TestHelpers.testUserProfile "Should retrieve correct user profile"
                    Expect.equal playlist TestHelpers.testPlaylist "Should retrieve correct playlist"
                | _ -> failtest "Should retrieve complex domain types correctly"
            }
        ]
    
    /// Tests for concurrent access (basic thread safety)
    module ConcurrencyTests =
        
        [<Tests>]
        let tests = testList "Concurrency" [
            
            test "should handle concurrent reads and writes" {
                let cache = TestHelpers.createCacheService()
                let key = "concurrent-key"
                
                // Start multiple tasks that read and write concurrently
                let tasks = [
                    async { 
                        for i in 1..100 do
                            cache.Set($"{key}-{i}", $"value-{i}", TestHelpers.longTtl)
                    }
                    async {
                        for i in 1..100 do
                            cache.Get<string>($"{key}-{i}") |> ignore
                    }
                    async {
                        for i in 1..50 do
                            cache.Remove($"{key}-{i}")
                    }
                ]
                
                // Run all tasks concurrently
                Async.Parallel tasks |> Async.RunSynchronously |> ignore
                
                // Should not crash and should be able to continue working
                cache.Set("after-concurrent", "test", TestHelpers.longTtl)
                let result = cache.Get<string>("after-concurrent")
                
                match result with
                | Some "test" -> () // Expected - cache should still work
                | _ -> failtest "Should handle concurrent access gracefully"
            }
        ]
    
    /// Tests for edge cases and boundary conditions
    module EdgeCaseTests =
        
        [<Tests>]
        let tests = testList "Edge Cases" [
            
            test "should handle empty string keys" {
                let cache = TestHelpers.createCacheService()
                let key = ""
                let value = "empty-key-value"
                
                cache.Set(key, value, TestHelpers.longTtl)
                let result = cache.Get<string>(key)
                
                match result with
                | Some retrievedValue -> Expect.equal retrievedValue value "Should handle empty string keys"
                | None -> failtest "Should store and retrieve with empty string key"
            }
            
            test "should handle very long keys" {
                let cache = TestHelpers.createCacheService()
                let longKey = String.replicate 1000 "a"
                let value = "long-key-value"
                
                cache.Set(longKey, value, TestHelpers.longTtl)
                let result = cache.Get<string>(longKey)
                
                match result with
                | Some retrievedValue -> Expect.equal retrievedValue value "Should handle very long keys"
                | None -> failtest "Should store and retrieve with long keys"
            }
            
            test "should handle null values gracefully" {
                let cache = TestHelpers.createCacheService()
                let key = "null-value-key"
                
                cache.Set(key, null, TestHelpers.longTtl)
                let result = cache.Get<string>(key)
                
                match result with
                | Some null -> () // Expected - null should be stored and retrieved
                | Some _ -> failtest "Should store null as null"
                | None -> failtest "Should be able to store and retrieve null"
            }
            
            test "should handle zero TTL" {
                let cache = TestHelpers.createCacheService()
                let key = "zero-ttl-key"
                let value = "zero-ttl-value"
                let zeroTtl = TimeSpan.Zero
                
                cache.Set(key, value, zeroTtl)
                let result = cache.Get<string>(key)
                
                match result with
                | Some _ -> failtest "Should immediately expire with zero TTL"
                | None -> () // Expected - should be expired immediately
            }
            
            test "should handle negative TTL" {
                let cache = TestHelpers.createCacheService()
                let key = "negative-ttl-key"
                let value = "negative-ttl-value"
                let negativeTtl = TimeSpan.FromMilliseconds(-100.0)
                
                cache.Set(key, value, negativeTtl)
                let result = cache.Get<string>(key)
                
                match result with
                | Some _ -> failtest "Should immediately expire with negative TTL"
                | None -> () // Expected - should be expired immediately
            }
        ]
    
    /// Property-based tests
    module PropertyBasedTests =
        
        [<Tests>]
        let tests = testList "Property-Based Tests" [
            
            testCase "stored values should be retrievable within TTL" <| fun (key: string) (value: string) ->
                let cache = TestHelpers.createCacheService()
                let longTtl = TimeSpan.FromHours(1.0)
                
                cache.Set(key, value, longTtl)
                let result = cache.Get<string>(key)
                
                match result with
                | Some retrievedValue -> retrievedValue = value
                | None -> false
            
            testCase "different keys should not interfere" <| fun (key1: string) (key2: string) (value1: string) (value2: string) ->
                key1 <> key2 ==> lazy (
                    let cache = TestHelpers.createCacheService()
                    let longTtl = TestHelpers.longTtl
                    
                    cache.Set(key1, value1, longTtl)
                    cache.Set(key2, value2, longTtl)
                    
                    let result1 = cache.Get<string>(key1)
                    let result2 = cache.Get<string>(key2)
                    
                    match result1, result2 with
                    | Some v1, Some v2 -> v1 = value1 && v2 = value2
                    | _ -> false
                )
        ]
    
    /// Combined test suite
    [<Tests>]
    let allTests = testList "CacheServiceTests" [
        BasicCacheOperationsTests.tests
        TTLExpirationTests.tests
        RemovalClearingTests.tests
        CleanupTests.tests
        TypeSafetyTests.tests
        ConcurrencyTests.tests
        EdgeCaseTests.tests
        PropertyBasedTests.tests
    ]