module Expecto.RunnerServer.Tests
open System
open Expecto
open Expecto.RunnerServer

module internal Dummy =
    let singlePassing = test "passingTestCase" { Expect.equal (1 + 2) 3 "" }

    let singleFailing = test "failingTestCase" { Expect.equal (1 + 2) 42 "" }

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
            Expect.equalObj Dummy.singlePassing Dummy.singlePassing "This assertation should pass"
        }
        test "equalObj should return false when comparing two different Tests" {
            Expect.throwsT<AssertException> (fun () ->
                Expect.equalObj Dummy.singlePassing Dummy.singleFailing "Assertation"
            ) "The assertation should fail"
        }
    ]

[<Tests>]
let testRunnerAgentTests =
    testList "TestRunnerAgent" [
        testAsync "Should be able to add and get tests" {
            let agent = new TestRunnerAgent()
            let guid = Guid()
            agent.Post (Message.AddTest (guid, Dummy.singlePassing))
            let! retrieved = agent.PostAndAsyncReply (Message.TryGetTest guid)

            Expect.equalObj retrieved (Some Dummy.singlePassing) ""
        }
    ]