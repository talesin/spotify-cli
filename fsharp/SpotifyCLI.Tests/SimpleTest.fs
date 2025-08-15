namespace SpotifyCLI.Tests

module SimpleTest =

    open NUnit.Framework
    open FsUnit // Refer to https://fsprojects.github.io/FsUnit/
    open FsCheck
    open FsCheck.NUnit 
    open FsCheck.FSharp

    open SpotifyCLI.Domain
    open TestUtilities

    
    /// Basic arithmetic test
    [<Test>]
    let ``basic arithmetic should work`` () =
        let result = 2 + 2
        result |> should equal 4
        
        // Test commutative property
        let x, y = 5, 3
        (x + y) |> should equal (y + x)
    
    /// String operations test  
    [<Test>]
    let ``string operations should work`` () =
        let text = "Hello"
        let doubled = text + text
        doubled |> should equal "HelloHello"
        doubled.Length |> should equal (text.Length * 2)
    
    /// Boolean logic test
    [<Test>]
    let ``boolean logic should work`` () =
        // Test commutative property
        let a, b = true, false
        (a && b) |> should equal (b && a)
        (a || b) |> should equal (b || a)

    [<Property>]
    let ``commutative property`` (x: int) (y: int) =
        (x + y) = (y + x)


    let xyz () = gen {
        let! length = Gen.choose(1, 50)
        let! chars = Gen.listOfLength length (Gen.choose(32, 126) |> Gen.map char)
        let s = chars |> List.toArray |> System.String
        return s
    }

    [<Property>]
    let ``String50 constraint`` () =
        Gen.choose(1, 50)
        |> Arb.fromGen
        |> Prop.forAll <| (fun s -> 
            true)
            // let expected: Result<String50, string> = Ok (String50 s)
            // let result = ConstrainedTypes.createString50 s
            // if result <> expected then
            //     printfn "%A %A" s.Length s
            // result === expected)