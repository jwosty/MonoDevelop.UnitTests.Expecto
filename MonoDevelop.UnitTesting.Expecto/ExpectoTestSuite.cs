using System;
using MonoDevelop.Projects;
using MonoDevelop.UnitTesting;

namespace MonoDevelop.UnitTesting.Expecto {
    public class ExpectoTestSuite : UnitTest {
        public ExpectoTestSuite(string name) : base(name) { }

        public ExpectoTestSuite(string name, WorkspaceObject ownerSolutionItem) : base(name, ownerSolutionItem) { }

        protected override UnitTestResult OnRun(TestContext testContext) {
            throw new NotImplementedException();
        }
    }
}
