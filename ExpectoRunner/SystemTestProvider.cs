using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mono.Addins;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;

namespace MonoDevelop.UnitTesting.Expecto {
    public class SystemTestProvider : ITestProvider {
        public SystemTestProvider() { }

        public void Foo () {
            ITestProvider x = this;
            
        }

        public UnitTest CreateUnitTest(WorkspaceObject entry) {
            return new ExpectoTestSuite("My test");
        }

        public void Dispose() { }
    }
}
