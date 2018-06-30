module DummyProject.Flat
open Expecto

let test1 =
    test "DummyProjectTest" {
        Expect.equal (2 + 2) 4 ""
    }