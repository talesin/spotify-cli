# F# Coding Guide

## Core Design Principles

### 1. Type-Driven Design

- **Design types first**: Let the type system guide your domain modeling
- **Make illegal states unrepresentable**: Use the type system to eliminate entire classes of bugs
- **Use single-case discriminated unions** for domain primitives:
  ```fsharp
  type EmailAddress = EmailAddress of string
  type CustomerId = CustomerId of int
  ```

### 2. Domain Modeling with Types

- **Use discriminated unions for choices**:
  ```fsharp
  type PaymentMethod =
    | Cash
    | Cheque of CheckNumber
    | Card of CardType * CardNumber
  ```
- **Use records for structured data**:
  ```fsharp
  type PersonalName = {
      FirstName: String50
      MiddleInitial: String1 option
      LastName: String50
  }
  ```

### 3. Functional-First Approach

- **Prefer immutability**: Use mutable only when necessary
- **Favor composition over inheritance**
- **Use pure functions** whenever possible
- **Separate I/O from business logic**

## Code Organization

### Module Structure

```fsharp
// Domain types first
type CalculatorInput =
    | Digit of CalculatorDigit
    | Op of CalculatorMathOp
    | Action of CalculatorAction

// Service interfaces
type CalculatorServices = {
    updateDisplay: UpdateDisplay
    doMathOperation: DoMathOperation
    initState: InitState
}

// Implementation functions
module CalculatorImplementation =
    let createCalculate (services: CalculatorServices) =
        // implementation here
```

### File Organization

- **Domain types at the top** of modules
- **Service definitions** after types
- **Implementation functions** in separate modules
- **Use `[<AutoOpen>]`** sparingly for utilities

## Function Design

### Function Signatures

```fsharp
// Good: Clear parameter order (data last for piping)
let updateDisplayFromDigit digit display =
    // implementation

// Better: Explicit service dependencies
let updateDisplayFromDigit services digit display =
    // implementation
```

### Error Handling Patterns

#### Use Result for Domain Errors Only

```fsharp
type MathOperationResult =
    | Success of Number
    | Failure of MathOperationError

type MathOperationError =
    | DivideByZero
```

#### When NOT to use Result

- For diagnostics (use exceptions)
- For unexpected errors (use exceptions)
- When no one cares about error details (use Option)
- For I/O errors (use exceptions)
- For performance-critical code
- For interop scenarios

#### Prefer Option for Simple Cases

```fsharp
// When callers don't care about error details
type GetDisplayNumber = string -> float option
```

### Computation Expressions

```fsharp
type MaybeBuilder() =
    member this.Bind(x, f) = Option.bind f x
    member this.Return(x) = Some x

let maybe = MaybeBuilder()

// Usage
let result = maybe {
    let! x = getNumber()
    let! y = getAnotherNumber()
    return x + y
}
```

## Type System Best Practices

### Constrained Types

```fsharp
type String50 = String50 of string

let createString50 (s: string) =
    if s = null then None
    elif s.Length <= 50 then Some (String50 s)
    else None
```

### Value vs Entity Types

```fsharp
// Value Object - structural equality
[<StructuralEquality; NoComparison>]
type PersonalName = {
    FirstName: string
    LastName: string
}

// Entity - custom equality based on ID
[<CustomEquality; NoComparison>]
type Person = {
    Id: int
    Name: PersonalName
} with
    override this.Equals(other) =
        match other with
        | :? Person as p -> this.Id = p.Id
        | _ -> false
    override this.GetHashCode() = hash this.Id
```

### Working with Options

```fsharp
// Pattern matching
match someOption with
| Some value -> processValue value
| None -> handleMissingValue()

// Pipeline with Option.bind
someOption
|> Option.bind processStep1
|> Option.bind processStep2
|> Option.map finalTransform
```

## Dependency Management

### Dependency Injection Patterns

```fsharp
// Services record pattern
type AppServices = {
    Logger: ILogger
    Database: IDatabase
    Config: IConfig
}

// Function takes services as parameter
let processOrder services order =
    services.Logger.Info "Processing order"
    // implementation
```

### Reader Monad for Complex Dependencies

```fsharp
type Reader<'env, 'a> = Reader of ('env -> 'a)

module Reader =
    let run env (Reader f) = f env
    let ask = Reader id
    let map f reader = Reader (fun env -> f (run env reader))
```

## Pattern Matching Guidelines

### Exhaustive Matching

```fsharp
// Compiler enforces all cases are handled
let processInput input =
    match input with
    | Digit d -> handleDigit d
    | Op operation -> handleOperation operation
    | Action action -> handleAction action
```

### Active Patterns

```fsharp
let (|Even|Odd|) n =
    if n % 2 = 0 then Even else Odd

match someNumber with
| Even -> "even number"
| Odd -> "odd number"
```

## Async and Parallel Patterns

### Async Workflows

```fsharp
let fetchDataAsync url = async {
    let! response = Http.AsyncRequestString url
    return parseResponse response
}
```

### Task Integration

```fsharp
open System.Threading.Tasks

let fetchDataTask url = task {
    let! response = httpClient.GetStringAsync(url)
    return parseResponse response
}
```

## Testing Patterns

### Property-Based Testing Structure

```fsharp
[<Property>]
let ``Addition is commutative`` (x: int) (y: int) =
    x + y = y + x
```

### Unit Testing with Services

```fsharp
let mockServices = {
    Logger = MockLogger()
    Database = MockDatabase()
}

[<Test>]
let ``Test with mocked services`` () =
    let result = processOrder mockServices testOrder
    Assert.AreEqual(expected, result)
```

## Common Anti-Patterns to Avoid

### Don't Over-Use Result

- Not for exceptions that should crash the app
- Not when no one cares about error details
- Not for complex control flow hidden from consumers

### Don't Fight Type Inference

```fsharp
// Good: Let inference work
let numbers = [1; 2; 3]
let doubled = numbers |> List.map (fun x -> x * 2)

// Avoid: Unnecessary annotations
let numbers: int list = [1; 2; 3]
let doubled: int list = numbers |> List.map (fun (x: int) -> x * 2)
```

### Don't Create God Objects

```fsharp
// Bad: One giant record with everything
type MegaServices = {
    Logger: ILogger
    Database: IDatabase
    Email: IEmail
    Payment: IPayment
    // ... 20 more services
}

// Good: Focused service groups
type CoreServices = { Logger: ILogger; Database: IDatabase }
type PaymentServices = { Payment: IPayment; Billing: IBilling }
```

## Performance Considerations

### Choose Appropriate Data Structures

- **List**: Sequential access, head operations
- **Array**: Random access, imperative style
- **Seq**: Lazy evaluation, large datasets
- **Map**: Key-value lookup, immutable
- **Dictionary**: Key-value lookup, mutable

### Tail Recursion

```fsharp
// Tail recursive (good)
let rec sumTailRec acc = function
    | [] -> acc
    | x :: xs -> sumTailRec (acc + x) xs

// Not tail recursive (stack overflow risk)
let rec sumNonTail = function
    | [] -> 0
    | x :: xs -> x + sumNonTail xs
```

## Interop Guidelines

### Working with .NET APIs

```fsharp
// Handle nullable values
let safeToString (x: obj) =
    match x with
    | null -> None
    | _ -> Some (x.ToString())

// Explicit type annotations for overloaded methods
let concat (items: string[]) = String.Concat(items)
```

### Creating F#-Friendly APIs

```fsharp
// Use Option instead of null
type SafeApi = {
    TryGetValue: string -> string option
    ProcessItems: string list -> Result<string, Error>
}
```

## Documentation and Naming

### Function Naming

- Use descriptive names: `calculateTotalPrice` not `calc`
- Verb phrases for functions: `validateEmail`, `processOrder`
- Noun phrases for values: `emailAddress`, `orderTotal`

### Module Organization

```fsharp
module MyDomain.Orders.Processing

// Domain types
type Order = { ... }
type OrderError = { ... }

// Public API
let processOrder: Order -> Result<ProcessedOrder, OrderError>

// Internal implementation
module Internal =
    let validateOrder: Order -> Result<Order, ValidationError>
```

## Summary

F# excels when you:

1. **Design with types first** - let the type system guide your modeling
2. **Use the right tool for the job** - Result for domain errors, exceptions for panics
3. **Embrace immutability and purity** - separate effects from pure logic
4. **Leverage pattern matching** - make illegal states unrepresentable
5. **Compose functions** rather than building complex inheritance hierarchies

Remember: The goal is not to use every F# feature, but to write clear, maintainable code that leverages F#'s strengths in type safety and functional composition.
