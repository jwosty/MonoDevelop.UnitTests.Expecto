module Expecto.RunnerServer.RunnerServer.Tests
open System
open Expecto
open Expecto.RunnerServer
open Expecto.RunnerServer.Tests

let flatTest name f =
  { name = name
    test = TestCode.Sync f
    state = FocusState.Normal
    focusOn = false
    sequenced = SequenceMethod.InParallel }

module internal Dummy =
    let flatPassing = flatTest "Single passing dummy test" (fun () -> Expect.equal (1 + 2) 3 "")

    let flatFailing = flatTest "Single failing dummy test" (fun () -> Expect.equal (1 + 2) 42 "")

module Expect =
    /// Like Expect.equal, but compares the objects using Object.Equals instead of the equality operator
    let equalObj (actual: obj) expected message =
        if not (actual.Equals expected) then
            Tests.failtestf "%s. Actual value was equal to %A but had epected them to be non-equal."
                message actual

[<Tests>]
let expectTests =
    testList "Expect" [
        test "equalObj should return true when comparing a Test to itself" {
            Expect.equalObj Dummy.flatPassing Dummy.flatPassing "This assertation should pass"
        }
        test "equalObj should return false when comparing two different Tests" {
            Expect.throwsT<AssertException> (fun () ->
                Expect.equalObj Dummy.flatPassing Dummy.flatFailing "Assertation"
            ) "The assertation should fail"
        }
    ]

/// Create a new test dictionary agent, and run a FlatTest inside of it, returning the result.
let runFlatTestInAgent test = async {
    let agent = new TestDictionaryAgent()
    let guid = Guid.NewGuid()
    agent.Post (TestDictionaryMessage.AddTest (guid, test))

    let! tryGetTestSummary = agent.PostAndAsyncReply (TestDictionaryMessage.TryGetTestResultGetter guid)
    Expect.isSome tryGetTestSummary "Check that the test / test agent exists"
    return! Option.get tryGetTestSummary
}

let makeTotallyRealTestSuite topLevelTestValues =
    let first = "module TotallyARealAssembly.Tests
open System.Reflection
open Expecto

[<assembly: AssemblyTitle(\"TotallyARealAssembly\")>]
do ()\n\n"

    let last = "
\n[<EntryPoint>]
let main argv =
    Tests.runTestsInAssembly Impl.ExpectoConfig.defaultConfig argv"

    first + (topLevelTestValues |> Seq.mapi (fun i tval -> sprintf "[<Tests>]\nlet tests%d = %s" (i + 1) tval) |> Seq.reduce (+)) + last

let totallyRealTestSuiteName = "totallyRealTestSuite"

let totallyRealTestSuiteV1 = makeTotallyRealTestSuite ["""test "passing test 1" { Expect.equal (10 * 2) 20 }"""]

let totallyRealTestSuiteV2 = makeTotallyRealTestSuite ["""test "passing test 1" { Expect.equal (10 * 2) 20 }"""
                                                       """test "passing test 2" { Expect.equal ("foo" + "bar") "foobar" }"""]

[<Tests>]
let testRunnerAgentTests =
    testList "TestDictionaryAgent" [
        testAsync "Should be able to add and get tests" {
            let agent = new TestDictionaryAgent()
            let guid = Guid.NewGuid()
            agent.Post (TestDictionaryMessage.AddTest (guid, Dummy.flatPassing))

            let! theTest = agent.PostAndAsyncReply (TestDictionaryMessage.TryGetTest guid)

            Expect.equalObj theTest (Some Dummy.flatPassing) ""
        }
        testAsync "Should be able to start tests" {
            let! tryGetTestSummary = runFlatTestInAgent Dummy.flatPassing

            let result = tryGetTestSummary |> Result.map (fun { result = result } -> result)
            Expect.equal result (Ok Impl.TestResult.Passed) "Check that the agent didn't crash and the test passed"
        }
        testAsync "Should be able to start tests and report failures" {
            let! tryGetTestSummary = runFlatTestInAgent Dummy.flatFailing

            let result = tryGetTestSummary |> Result.map (fun { result = result } -> result)
            match result with
            | Ok (Impl.TestResult.Failed _) -> ()
            | Ok testResult -> Tests.failtestf "Expected test to fail. Actual test result value was: %s" (string testResult)
            | Error e -> Tests.failtestf "Agent threw an exception: \n%s" (string e)
        }
        testAsync "Should be able to detect and load tests from an assembly" {
            let! asmPath = AssemblyCompiler.compile totallyRealTestSuiteV1 totallyRealTestSuiteName
            let agent = new TestDictionaryAgent()
            let! loadedTests = agent.PostAndAsyncReply (TestDictionaryMessage.AddTestsFromAssembly asmPath)
            match loadedTests with
            | Ok loadedTests -> Expect.equal loadedTests.Length 1 "Check that 1 test was loaded"
            | Error _ as x -> Expecto.Tests.failtestf "Expected loadedTests to be Ok, but got: %A" x
        }
    ]