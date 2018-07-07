module Expecto.RunnerServer.Tests
open System
open Expecto
open Expecto.RunnerServer

let flatTest name f =
  { name = name
    test = TestCode.Sync f
    state = FocusState.Normal
    focusOn = false
    sequenced = SequenceMethod.InParallel }

module internal Dummy =
    let flatPassingName = "Single passing dummy test"
    let flatPassing = flatTest flatPassingName (fun () -> Expect.equal (1 + 2) 3 "")

    let flatFailingName = "Single failing dummy test"
    let flatFailing = flatTest flatFailingName (fun () -> Expect.equal (1 + 2) 42 "")

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

[<Tests>]
let testRunnerAgentTests =
    testList "TestDictionaryAgent" [
        testAsync "Should be able to add and get tests" {
            let agent = new TestDictionaryAgent()
            let guid = Guid()
            agent.Post (TestDictionaryMessage.AddTest (guid, Dummy.flatPassing))

            let! theTest = agent.PostAndAsyncReply (TestDictionaryMessage.TryGetTest guid)

            Expect.equalObj theTest (Some Dummy.flatPassing) ""
        }
        testAsync "Should be able to start tests" {
            let agent = new TestDictionaryAgent()
            let guid = Guid()
            agent.Post (TestDictionaryMessage.AddTest (guid, Dummy.flatPassing))

            let! tryGetTestSummary = agent.PostAndAsyncReply (TestDictionaryMessage.TryGetTestResultGetter guid)
            Expect.isSome tryGetTestSummary "Check that the test / test agent exists"
            
            let! tryGetTestSummary = Option.get tryGetTestSummary

            match tryGetTestSummary with
            | Ok { result = result } ->
                Expect.equal result Impl.TestResult.Passed "Check that test passed"
            | Error _ as x ->
                Expect.isOk x "Check that the test agent didn't crash."
        }
    ]