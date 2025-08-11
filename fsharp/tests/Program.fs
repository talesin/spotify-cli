namespace SpotifyCLI.Tests

open Expecto
open SpotifyCLI.Tests.Domain
open SpotifyCLI.Tests.Services

/// Main test program entry point
module TestProgram =
    
    /// All tests organized by category
    let allTests = testList "SpotifyCLI Tests" [
        TypesTests.allTests
        ConfigServiceTests.allTests
    ]
    
    /// Test runner configuration
    let config = { 
        defaultConfig with 
            // Increase timeout for slower tests
            timeout = System.TimeSpan.FromMinutes(2.0)
            // Run tests in parallel for speed
            parallel = true
            // Show test names and progress
            printer = Expecto.Logging.Targets.ConsoleTarget(
                name = "console", 
                minLevel = Expecto.Logging.LogLevel.Info,
                options = [])
    }

[<EntryPoint>]
let main args =
    // Run all tests with configuration
    Tests.runTestsWithCLIArgs TestProgram.config args TestProgram.allTests